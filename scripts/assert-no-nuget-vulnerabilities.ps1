[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ReportPath)) {
    throw "NuGet vulnerability report '$ReportPath' does not exist."
}

# `dotnet list package --vulnerable --format json` exits successfully even when it
# reports vulnerabilities. A severity property only occurs for an actual finding.
$findings = Select-String -LiteralPath $ReportPath -Pattern '"severity"\s*:'
if ($findings.Count -gt 0) {
    throw "NuGet vulnerability audit found $($findings.Count) vulnerable package entry/entries. See '$ReportPath'."
}

Write-Output 'NuGet vulnerability audit passed: no vulnerable package entries reported.'
