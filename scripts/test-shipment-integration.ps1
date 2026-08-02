param(
    [string]$ApiBaseUrl = "http://localhost:5080",
    [string]$ProviderBaseUrl = "http://localhost:5221/api/v1/partner",
    [Parameter(Mandatory = $true)]
    [string]$ProviderApiKey,
    [Parameter(Mandatory = $true)]
    [string]$WebhookSecret,
    [string]$SessionId = "",
    [string]$CustomerPhone = "0900000000"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SessionId)) {
    $SessionId = "shipment-smoke-$([guid]::NewGuid().ToString('N'))"
}

function Get-ApiData {
    param([object]$Response)

    if (-not $Response.success) {
        $message = ($Response.errors -join "; ")
        throw "E-commerce API request failed: $message"
    }

    return $Response.data
}

$productsResponse = Invoke-RestMethod `
    -Method Get `
    -Uri "$ApiBaseUrl/api/products?pageNumber=1&pageSize=10&inStock=true"
$products = Get-ApiData $productsResponse
$product = $products.items | Select-Object -First 1
if ($null -eq $product) {
    throw "No in-stock product is available for the smoke-test cart."
}

$productResponse = Invoke-RestMethod `
    -Method Get `
    -Uri "$ApiBaseUrl/api/products/$([uri]::EscapeDataString($product.slug))"
$productDetail = Get-ApiData $productResponse
$variant = $productDetail.variants | Where-Object { $_.stockQuantity -gt 0 } | Select-Object -First 1
if ($null -eq $variant) {
    throw "No in-stock product variant is available for the smoke-test cart."
}

$cartBody = @{
    sessionId = $SessionId
    productVariantId = $variant.id
    quantity = 1
} | ConvertTo-Json
$cartResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/cart/items" `
    -ContentType "application/json" `
    -Body $cartBody
$cart = Get-ApiData $cartResponse
if ($cart.totalQuantity -lt 1) {
    throw "Smoke-test cart could not be prepared."
}

$quoteBody = @{
    sessionId = $SessionId
    street = "9 Le Loi"
    ward = "Ben Nghe"
    province = "Ho Chi Minh"
} | ConvertTo-Json

$quoteResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/checkout/shipping-quote" `
    -ContentType "application/json" `
    -Body $quoteBody
$quote = Get-ApiData $quoteResponse
if ($quote.totalFeeAmount -lt 0) {
    throw "Shipping quote returned an invalid total fee."
}

$checkoutBody = @{
    sessionId = $SessionId
    customerName = "Shipment Smoke Test"
    customerPhone = $CustomerPhone
    customerEmail = "shipment-smoke@example.com"
    shippingAddress = "9 Le Loi, Ben Nghe, Ho Chi Minh"
    shippingStreet = "9 Le Loi"
    shippingWard = "Ben Nghe"
    shippingProvince = "Ho Chi Minh"
    note = "Automated shipment integration smoke test"
    paymentMethod = 0
} | ConvertTo-Json

$checkoutResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/checkout" `
    -ContentType "application/json" `
    -Body $checkoutBody
$checkout = Get-ApiData $checkoutResponse
$order = $checkout.order
if ([string]::IsNullOrWhiteSpace($order.trackingCode)) {
    throw "Checkout completed without a tracking code. Check the provider configuration and shipment outbox."
}

$trackingCode = $order.trackingCode
$providerHeaders = @{ Authorization = "Bearer $ProviderApiKey" }
$providerTracking = Invoke-RestMethod `
    -Method Get `
    -Uri "$ProviderBaseUrl/shipments/$([uri]::EscapeDataString($trackingCode))" `
    -Headers $providerHeaders
if ($providerTracking.externalOrderId -ne $order.orderCode) {
    throw "Provider tracking response is mapped to a different external order."
}

$eventId = [guid]::NewGuid()
$changedAt = [DateTimeOffset]::UtcNow
$webhookPayload = @{
    eventId = $eventId
    event = "shipment.status_changed"
    trackingCode = $trackingCode
    externalOrderId = $order.orderCode
    status = "Delivered"
    changedAtUtc = $changedAt.ToString("O")
} | ConvertTo-Json -Compress
$timestamp = [DateTimeOffset]::UtcNow.ToString("O")
$hmac = [System.Security.Cryptography.HMACSHA256]::new(
    [System.Text.Encoding]::UTF8.GetBytes($WebhookSecret))
try {
    $signatureBytes = $hmac.ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes("$timestamp.$webhookPayload"))
}
finally {
    $hmac.Dispose()
}
$signatureHex = -join ($signatureBytes | ForEach-Object { $_.ToString("x2") })
$signature = "sha256=$signatureHex"
$webhookHeaders = @{
    "X-MiniLogistics-Event" = "shipment.status_changed"
    "X-MiniLogistics-Timestamp" = $timestamp
    "X-MiniLogistics-Signature" = $signature
}

Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/webhooks/minilogistics" `
    -Headers $webhookHeaders `
    -ContentType "application/json" `
    -Body $webhookPayload | Out-Null

$query = "orderCode=$([uri]::EscapeDataString($order.orderCode))&phone=$([uri]::EscapeDataString($CustomerPhone))"
$localTrackingResponse = Invoke-RestMethod `
    -Method Get `
    -Uri "$ApiBaseUrl/api/orders/lookup/tracking?$query"
$localTracking = Get-ApiData $localTrackingResponse
if ($localTracking.providerStatus -ne "Delivered" -or $localTracking.orderStatus -ne 4) {
    throw "Delivered webhook was not reflected in local shipment and order state."
}
if ($localTracking.timeline.Count -eq 0) {
    throw "Local shipment timeline is empty after webhook processing."
}

[pscustomobject]@{
    OrderCode = $order.orderCode
    TrackingCode = $trackingCode
    QuoteAmount = $quote.totalFeeAmount
    ProviderStatus = $providerTracking.status
    LocalStatus = $localTracking.providerStatus
    TimelineEntries = $localTracking.timeline.Count
}
