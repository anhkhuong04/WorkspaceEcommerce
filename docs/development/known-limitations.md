# Known limitations and protected areas

These are current boundaries, not an invitation to expand unrelated tasks.
Re-verify source and tests before changing them.

## Product and workflow gaps

- Return handling is only a direct `Completed -> Returned` transition. There is
  no return request/review aggregate, item-level disposition, refund execution,
  inventory movement, loyalty reversal, or automatic warranty reconciliation.
- Paid cancellation ends at `PaymentStatus.RefundPending`; no VNPay refund worker
  or reconciliation process exists.
- Manual bank transfer checkout remains pending; there is no settlement endpoint
  or automatic shipment trigger for it.
- COD/manual-transfer shipping fee refresh happens after the order transaction.
  A quote failure can leave an accepted order at its initial zero shipping fee;
  COD shipment work may already be queued. Treat any redesign as a money and
  fulfillment consistency change.
- Loyalty tier discount/free-shipping fields are displayed but checkout does not
  apply them. Earned points are not reversed on cancellation/return.
- PDF receipts are not legal tax/VAT invoices.
- Admin authentication is one configured credential and `Admin` role; there is
  no operator directory or fine-grained RBAC.

## Security and scale gaps

- `GET /api/payments/result` accepts phone as optional. Knowledge of an order
  code can expose payment/shipment result data; do not copy this authorization
  pattern to new endpoints.
- Rate limiting is in-process and runs before authentication. Multi-replica
  deployment needs an edge/distributed limiter and topology testing.
- SignalR has no shared backplane. Multi-replica notifications are not complete
  until an approved backplane is configured and tested.
- Media uses a `NoOp` malware scanner. Non-Development use is a time-limited
  accepted risk, not a production scanning capability.
- Shipment webhook duplicate lookup occurs before the transaction and the
  order/shipment records do not provide a complete concurrency barrier for
  simultaneous different events. Preserve inbox/signature/monotonic checks and
  add a focused design/test before claiming exactly-once processing.
- Admin warranty activation does not use the same locked transaction as customer
  activation. Warranty remains disabled by default.

## Performance and maintainability

- Cart/checkout still contain per-item reads, and VNPay checkout rebuilds item
  snapshots. Do not reproduce these query shapes; measure before refactoring.
- Account cleanup materializes expired rows before deletion; do not extend this
  unbounded pattern to new retention jobs.
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
