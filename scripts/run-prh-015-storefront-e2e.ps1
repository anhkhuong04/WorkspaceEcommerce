[CmdletBinding()]
param(
    [string]$ImageTag = 'workspace-ecommerce-api:prh015-e2e',
    [string]$MigrationImageTag = 'workspace-ecommerce-api-migrate:prh015-e2e',
    [string]$ArtifactsDirectory = (Join-Path $PSScriptRoot '..\artifacts\frontend\e2e'),
    [switch]$SkipImageBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [IO.Path]::IsPathRooted($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path $repositoryRoot $ArtifactsDirectory
}

$ArtifactsDirectory = [IO.Path]::GetFullPath($ArtifactsDirectory)
New-Item -ItemType Directory -Force -Path $ArtifactsDirectory | Out-Null

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$networkName = "prh015-e2e-network-$suffix"
$postgresContainer = "prh015-e2e-postgres-$suffix"
$apiContainer = "prh015-e2e-api-$suffix"
$database = 'workspace_ecommerce_e2e'
$username = 'workspace_ecommerce'
$apiPort = $null
$storefrontPort = $null
$apiBaseUrl = $null
$storefrontBaseUrl = $null
$resultPath = Join-Path $ArtifactsDirectory 'storefront-e2e-smoke.json'
$succeeded = $false
$failure = $null

function Assert-RequiredCommand {
    param([Parameter(Mandatory)][string]$Name)

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' is not available on PATH."
    }
}

function New-IsolatedSecret {
    param(
        [ValidateRange(6, 64)]
        [int]$ByteCount = 32
    )

    $bytes = [byte[]]::new($ByteCount)
    $randomNumberGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $randomNumberGenerator.GetBytes($bytes)
    }
    finally {
        $randomNumberGenerator.Dispose()
    }

    return [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}

$password = New-IsolatedSecret
$adminEmail = "e2e-$suffix@example.test"
$adminPassword = New-IsolatedSecret
$jwtSigningKey = "e2e-$(New-IsolatedSecret)"
$miniLogisticsApiKey = New-IsolatedSecret
$miniLogisticsWebhookSecret = New-IsolatedSecret
$vnPayTmnCode = "E2E$(New-IsolatedSecret -ByteCount 6)"
$vnPayHashSecret = New-IsolatedSecret
$connectionString = "Host=$postgresContainer;Port=5432;Database=$database;Username=$username;Password=$password"

function Invoke-Docker {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        # Do not echo the Docker argument list: it contains isolated test credentials.
        throw "Docker operation '$($Arguments[0])' failed. Inspect the isolated-run artifacts for details."
    }
}

function Get-AvailableLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForPostgres {
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        & docker exec $postgresContainer pg_isready -U $username -d $database | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "The isolated PostgreSQL container did not become ready."
}

function Wait-ForApiReadiness {
    param([Parameter(Mandatory)][string]$Url)

    $lastResult = 'no response'
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -TimeoutSec 3 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                return
            }

            $lastResult = "HTTP $($response.StatusCode)"
        }
        catch {
            # The API may still be applying its startup initialization.
            $lastResult = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    }

    throw "The isolated API readiness endpoint did not return HTTP 200. Last result: $lastResult"
}

function Test-LocalImage {
    param([Parameter(Mandatory)][string]$Tag)

    & docker image inspect $Tag *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Required local Docker image '$Tag' was not found. Run without -SkipImageBuild or build it first."
    }
}

