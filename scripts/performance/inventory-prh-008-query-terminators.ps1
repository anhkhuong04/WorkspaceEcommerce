[CmdletBinding()]
param(
    [string]$SourceRoot = "src",
    [string]$OutputPath = "artifacts/performance/prh-008-query-terminal-inventory.md"
)

$ErrorActionPreference = 'Stop'
$pattern = '\.(ToArray|ToList|FirstOrDefault|SingleOrDefault|Single|Count|Any)\(\)|\.(ToArrayAsync|ToListAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|SingleAsync|CountAsync|AnyAsync)\('
$rootPath = Resolve-Path $SourceRoot
$rows = Get-ChildItem -Path $rootPath -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring((Get-Location).Path.Length + 1).Replace('\', '/')
        Select-String -Path $_.FullName -Pattern $pattern | ForEach-Object {
            [PSCustomObject]@{
                File = $relativePath
                Line = $_.LineNumber
                Code = $_.Line.Trim()
            }
        }
    } |
    Sort-Object File, Line

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }

@(
    '# PRH-008 IQueryable terminal inventory',
    '',
    "Generated at: $(Get-Date -Format o)",
    "Source root: $rootPath",
    "Terminal operations found: $($rows.Count)",
    '',
    '| File | Line | Terminal expression |',
    '| --- | ---: | --- |'
) + ($rows | ForEach-Object {
    "| ``$($_.File)`` | $($_.Line) | ``$($_.Code.Replace('|', '\|'))`` |"
}) | Set-Content -Path $OutputPath -Encoding utf8

Write-Host "Wrote $($rows.Count) terminal operations to $OutputPath"
