# Known limitations and protected areas

These are current boundaries, not an invitation to expand unrelated tasks.
Re-verify source and tests before changing them.

## Product and workflow gaps

- [Item returns](../api/order-returns.md) support approval, physical inspection,
  partial verified refunds, loyalty reversal/debt and warranty reconciliation.
  Serialized returned goods stay quarantined; resale needs a separate
  reprovisioning workflow. Refund execution and carrier reverse logistics are
  operational actions; no bank/VNPay refund worker initiates transfers.
- Paid cancellation enters RefundPending until verified outgoing proof is
  recorded. [Payment confirmations](../api/order-payment-confirmations.md)
  reconcile new manual transfers/COD and short/excess receipts. Old unmarked
  legacy orders keep their prior workflow and need reviewed migration rather
  than automatic reclassification.
- COD/manual-transfer shipping fee refresh happens after the order transaction.
  A quote failure can leave an accepted order at its initial zero shipping fee;
  COD shipment work may already be queued. Treat any redesign as a money and
  fulfillment consistency change.
- Loyalty tier discount/free-shipping fields are displayed but checkout does not
  apply them. Verified item-return refunds reverse earned points proportionally;
  already spent points become debt repaid from future earning.
- PDF receipts are not legal tax/VAT invoices.
- Admin authentication is one configured credential and `Admin` role; there is
  no operator directory or fine-grained RBAC.

## Security and scale gaps

- Application rate limiting is in-process. Authentication now runs first and
  security-critical route families have independent identity/client buckets,
  but multi-replica deployment still needs an edge/distributed limiter and
  topology testing.
- SignalR has no shared backplane. Multi-replica notifications are not complete
  until an approved backplane is configured and tested.
- Media uses a `NoOp` malware scanner. Non-Development use is a time-limited
  accepted risk, not a production scanning capability.

## Performance and maintainability

- VNPay checkout builds item snapshots before the shipping quote and again under
  locks during placement. Cart/checkout now use batched reads with query-budget
  integration coverage; preserve the locked revalidation when optimizing.
- The Admin frontend has lint/typecheck/build scripts but no unit-test script.
- The built-in JWT validator accepts one HMAC key at a time, so signing-key
  rotation is a planned session-expiry window, not transparent rollover.

## Do not refactor opportunistically

- Direct EF querying in Application is accepted by [ADR 001](../adr/001-direct-dbcontext.md).
- Do not rewrite historical migrations or bulk-rename schemas, routes, envelopes,
  DTOs, or localization conventions.
- Do not bypass the outbox/inbox, token hashing/rotation, Data Protection,
  signature checks, media lifecycle, or warranty identifier protection as cleanup.
- Split large order/shipment/warranty services only in a scoped,
  behavior-preserving change with characterization tests.
