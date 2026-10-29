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
