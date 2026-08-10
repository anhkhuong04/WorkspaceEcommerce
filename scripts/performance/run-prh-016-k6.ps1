[CmdletBinding()]
param(
    [ValidateSet('PublicRead', 'AuthenticatedRead', 'AdminRead', 'MediaRead', 'SignalRConnectivity', 'SignedWebhook', 'Commerce', 'Resilience')]
    [string]$Suite = 'PublicRead',

    [ValidateSet('Smoke', 'Baseline', 'Peak', 'Soak')]
    [string]$Profile = 'Smoke',

    [string]$BaseUrl = 'http://localhost:5080',

    [ValidateRange(1, 10000)]
    [int]$VirtualUsers = 100,

    [ValidateRange(1, 1000000)]
    [int]$Iterations = 1,

    [string]$Duration,

    [string]$CandidateIdentity = 'local-untracked',

    [string]$ResultsDirectory,

    [switch]$AllowNonLocalTarget,

    [switch]$InsecureSkipTlsVerify,

    [switch]$CaptureRawSamples
)

$ErrorActionPreference = 'Stop'

function Test-LocalTarget([Uri]$Target) {
    return $Target.Host -in @('localhost', '127.0.0.1', '::1')
}

function Set-ProcessEnvironmentValue([hashtable]$PreviousValues, [string]$Name, [string]$Value) {
    if (-not $PreviousValues.ContainsKey($Name)) {
        $PreviousValues[$Name] = [Environment]::GetEnvironmentVariable($Name, 'Process')
    }

    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
}

function Restore-ProcessEnvironmentValues([hashtable]$PreviousValues) {
    foreach ($name in $PreviousValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $PreviousValues[$name], 'Process')
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$k6Command = Get-Command k6 -ErrorAction SilentlyContinue
if ($null -eq $k6Command) {
    throw 'k6 was not found on PATH. Install Grafana k6, then rerun this command. See docs/performance/prh-016-load-resilience-runbook.md.'
}

try {
    $target = [Uri]$BaseUrl
}
catch {
    throw "BaseUrl '$BaseUrl' is not a valid absolute URI."
}

if (-not $target.IsAbsoluteUri -or $target.Scheme -notin @('http', 'https') -or
    -not [string]::IsNullOrWhiteSpace($target.UserInfo) -or
    -not [string]::IsNullOrWhiteSpace($target.Query) -or
    -not [string]::IsNullOrWhiteSpace($target.Fragment) -or
    $target.AbsolutePath -ne '/') {
    throw 'BaseUrl must be a plain http(s) origin without credentials, path, query string, or fragment.'
}

$isLocalTarget = Test-LocalTarget $target
if (-not $isLocalTarget -and -not $AllowNonLocalTarget) {
    throw 'Refusing a non-local target. Use -AllowNonLocalTarget only for an approved isolated environment.'
}

if (-not $isLocalTarget -and $CandidateIdentity -notmatch '^.+@sha256:[A-Fa-f0-9]{64}$') {
    throw 'A non-local run requires -CandidateIdentity in immutable image@sha256:<64-hex> form.'
}

if ($Suite -ne 'PublicRead' -and $PSBoundParameters.ContainsKey('Profile')) {
    throw '-Profile is only valid for the PublicRead suite.'
}

$effectiveVirtualUsers = if ($PSBoundParameters.ContainsKey('VirtualUsers')) {
    $VirtualUsers
}
else {
    switch ($Suite) {
        'AuthenticatedRead' { 1 }
        'AdminRead' { 1 }
        'MediaRead' { 1 }
        'SignalRConnectivity' { 1 }
        'SignedWebhook' { 1 }
        'Commerce' { 1 }
        'Resilience' { 10 }
        default { $VirtualUsers }
    }
}
$suiteMaximumVirtualUsers = @{
    PublicRead = 10000
    AuthenticatedRead = 50
    AdminRead = 50
    MediaRead = 100
    SignalRConnectivity = 25
    SignedWebhook = 5
    Commerce = 10
    Resilience = 10000
}
if ($effectiveVirtualUsers -gt $suiteMaximumVirtualUsers[$Suite]) {
    throw "-VirtualUsers must not exceed $($suiteMaximumVirtualUsers[$Suite]) for the $Suite suite."
}

$isolatedStagingSuites = @('AdminRead', 'MediaRead', 'SignalRConnectivity', 'SignedWebhook')
if ($Suite -in $isolatedStagingSuites) {
    $testEnvironment = [Environment]::GetEnvironmentVariable('K6_TEST_ENVIRONMENT', 'Process')
    if (-not [string]::Equals($testEnvironment, 'isolated-staging', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Suite requires K6_TEST_ENVIRONMENT=isolated-staging."
    }
}

# JSON sample output can contain request URLs and bodies. Keep it out of every suite
# that sends a token, a signed callback, or synthetic customer data.
if ($CaptureRawSamples -and $Suite -notin @('PublicRead', 'Resilience')) {
    throw '-CaptureRawSamples is limited to PublicRead and Resilience; secret-bearing and write-capable suites never emit raw request samples.'
}

$scriptName = switch ($Suite) {
    'PublicRead' { 'prh-016-storefront.js' }
    'AuthenticatedRead' { 'prh-016-authenticated-read.js' }
    'AdminRead' { 'prh-016-admin-read.js' }
    'MediaRead' { 'prh-016-media-read.js' }
    'SignalRConnectivity' { 'prh-016-signalr-connectivity.js' }
    'SignedWebhook' { 'prh-016-signed-webhook.js' }
    'Commerce' { 'prh-016-commerce.js' }
    'Resilience' { 'prh-016-resilience.js' }
}
$scriptPath = Join-Path $PSScriptRoot "k6\$scriptName"
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "The k6 suite script '$scriptPath' is missing."
}

$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
$outputDirectory = if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    Join-Path $repositoryRoot "artifacts\performance\prh-016-$($Suite.ToLowerInvariant())-$timestamp"
}
else {
    [IO.Path]::GetFullPath($ResultsDirectory)
}
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$gitCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($gitCommit)) {
    $gitCommit = 'unavailable'
}

