# Shipping quotes and accepted orders

The additive APIs are disabled by default. `ShippingCheckout:Enabled` controls
new quote issuance; `WorkspaceEnabled` and `ExternalEnabled` separately control
new offers. They do not revoke accepted orders, valid quotes, or partner backlog.
Keep issuance disabled until the delivery lifecycle and storefront rollout gates
in SHIP-10–14 are complete. Legacy clients keep their existing routes/semantics.

- `POST /api/checkout/shipping/options`: session, street, province/ward codes,
  payment method, optional coupon, method. Returns availability per method.
  This operation never calls a carrier. External `QuoteRequired` means availability
  must be confirmed by requesting that method's quote; it is not a promised fee.
- `POST /api/checkout/shipping/quotes`: same input, explicit method
  (`0 = WorkspaceEC`, `1 = MiniLogistics`). Only a selected external method calls
  the carrier. Failure is explicit 409 with no zero-priced replacement.
- `POST /api/admin/shipping/quotes/{id}/revoke`: Admin-only `{ reason }`;
  server session actor/reason/time are persisted. Consumed quotes cannot be revoked.

Quotes return an unpredictable ID, whole VND customer charge, actual/calculated
grams, expiry and optional rate-plan ID. No public audit/cost/address payload is
returned. Customer identity and session are hashed together; the ID alone is not
authorization. Cart prices, quantities, weights/dimensions, catalog version,
address codes/street, payment and coupon discount are fingerprinted. Quote issuance
does not reserve stock. A new published plan does not invalidate a still-valid
offer from the previous plan. Invalid address parents and missing local weights
fail explicitly.

`ShippingCheckout:QuoteLifetimeMinutes` defaults to 10 (1–60),
`ExternalQuoteTimeoutSeconds` to 12 (1–60). Existing adapter retries remain bounded
inside the overall quote deadline. `UnusedQuoteRetentionDays` defaults to 30
(1–365). An hourly worker deletes at most 1,000 expired, unconsumed quotes older
than retention. Consumed offers remain linked as order evidence. Snapshots contain
delivery data and require the same database access controls as order addresses;
do not log snapshot JSON or session IDs.

## Acceptance and fulfillment (SHIP-07/08)

`POST /api/checkout` accepts additive `shippingQuoteId`, `shippingMethod`,
`shippingProvinceCode` and `shippingWardCode`. Send street and customer/payment
details as before. With a quote, backend resolves the address names; client names,
shipping fee and IP address are not authoritative. The API supplies the trusted
remote IP for VNPay. Missing/invalid selection fields fail explicitly.

Quote, cart/items and variants are locked; coupon eligibility/usage is rechecked.
The cart's `UnitPriceSnapshot` policy is preserved. Quote consumption, selection,
order/items, stock/coupon reservation and eligible work share one transaction.
Changed inputs, expired or revoked quotes require a new confirmation (409), while
another subject receives a neutral 404. Repeating the exact accepted checkout
returns the same order, including after cart removal/expiry; changing acceptance
details returns 409. The quote ID is the idempotency key for this new flow.

Published rate changes do not rewrite accepted fees. VNPay transaction amount is
the frozen order total. New orders never run legacy post-commit quote refresh or
worker repricing. Partner create uses the persisted parcel/address snapshot and
stores actual cost separately. Tracking returns the accepted customer fee for
new selections. Legacy imports/orders retain their old semantics.

WorkspaceEC has a local `AwaitingRelease` record with its own ID/tracking code;
`Order.ShipmentId` still means external provider ID. No partner create/cancel/
refresh/replay is permitted for local work. Order cancellation changes local work
in the same transaction. Direct local delivery status changes are blocked until
the SHIP-10 lifecycle commands are implemented. External COD work is accepted
with the order; prepaid work requires Paid. VNPay verification and work creation
share the payment transaction and duplicate callbacks remain idempotent.

External creation runs outside order locks. Its response is persisted after
reloading the order under its lock; cancellation during HTTP creates durable
compensating cancellation alongside the provider mapping. Existing leases,
idempotency keys, retries/dead letters remain in use. Disabling external intake
does not disable backlog processing. The HTTP adapter is constructed lazily:
local-only resolution requires no carrier credentials or HTTP connectivity, while
actual partner operations fail explicitly if unconfigured. A missing webhook
secret always fails authentication.
