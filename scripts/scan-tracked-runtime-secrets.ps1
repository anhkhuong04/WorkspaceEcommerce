[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    # Windows PowerShell 5.1 does not populate $PSScriptRoot early enough for
    # a parameter default expression. Resolve it after parameter binding so the
    # scanner remains runnable in both Windows PowerShell and PowerShell 7.
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Test-PlaceholderOrExternalReference {
    param([string]$Value)

    $normalized = $Value.Trim().Trim('"', "'")

    return [string]::IsNullOrWhiteSpace($normalized) -or
        $normalized -match '(?i)CHANGE|YOUR|PLACEHOLDER|EXAMPLE|<[^>]+>|\$\{[^}]+\}|^\*+$'
}

function Add-Finding {
    param(
        [System.Collections.Generic.List[object]]$Findings,
        [string]$Path,
        [int]$Line,
        [string]$Rule)

    $Findings.Add([pscustomobject]@{
        Path = $Path
        Line = $Line
        Rule = $Rule
    })
}

Push-Location $RepositoryRoot

try {
    $trackedFiles = @(git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate tracked files with Git."
    }

    $findings = [System.Collections.Generic.List[object]]::new()
    $sensitiveKey = '(?:password|pwd|signingkey|hashsecret|apikey|webhooksecret|clientsecret|privatekey|connectionstring)'
    $structuredPattern = '(?i)["''](?<key>[^"'']*{0}[^"'']*)["'']\s*[:=]\s*["''](?<value>[^"'']+)["'']' -f $sensitiveKey
    $environmentPattern = "(?i)^(?<key>[A-Z0-9_]*$sensitiveKey[A-Z0-9_]*)\s*=\s*(?<value>.+)$"
    $composePattern = "(?i)^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*$sensitiveKey[A-Za-z0-9_]*)\s*:\s*(?<value>.+)$"

    foreach ($path in $trackedFiles) {
        if ($path -like "tests/*" -or
            $path -like "docs/*" -or
            $path -notmatch '^(src/|docker-compose\.yml$|\.env\.example$)') {
            continue
        }

        $extension = [IO.Path]::GetExtension($path).ToLowerInvariant()
        if ($extension -notin ".cs", ".json", ".yml", ".yaml", ".example") {
            continue
        }

        $absolutePath = Join-Path $RepositoryRoot $path
        $bytes = [IO.File]::ReadAllBytes($absolutePath)
        if ($bytes -contains 0) {
            continue
        }

        $lineNumber = 0
        foreach ($line in [Text.Encoding]::UTF8.GetString($bytes) -split "`r?`n") {
            $lineNumber++

            $connectionStringMatch = [regex]::Match(
                $line,
                '(?i)(?:password|pwd)=(?<value>[^;\r\n]+)')
            if ($connectionStringMatch.Success -and
                -not (Test-PlaceholderOrExternalReference $connectionStringMatch.Groups['value'].Value)) {
                Add-Finding $findings $path $lineNumber "embedded connection-string password"
            }

            $structuredMatch = [regex]::Match(
                $line,
                $structuredPattern)
            if ($structuredMatch.Success -and
                -not (Test-PlaceholderOrExternalReference $structuredMatch.Groups['value'].Value)) {
                Add-Finding $findings $path $lineNumber "embedded sensitive configuration value"
            }

            $environmentMatch = [regex]::Match(
                $line,
                $environmentPattern)
            if ($environmentMatch.Success -and
                -not (Test-PlaceholderOrExternalReference $environmentMatch.Groups['value'].Value)) {
                Add-Finding $findings $path $lineNumber "embedded sensitive environment value"
            }

            $composeMatch = [regex]::Match(
                $line,
                $composePattern)
            if ($composeMatch.Success -and
                -not (Test-PlaceholderOrExternalReference $composeMatch.Groups['value'].Value)) {
                Add-Finding $findings $path $lineNumber "embedded sensitive Compose value"
            }
        }
    }

    $uniqueFindings = @($findings | Sort-Object Path, Line, Rule -Unique)
    if ($uniqueFindings.Count -gt 0) {
        [Console]::Error.WriteLine(
            "Tracked runtime secret scan found $($uniqueFindings.Count) high-confidence finding(s). Values are intentionally omitted.")
        foreach ($finding in $uniqueFindings) {
            [Console]::Error.WriteLine("$($finding.Path):$($finding.Line) [$($finding.Rule)]")
        }

        exit 1
    }

    Write-Output "Tracked runtime secret scan passed. Scanned $($trackedFiles.Count) tracked files; values were not emitted."
}
finally {
    Pop-Location
}
