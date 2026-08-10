[CmdletBinding()]
param(
    [int]$PostgresPort = 55432
)

$ErrorActionPreference = 'Stop'
$containerName = "prh009-migrations-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
$database = 'workspace_ecommerce_prh009'
$username = 'workspace_ecommerce'
$password = 'workspace_ecommerce_prh009'
$connectionString = "Host=localhost;Port=$PostgresPort;Database=$database;Username=$username;Password=$password"
$shipmentMigration = '20260802034719_AddShipmentIntegration'
$latestMigration = '20260809151744_OptimizeReadPathIndexes'

function Wait-ForPostgres {
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec $containerName pg_isready -U $username -d $database | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL container '$containerName' did not become ready."
}

function Invoke-MigrationUpdate([string]$TargetMigration) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:AdminAuth__Password = 'prh009-local-admin-password'
    $env:Jwt__SigningKey = 'prh009-local-signing-key-at-least-32-bytes'

    $arguments = @(
        'database', 'update',
        '--project', 'src\WorkspaceEcommerce.Infrastructure',
        '--startup-project', 'src\WorkspaceEcommerce.Api',
        '--context', 'AppDbContext',
        '--configuration', 'Release',
        '--no-build')
    if (-not [string]::IsNullOrWhiteSpace($TargetMigration)) {
        $arguments += $TargetMigration
    }

    & dotnet tool run dotnet-ef @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Migration update to '$TargetMigration' failed."
    }
}

try {
    docker run --rm -d --name $containerName `
        -e "POSTGRES_DB=$database" `
        -e "POSTGRES_USER=$username" `
        -e "POSTGRES_PASSWORD=$password" `
        -p "${PostgresPort}:5432" postgres:17-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not start temporary PostgreSQL container '$containerName'."
    }

    Wait-ForPostgres

    # Clean database creation through every migration.
    Invoke-MigrationUpdate ''
    $emptyDatabaseVersion = [string]('SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";' |
        docker exec -i $containerName psql -U $username -d $database -tA)
    if ($emptyDatabaseVersion.Trim() -ne $latestMigration) {
        throw "Empty-database migration verification ended at '$emptyDatabaseVersion', expected '$latestMigration'."
    }

    # Recreate the database, stop at the latest existing shipment schema, then upgrade it.
    docker exec $containerName psql -U $username -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE $database WITH (FORCE);" | Out-Null
    docker exec $containerName psql -U $username -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $database OWNER $username;" | Out-Null
    Invoke-MigrationUpdate $shipmentMigration
    Invoke-MigrationUpdate ''
    $upgradedDatabaseVersion = [string]('SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";' |
        docker exec -i $containerName psql -U $username -d $database -tA)
    if ($upgradedDatabaseVersion.Trim() -ne $latestMigration) {
        throw "Shipment-schema upgrade verification ended at '$upgradedDatabaseVersion', expected '$latestMigration'."
    }

    Write-Host "PRH-009 migration verification passed: clean create and upgrade from $shipmentMigration to $latestMigration."
}
finally {
    docker rm -f $containerName 2>$null | Out-Null
}
