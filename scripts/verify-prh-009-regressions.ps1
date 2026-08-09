[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$filter = @(
    'FullyQualifiedName~CartCheckoutAndOrderLookupIntegrationTests',
    'FullyQualifiedName~PaymentIntegrationTests',
    'FullyQualifiedName~LoyaltyIntegrationTests',
    'FullyQualifiedName~ShipmentWebhookIntegrationTests',
    'FullyQualifiedName~OrderShipmentServiceTests'
) -join '|'

& dotnet test WorkspaceEcommerce.slnx --no-restore --filter $filter --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) {
    throw 'PRH-009 business regression gate failed.'
}

Write-Host 'PRH-009 regression gate passed: checkout stock/coupons, VNPay idempotency, loyalty, and shipment outbox/webhook.'
