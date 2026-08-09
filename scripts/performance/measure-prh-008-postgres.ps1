[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [string]$OutputDirectory = "artifacts/performance",

    [string]$PsqlContainer,

    [switch]$Analyze
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PsqlContainer) -and -not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw 'psql is required. Install PostgreSQL client tools or pass -PsqlContainer for a running PostgreSQL Docker container.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportPath = Join-Path $OutputDirectory "prh-008-postgres-$timestamp.md"
$explainOptions = if ($Analyze) { 'ANALYZE, BUFFERS, FORMAT TEXT' } else { 'COSTS, VERBOSE, FORMAT TEXT' }

function Invoke-Psql([string]$Sql, [string[]]$Arguments) {
    if ([string]::IsNullOrWhiteSpace($PsqlContainer)) {
        return $Sql | & psql $ConnectionString @Arguments
    }

    return $Sql | & docker exec -i $PsqlContainer psql $ConnectionString @Arguments
}

function Invoke-ScalarSql([string]$Sql) {
    $value = Invoke-Psql $Sql @('--tuples-only', '--no-align', '--quiet')
    if ($LASTEXITCODE -ne 0) { throw "psql failed while running scalar query: $Sql" }
    return ($value | Select-Object -First 1).Trim()
}

function Invoke-Explain([string]$Name, [string]$Sql) {
    $startedAt = Get-Date
    $plan = Invoke-Psql "EXPLAIN ($explainOptions) $Sql;" @('--tuples-only', '--no-align')
    if ($LASTEXITCODE -ne 0) { throw "psql failed while explaining $Name" }
    $elapsed = ((Get-Date) - $startedAt).TotalMilliseconds
    @(
        "## $Name",
        '',
        "- Client elapsed milliseconds: $([Math]::Round($elapsed, 2))",
        "- Query: ``$Sql``",
        '',
        '```text',
        $plan,
        '```',
        ''
    )
}

$customerId = Invoke-ScalarSql 'SELECT customer_id FROM ordering.orders WHERE customer_id IS NOT NULL GROUP BY customer_id ORDER BY count(*) DESC, customer_id LIMIT 1;'
$productId = Invoke-ScalarSql 'SELECT id FROM catalog.products ORDER BY id LIMIT 1;'

if ([string]::IsNullOrWhiteSpace($customerId) -or [string]::IsNullOrWhiteSpace($productId)) {
    throw 'Representative data is required: seed at least one customer and one product before running this script.'
}

$sections = @(
    '# PRH-008 PostgreSQL query-plan evidence',
    '',
    "Generated at: $(Get-Date -Format o)",
    "EXPLAIN mode: $explainOptions",
    '',
    (Invoke-Explain 'Customer order page' "SELECT o.id, o.order_code, o.total_amount, (SELECT count(*) FROM ordering.order_items i WHERE i.order_id = o.id) AS item_count FROM ordering.orders o WHERE o.customer_id = '$customerId' ORDER BY o.created_at DESC, o.order_code DESC LIMIT 20"),
    (Invoke-Explain 'Admin order page by status' "SELECT o.id, o.order_code, o.customer_name, (SELECT count(*) FROM ordering.order_items i WHERE i.order_id = o.id) AS item_count FROM ordering.orders o WHERE o.status = 'Pending' ORDER BY o.created_at DESC, o.order_code DESC LIMIT 20"),
    (Invoke-Explain 'Product review page' "SELECT r.id, r.rating, r.created_at FROM catalog.reviews r WHERE r.product_id = '$productId' ORDER BY r.created_at DESC, r.id DESC LIMIT 20"),
    (Invoke-Explain 'Active coupon page' "SELECT c.id, c.code, c.created_at FROM promotions.coupons c WHERE c.is_active = TRUE ORDER BY c.created_at DESC, c.code ASC LIMIT 20")
)

$sections | Set-Content -Path $reportPath -Encoding utf8
Write-Host "Wrote PostgreSQL plan evidence to $reportPath"
