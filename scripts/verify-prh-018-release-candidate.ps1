[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceManifestPath,

    [switch]$AllowDirtyWorktree
)

$ErrorActionPreference = 'Stop'

function Get-RequiredString([object]$Value, [string]$Name) {
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Release manifest field '$Name' is required."
    }

    return $text.Trim()
}

function Get-RequiredEvidence([object]$Value, [string]$Name) {
    $evidence = Get-RequiredString $Value $Name
    if ($evidence -match '^(?i:pending|todo|tbd)$' -or $evidence -match '^<.*>$') {
        throw "Release evidence field '$Name' must reference retained evidence, not a placeholder."
    }

    return $evidence
}

function Get-RequiredRemediationDate([object]$Value, [string]$Name) {
    $dateText = Get-RequiredString $Value $Name
    $parsedDate = [DateTime]::MinValue
    if (-not [DateTime]::TryParseExact(
            $dateText,
            'yyyy-MM-dd',
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::None,
            [ref]$parsedDate)) {
        throw "Release finding field '$Name' must use ISO date format yyyy-MM-dd."
    }

    if ($parsedDate.Date -lt [DateTime]::UtcNow.Date) {
        throw "Release finding field '$Name' must not be in the past."
    }

    return $dateText
}

function Get-ManifestGate([object[]]$Gates, [string]$Id) {
    $matches = @($Gates | Where-Object { [string]$_.id -eq $Id })
    if ($matches.Count -ne 1) {
        throw "Release manifest must contain exactly one '$Id' gate."
    }

    return $matches[0]
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestFile = Resolve-Path -LiteralPath $EvidenceManifestPath -ErrorAction Stop

try {
    $manifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding utf8 | ConvertFrom-Json -ErrorAction Stop
}
catch {
    throw "Release manifest '$manifestFile' is not valid JSON: $($_.Exception.Message)"
}

$candidateCommit = Get-RequiredString $manifest.candidateCommit 'candidateCommit'
$imageReference = Get-RequiredString $manifest.imageReference 'imageReference'

if ($candidateCommit -notmatch '^[A-Fa-f0-9]{40}$') {
    throw 'candidateCommit must be the full 40-character Git commit SHA.'
}

if ($imageReference -notmatch '^.+@sha256:[A-Fa-f0-9]{64}$') {
    throw 'imageReference must be a fully-qualified immutable image@sha256:<64-hex-digest> reference.'
}

$currentCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not resolve the checked-out Git commit.'
}

$resolvedCandidateCommit = (& git -C $repositoryRoot rev-parse $candidateCommit).Trim()
if ($LASTEXITCODE -ne 0 -or $resolvedCandidateCommit -ne $currentCommit) {
    throw "candidateCommit '$candidateCommit' must resolve to the checked-out commit '$currentCommit'."
}

if (-not $AllowDirtyWorktree) {
    $worktreeChanges = @(& git -C $repositoryRoot status --porcelain)
    if ($worktreeChanges.Count -gt 0) {
        throw 'The release-candidate check requires a clean worktree. Commit or remove unrelated changes first.'
    }
}

$requiredGates = @(
    'backend-ci',
    'frontend-ci',
    'container-ci',
    'migrations',
    'dependency-and-secret-security',
    'browser-e2e',
    'api-contract-and-authorization',
    'two-replica-topology',
    'load-and-resilience',
    'backup-and-recovery',
    'telemetry-and-alerts',
    'configuration-and-rotation'
)
$gates = @($manifest.gates)
if ($gates.Count -eq 0) {
    throw 'Release manifest must contain a non-empty gates array.'
}

foreach ($requiredGate in $requiredGates) {
    $gate = Get-ManifestGate $gates $requiredGate
    $status = Get-RequiredString $gate.status "gates[$requiredGate].status"
    $evidence = Get-RequiredEvidence $gate.evidence "gates[$requiredGate].evidence"

    if ($status -ne 'Passed') {
        throw "Release gate '$requiredGate' is '$status', not Passed. Evidence: $evidence"
    }
}

$findings = if ($null -eq $manifest.findings) { @() } else { @($manifest.findings) }
foreach ($finding in $findings) {
    $severity = Get-RequiredString $finding.severity 'findings[].severity'
    $status = Get-RequiredString $finding.status 'findings[].status'
    $id = Get-RequiredString $finding.id 'findings[].id'

    if ($severity -notin @('Critical', 'High', 'Medium', 'Low', 'Info')) {
        throw "Finding '$id' has unsupported severity '$severity'."
    }

    if ($status -notin @('Resolved', 'AcceptedRisk')) {
        if ($severity -in @('Critical', 'High')) {
            throw "Release-blocking finding '$id' is '$status'."
        }

        continue
    }

    if ($status -eq 'AcceptedRisk') {
        if ($severity -eq 'Critical') {
            throw "Critical finding '$id' cannot be accepted for an initial production release."
        }

        Get-RequiredString $finding.owner "findings[$id].owner" | Out-Null
        Get-RequiredRemediationDate $finding.remediationDate "findings[$id].remediationDate" | Out-Null
        Get-RequiredString $finding.justification "findings[$id].justification" | Out-Null
    }

    if ($severity -in @('Critical', 'High')) {
        Get-RequiredEvidence $finding.evidence "findings[$id].evidence" | Out-Null
    }
}

Write-Host "PRH-018 release-candidate manifest passed for commit $currentCommit and image $imageReference."