function Save-ContainerLogs {
    param(
        [Parameter(Mandatory)][string]$ContainerName,
        [Parameter(Mandatory)][string]$Path
    )

    $containerIds = @(& docker ps --all --quiet --filter "name=^/$ContainerName`$")
    if ($containerIds.Count -eq 0) {
        return
    }

    # Some base images emit benign diagnostic text to stderr. Cleanup evidence must
    # never turn that native stderr into a terminating PowerShell error or hide the
    # result of the browser test.
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & docker logs $ContainerName 2>&1 | Out-File -LiteralPath $Path -Encoding utf8
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

Assert-RequiredCommand -Name docker
Assert-RequiredCommand -Name corepack

try {
    Push-Location $repositoryRoot
    try {
        if ($SkipImageBuild) {
            Test-LocalImage -Tag $ImageTag
            Test-LocalImage -Tag $MigrationImageTag
        }
        else {
            Invoke-Docker -Arguments @(
                'build', '--file', 'src/WorkspaceEcommerce.Api/Dockerfile', '--target', 'final',
                '--tag', $ImageTag, '.')
            Invoke-Docker -Arguments @(
                'build', '--file', 'src/WorkspaceEcommerce.Api/Dockerfile', '--target', 'migrate',
                '--tag', $MigrationImageTag, '.')
        }
    }
    finally {
        Pop-Location
    }

    $apiPort = Get-AvailableLoopbackPort
    $storefrontPort = Get-AvailableLoopbackPort
    $apiBaseUrl = "http://127.0.0.1:$apiPort"
    $storefrontBaseUrl = "http://127.0.0.1:$storefrontPort"

    # Every runtime endpoint is created here. The browser configuration independently
    # rejects non-loopback URLs, so this runner cannot be pointed at a deployed system.
    $commonEnvironment = @(
        '-e', 'ASPNETCORE_ENVIRONMENT=Development',
        '-e', 'ASPNETCORE_URLS=http://+:8080',
        '-e', 'AllowedHosts=localhost;127.0.0.1',
        '-e', "ConnectionStrings__DefaultConnection=$connectionString",
        '-e', "AdminAuth__Email=$adminEmail",
        '-e', "AdminAuth__Password=$adminPassword",
        '-e', 'Jwt__Issuer=WorkspaceEcommerce.E2E',
        '-e', 'Jwt__Audience=WorkspaceEcommerce.E2E',
        '-e', "Jwt__SigningKey=$jwtSigningKey",
        '-e', 'Jwt__AccessTokenMinutes=60',
        '-e', 'EmailDelivery__Provider=Log',
        '-e', 'EmailDelivery__WorkerIntervalSeconds=3600',
        '-e', 'EmailDelivery__WorkerBatchSize=1',
        '-e', 'EmailDelivery__LeaseDurationSeconds=120',
        '-e', 'EmailDelivery__MaxDeliveryAttempts=1',
        '-e', 'MediaStorage__Provider=Local',
        '-e', "MediaStorage__PublicBaseUrl=$apiBaseUrl",
        '-e', "Storefront__BaseUrl=$storefrontBaseUrl",
        '-e', 'MiniLogistics__BaseUrl=http://127.0.0.1:9/api/v1/partner',
        '-e', "MiniLogistics__ApiKey=$miniLogisticsApiKey",
        '-e', "MiniLogistics__WebhookSecret=$miniLogisticsWebhookSecret",
        '-e', 'MiniLogistics__CommandWorkerIntervalSeconds=3600',
        '-e', "Payment__VNPay__TmnCode=$vnPayTmnCode",
        '-e', "Payment__VNPay__HashSecret=$vnPayHashSecret",
        '-e', 'Payment__VNPay__PaymentUrl=http://127.0.0.1:9/payment',
        '-e', "Payment__VNPay__ReturnUrl=$apiBaseUrl/api/payments/vnpay/return",
        '-e', "Payment__VNPay__IpnUrl=$apiBaseUrl/api/payments/vnpay/ipn",
        '-e', 'DemoSeed__IncludeExternalCatalog=false'
    )

    Invoke-Docker -Arguments @('network', 'create', $networkName)
    Invoke-Docker -Arguments @(
        'run', '--rm', '-d', '--name', $postgresContainer, '--network', $networkName,
        '-e', "POSTGRES_DB=$database",
        '-e', "POSTGRES_USER=$username",
        '-e', "POSTGRES_PASSWORD=$password",
        'postgres:17-alpine')
    Wait-ForPostgres

    # Schema and deterministic demo data are initialized once before the API starts.
    Invoke-Docker -Arguments (@('run', '--rm', '--network', $networkName) + $commonEnvironment + @($MigrationImageTag))
    Invoke-Docker -Arguments (@('run', '--rm', '--network', $networkName) + $commonEnvironment + @($ImageTag, '--seed-demo'))
    Invoke-Docker -Arguments (@(
            'run', '-d', '--name', $apiContainer, '--network', $networkName,
            '--publish', "127.0.0.1:$apiPort`:8080") + $commonEnvironment + @($ImageTag))
    Wait-ForApiReadiness -Url "$apiBaseUrl/health/ready"

    $e2eEnvironment = @{
        E2E_ISOLATED_RUN = 'true'
        E2E_STOREFRONT_URL = $storefrontBaseUrl
        E2E_STOREFRONT_PORT = [string]$storefrontPort
        E2E_API_PROXY_TARGET = $apiBaseUrl
        E2E_ARTIFACTS_DIR = $ArtifactsDirectory
        VITE_API_BASE_URL = ''
        VITE_API_PROXY_TARGET = $apiBaseUrl
        VITE_CART_SESSION_ID = 'prh015-e2e-cart'
    }
    $previousEnvironment = @{}
    foreach ($entry in $e2eEnvironment.GetEnumerator()) {
        $previousEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    Push-Location (Join-Path $repositoryRoot 'frontend')
    try {
        # This is idempotent and non-interactive. CI installs system browser dependencies
        # separately; local Windows/macOS runs only need the Chromium binary.
        & corepack pnpm exec playwright install chromium
        if ($LASTEXITCODE -ne 0) {
            throw 'Playwright Chromium installation failed.'
        }

        & corepack pnpm test:e2e:storefront
        if ($LASTEXITCODE -ne 0) {
            throw 'Storefront Playwright smoke failed.'
        }
    }
    finally {
        Pop-Location
        foreach ($entry in $previousEnvironment.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
    }

    $succeeded = $true
}
catch {
    $failure = $_.Exception.Message
    throw
}
finally {
    Save-ContainerLogs -ContainerName $apiContainer -Path (Join-Path $ArtifactsDirectory 'api-container.log')
    Save-ContainerLogs -ContainerName $postgresContainer -Path (Join-Path $ArtifactsDirectory 'postgres-container.log')

    [pscustomobject]@{
        Task = 'PRH-015 storefront browser E2E smoke'
        Succeeded = $succeeded
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Browser = 'chromium'
        Image = $ImageTag
        MigrationImage = $MigrationImageTag
        Isolation = [pscustomobject]@{
            ApiBaseUrl = $apiBaseUrl
            StorefrontBaseUrl = $storefrontBaseUrl
            DockerNetwork = $networkName
            Database = $database
        }
        Failure = $failure
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding utf8

    foreach ($containerName in @($apiContainer, $postgresContainer)) {
        $containerIds = @(& docker ps --all --quiet --filter "name=^/$containerName`$")
        if ($containerIds.Count -gt 0) {
            & docker rm -f $containerName *> $null
        }
    }

    $networkIds = @(& docker network ls --quiet --filter "name=^$networkName`$")
    if ($networkIds.Count -gt 0) {
        & docker network rm $networkName *> $null
    }
}

Write-Output "PRH-015 isolated storefront browser smoke passed. Evidence: '$resultPath'."
