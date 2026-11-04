[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$composePath = Join-Path $repositoryRoot 'docker-compose.yml'
$examplePath = Join-Path $repositoryRoot '.env.example'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('shipping-compose-' + [Guid]::NewGuid().ToString('N'))
$previousEnvironment = @{}

function Assert-ShippingConfiguration {
    param([string]$Name, [hashtable]$Expected, [string]$BaseEnvironment)

    $environmentPath = Join-Path $temporaryDirectory "$Name.env"
    $environmentText = $BaseEnvironment
    foreach ($entry in $Expected.GetEnumerator()) {
        if ($Name -ne 'defaults') { $environmentText += "`n$($entry.Key)=$($entry.Value)" }
    }
    [IO.File]::WriteAllText($environmentPath, $environmentText)
    # Only render configuration. Never build/start containers or read local .env.
    $rendered = & docker compose --project-directory $repositoryRoot --env-file $environmentPath --file $composePath --profile tools --profile storage config --format json 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Compose configuration rendering failed for '$Name'." }
    $configuration = ($rendered -join "`n") | ConvertFrom-Json
    foreach ($entry in $Expected.GetEnumerator()) {
        $actual = $configuration.services.api.environment.PSObject.Properties[$entry.Key]
        if ($null -eq $actual -or [string]$actual.Value -cne [string]$entry.Value) {
            throw "Shipping Compose check '$Name' failed for $($entry.Key): expected mapping/value was not applied."
        }
    }
    Write-Output "Shipping Compose '$Name' passed ($($Expected.Count) settings)."
}

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    # Ambient values take precedence over --env-file. Remove them only in this
    # process so neither local credentials nor local flags enter test rendering.
    $composeText = [IO.File]::ReadAllText($composePath)
    $names = @([regex]::Matches($composeText, '\$\{([A-Za-z_][A-Za-z0-9_]*)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
    foreach ($name in $names) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    $baseEnvironment = ((Get-Content -LiteralPath $examplePath | Where-Object { $_ -notmatch '^ShippingCheckout__' }) -join "`n")
    $defaults = @{
        ShippingCheckout__Enabled = 'false'
        ShippingCheckout__WorkspaceEnabled = 'true'
        ShippingCheckout__ExternalEnabled = 'true'
        ShippingCheckout__QuoteLifetimeMinutes = '10'
        ShippingCheckout__UnusedQuoteRetentionDays = '30'
        ShippingCheckout__ExternalQuoteTimeoutSeconds = '12'
    }
    Assert-ShippingConfiguration 'defaults' $defaults $baseEnvironment
    $localOnly = $defaults.Clone()
    $localOnly['ShippingCheckout__Enabled'] = 'true'
    $localOnly['ShippingCheckout__ExternalEnabled'] = 'false'
    $localOnly['ShippingCheckout__QuoteLifetimeMinutes'] = '7'
    $localOnly['ShippingCheckout__UnusedQuoteRetentionDays'] = '14'
    $localOnly['ShippingCheckout__ExternalQuoteTimeoutSeconds'] = '5'
    Assert-ShippingConfiguration 'local-only' $localOnly $baseEnvironment
    $paused = $localOnly.Clone()
    $paused['ShippingCheckout__WorkspaceEnabled'] = 'false'
    Assert-ShippingConfiguration 'pause-both-methods' $paused $baseEnvironment
}
finally {
    foreach ($entry in $previousEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    # Delete only files created by this invocation in its unique temporary folder.
    foreach ($name in @('defaults.env', 'local-only.env', 'pause-both-methods.env')) {
        $path = Join-Path $temporaryDirectory $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    if (Test-Path -LiteralPath $temporaryDirectory) { Remove-Item -LiteralPath $temporaryDirectory }
}
