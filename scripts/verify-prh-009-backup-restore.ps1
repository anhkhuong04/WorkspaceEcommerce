[CmdletBinding()]
param(
    [int]$PostgresPort = 55433,
    [switch]$KeepBackup
)

$ErrorActionPreference = 'Stop'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$sourceContainer = "prh009-backup-source-$suffix"
$restoreContainer = "prh009-backup-restore-$suffix"
$database = 'workspace_ecommerce_prh009_backup'
$username = 'workspace_ecommerce'
$password = 'workspace_ecommerce_prh009_backup'
$connectionString = "Host=localhost;Port=$PostgresPort;Database=$database;Username=$username;Password=$password"
$backupDirectory = Join-Path $PSScriptRoot '..\.tmp'
$backupPath = Join-Path $backupDirectory "prh009-postgres-$suffix.sql"
$mediaAssetId = '99999999-9999-9999-9999-999999999999'

function Wait-ForPostgres([string]$ContainerName) {
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec $ContainerName pg_isready -U $username -d $database | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL container '$ContainerName' did not become ready."
}

function Invoke-MigrationUpdate {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:AdminAuth__Password = 'prh009-local-admin-password'
    $env:Jwt__SigningKey = 'prh009-local-signing-key-at-least-32-bytes'

    & dotnet ef database update `
        --project 'src\WorkspaceEcommerce.Infrastructure' `
        --startup-project 'src\WorkspaceEcommerce.Api' `
        --context AppDbContext `
        --no-build
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not migrate the temporary backup source database.'
    }
}

try {
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    foreach ($containerName in @($sourceContainer, $restoreContainer)) {
        docker run --rm -d --name $containerName `
            -e "POSTGRES_DB=$database" `
            -e "POSTGRES_USER=$username" `
            -e "POSTGRES_PASSWORD=$password" `
            $(if ($containerName -eq $sourceContainer) { @('-p', "${PostgresPort}:5432") }) `
            postgres:17-alpine | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not start temporary PostgreSQL container '$containerName'."
        }
    }
    Wait-ForPostgres $sourceContainer
    Wait-ForPostgres $restoreContainer
    Invoke-MigrationUpdate

    $mediaInsert = @"
INSERT INTO content.media_assets
    ("Id", folder, object_key, public_url, content_type, checksum, size, width, height, frame_count, state, created_at, available_at)
VALUES
    ('$mediaAssetId', 'products', 'products/prh009/restore-sentinel.jpg', 'https://media.example.test/products/prh009/restore-sentinel.jpg', 'image/jpeg', 'prh009-checksum', 123, 1, 1, 1, 'Available', NOW(), NOW());
"@
    $mediaInsert | docker exec -i $sourceContainer psql -U $username -d $database -v ON_ERROR_STOP=1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not insert the media metadata restore sentinel.'
    }

    docker exec $sourceContainer pg_dump -U $username -d $database --clean --if-exists --no-owner --no-privileges |
        Set-Content -LiteralPath $backupPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create the PostgreSQL backup.'
    }

    Get-Content -LiteralPath $backupPath -Raw |
        docker exec -i $restoreContainer psql -U $username -d $database -v ON_ERROR_STOP=1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restore the PostgreSQL backup.'
    }

    $restoredCount = [string]("SELECT COUNT(*) FROM content.media_assets WHERE `"Id`" = '$mediaAssetId';" |
        docker exec -i $restoreContainer psql -U $username -d $database -tA)
    if ($restoredCount.Trim() -ne '1') {
        throw 'The restored database does not contain the durable-media metadata sentinel.'
    }

    Write-Host "PRH-009 backup/restore verification passed. PostgreSQL schema and media metadata restored from '$backupPath'."
    if ($KeepBackup) {
        Write-Host "Backup retained at '$backupPath'. It contains test-only data."
    }
}
finally {
    docker rm -f $sourceContainer 2>$null | Out-Null
    docker rm -f $restoreContainer 2>$null | Out-Null
    if ((-not $KeepBackup) -and (Test-Path -LiteralPath $backupPath)) {
        Remove-Item -LiteralPath $backupPath -Force
    }
}
