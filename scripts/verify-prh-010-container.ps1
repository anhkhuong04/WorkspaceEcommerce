[CmdletBinding()]
param(
    [string]$ImageTag = 'workspace-ecommerce-api:ci',
    [string]$MigrationImageTag = 'workspace-ecommerce-api-migrate:ci',
    [string]$ArtifactsDirectory = (Join-Path $PSScriptRoot '..\artifacts\container')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$networkName = "prh010-network-$suffix"
$postgresContainer = "prh010-postgres-$suffix"
$apiContainer = "prh010-api-$suffix"
$database = 'workspace_ecommerce_ci'
$username = 'workspace_ecommerce'
$password = 'prh010-ci-only-password'
$connectionString = "Host=$postgresContainer;Port=5432;Database=$database;Username=$username;Password=$password"
$resultPath = Join-Path $ArtifactsDirectory 'container-smoke.json'
$runtimeUserId = $null

function Invoke-Docker {
    param([Parameter(ValueFromRemainingArguments)] [string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed: docker $($Arguments -join ' ')"
    }
}

function Wait-ForPostgres {
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $postgresContainer pg_isready -U $username -d $database | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Temporary PostgreSQL container '$postgresContainer' did not become ready."
}

function Invoke-HealthProbe {
    param([Parameter(Mandatory)] [string]$Path)

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker run --rm --network $networkName curlimages/curl:8.12.1 `
            --silent --show-error --fail --max-time 5 "http://$apiContainer`:8080$Path" | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Health probe '$Path' did not become successful."
}

function Assert-ApiRuntimeHardening {
    $script:runtimeUserId = (& docker exec $apiContainer id -u).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect runtime identity for API container '$apiContainer'."
    }

    if ($script:runtimeUserId -ne '10001') {
        throw "API container '$apiContainer' must run as the fixed unprivileged UID 10001; actual UID was '$script:runtimeUserId'."
    }

    & docker exec $apiContainer /bin/sh -ec '
        test -w /app/wwwroot/media
        test -w /var/lib/workspace-ecommerce/keys
    '
    if ($LASTEXITCODE -ne 0) {
        throw "API container '$apiContainer' cannot use its explicitly writable runtime directories safely."
    }
}

New-Item -ItemType Directory -Force -Path $ArtifactsDirectory | Out-Null

$commonEnvironment = @(
    '-e', 'ASPNETCORE_ENVIRONMENT=Development',
    '-e', 'ASPNETCORE_URLS=http://+:8080',
    '-e', 'AllowedHosts=*',
    '-e', "ConnectionStrings__DefaultConnection=$connectionString",
    '-e', 'AdminAuth__Email=ci-admin@example.test',
    '-e', 'AdminAuth__Password=prh010-ci-only-admin-password',
    '-e', 'Jwt__Issuer=WorkspaceEcommerce.CI',
    '-e', 'Jwt__Audience=WorkspaceEcommerce.CI',
    '-e', 'Jwt__SigningKey=prh010-ci-only-signing-key-that-is-long-enough',
    '-e', 'Jwt__AccessTokenMinutes=60',
    '-e', 'EmailDelivery__Provider=Log',
    '-e', 'MediaStorage__Provider=Local',
    '-e', 'MediaStorage__PublicBaseUrl=http://api:8080')

$succeeded = $false
$failure = $null
try {
    Invoke-Docker -Arguments @('network', 'create', $networkName)
    $postgresArguments = @(
        'run', '--rm', '-d', '--name', $postgresContainer, '--network', $networkName,
        '-e', "POSTGRES_DB=$database",
        '-e', "POSTGRES_USER=$username",
        '-e', "POSTGRES_PASSWORD=$password",
        'postgres:17-alpine')
    Invoke-Docker -Arguments $postgresArguments
    Wait-ForPostgres

    # The migration container runs before the API; the API never mutates schema at startup.
    $migrationArguments = @('run', '--rm', '--network', $networkName) +
        $commonEnvironment + @($MigrationImageTag)
    Invoke-Docker -Arguments $migrationArguments
    $apiArguments = @('run', '-d', '--name', $apiContainer, '--network', $networkName) +
        $commonEnvironment + @($ImageTag)
    Invoke-Docker -Arguments $apiArguments

    Assert-ApiRuntimeHardening
    Invoke-HealthProbe '/health/live'
    Invoke-HealthProbe '/health/ready'
    $succeeded = $true
}
catch {
    $failure = $_.Exception.Message
    if (& docker ps -a --format '{{.Names}}' | Select-String -SimpleMatch $apiContainer -Quiet) {
        & docker logs $apiContainer 2>&1 | Out-Host
    }

    throw
}
finally {
    [pscustomobject]@{
        Succeeded = $succeeded
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Image = $ImageTag
        MigrationImage = $MigrationImageTag
        RuntimeUserId = $runtimeUserId
        Probes = @('/health/live', '/health/ready')
        Failure = $failure
    } | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8

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

Write-Output "PRH-010 container smoke passed. Evidence: '$resultPath'."