$effectiveCandidateIdentity = if ($CandidateIdentity -eq 'local-untracked') {
    "local-$gitCommit"
}
else {
    $CandidateIdentity
}

$metadataPath = Join-Path $outputDirectory 'run-metadata.json'
$summaryPath = Join-Path $outputDirectory 'k6-summary.json'
$rawSamplesPath = Join-Path $outputDirectory 'k6-samples.json'
$metadata = [ordered]@{
    startedAtUtc      = [DateTimeOffset]::UtcNow.ToString('O')
    suite             = $Suite
    profile           = if ($Suite -eq 'PublicRead') { $Profile } else { $null }
    targetOrigin      = $target.GetLeftPart([UriPartial]::Authority)
    candidateIdentity = $effectiveCandidateIdentity
    gitCommit         = $gitCommit
    virtualUsers      = $effectiveVirtualUsers
    iterations        = if ($Suite -in @('Commerce', 'SignedWebhook')) { $Iterations } else { $null }
    duration          = $Duration
    rawSamples        = [bool]$CaptureRawSamples
    note              = 'Credentials and other K6_* secret environment values are intentionally excluded.'
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding utf8

$previousEnvironmentValues = @{}
try {
    Set-ProcessEnvironmentValue $previousEnvironmentValues 'BASE_URL' $target.GetLeftPart([UriPartial]::Authority)
    Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_ALLOW_NONLOCAL_TARGET' ([string](-not $isLocalTarget))

    switch ($Suite) {
        'PublicRead' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_PROFILE' $Profile.ToLowerInvariant()
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_PEAK_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_SOAK_VUS' ([string]$effectiveVirtualUsers)
        }
        'AuthenticatedRead' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_AUTH_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_AUTH_DURATION' $(if ([string]::IsNullOrWhiteSpace($Duration)) { '1m' } else { $Duration })
        }
        'AdminRead' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_ADMIN_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_ADMIN_DURATION' $(if ([string]::IsNullOrWhiteSpace($Duration)) { '1m' } else { $Duration })
        }
        'MediaRead' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_MEDIA_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_MEDIA_DURATION' $(if ([string]::IsNullOrWhiteSpace($Duration)) { '1m' } else { $Duration })
        }
        'SignalRConnectivity' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_SIGNALR_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_SIGNALR_DURATION' $(if ([string]::IsNullOrWhiteSpace($Duration)) { '1m' } else { $Duration })
        }
        'SignedWebhook' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_WEBHOOK_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_WEBHOOK_ITERATIONS' ([string]$Iterations)
        }
        'Commerce' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_COMMERCE_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_COMMERCE_ITERATIONS' ([string]$Iterations)
        }
        'Resilience' {
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_RESILIENCE_VUS' ([string]$effectiveVirtualUsers)
            Set-ProcessEnvironmentValue $previousEnvironmentValues 'K6_RESILIENCE_DURATION' $(if ([string]::IsNullOrWhiteSpace($Duration)) { '15m' } else { $Duration })
        }
    }

    $k6Arguments = @(
        'run',
        '--summary-export', $summaryPath,
        '--tag', "candidate=$effectiveCandidateIdentity",
        '--tag', "suite=$Suite"
    )
    if ($InsecureSkipTlsVerify) {
        $k6Arguments += '--insecure-skip-tls-verify'
    }
    if ($CaptureRawSamples) {
        $k6Arguments += @('--out', "json=$rawSamplesPath")
    }
    $k6Arguments += $scriptPath

    Write-Host "Running PRH-016 $Suite suite against $($target.GetLeftPart([UriPartial]::Authority))."
    & $k6Command.Source @k6Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "k6 returned exit code $LASTEXITCODE. Review '$outputDirectory'."
    }

    Write-Host "PRH-016 k6 evidence written to '$outputDirectory'."
}
finally {
    Restore-ProcessEnvironmentValues $previousEnvironmentValues
}
