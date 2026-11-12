# Item returns and verified refunds

Admin routes are under `/api/admin/orders/{orderId}/returns`. Authentication and
the Admin role are required; actor and operation time are server-owned.

1. `GET ?pageNumber=1` returns eligibility, remaining item quantities, masked
   physical units and ten return cases per page, including immutable audit.
2. `POST` takes `{ requestId, reason, items: [{ orderItemId, quantity }] }`.
   The order must be a paid, completed VND sale with a recorded completion time.
   The existing seven-day return window applies; a timely, unreviewed customer
   request remains reviewable after that window. Approval reserves quantities
   across cases. Reusing the same request ID and data is idempotent.
3. `POST /reject-request` takes `{ reason }` to reject the pending customer
   request. Its reviewed decision/reason is visible to that customer.
   Rejection is final for that request and cannot be bypassed by creating an
   admin case. There is no appeal/reopen endpoint in this workflow.
   `POST /{id}/reject` takes `{ expectedRevision, reason }` and releases an
   unreceived case's reserved quantities.
4. `POST /{id}/receive` takes `{ expectedRevision, reason, refundShipping, items:
   [{ orderItemId, receivedQuantity, restockQuantity, inspection,
   serializedUnitIds: [] }] }`. Inspect every approved line once. Receive only
   physical goods; restock only inspected sellable quantities. Partial receipt
   closes this case's inspection; unreceived quantities can use another case.
   Serialized returns enter quarantine, close their entitlement and cannot be
   counted again. Reselling serialized returned goods requires a future
   reprovisioning workflow; this operation does not add those units to stock.
5. `POST /{id}/refund` takes `{ expectedRevision, amount, reference, reason }`.
   Record only an already sent and verified payment for exactly the case's
   allocated refund. It does not initiate a transfer or call a refund provider.

Refund amounts use frozen line prices and the order's original discount.
Cumulative whole-VND allocation avoids per-unit rounding loss. Discount beyond
merchandise value reduces refundable delivery fees. Delivery fees may be
approved only with receipt of the whole order and cannot be refunded twice.
The sum of allocations and recorded refunds cannot exceed the frozen total.
Partially returned orders stay Completed; physical receipt of all items changes
the order to Returned. Payment is RefundPending while an approved refund remains
unrecorded, then PartiallyRefunded (`7`) or Refunded (`6`).

Verified merchandise refunds reverse the earned points proportionally. If points
were already spent, available points stop at zero and `PointsDebt` holds the
remainder; later earning first repays this debt. Tier eligibility is recalculated.
Existing vouchers and historical transactions are retained. Return decisions,
receipt/disposition, outgoing proof and actor/time remain auditable. Customer
notifications use the existing transactional email outbox.

All mutations lock payment rows then units/entitlements in a deterministic
order, followed by the order/case and affected stock/account. Order locking,
revision checks and persistent request/receipt identities prevent simultaneous
over-approval, duplicate stock movement and duplicate refunds. Shipping provider
returns and bank settlement remain operational actions requiring real evidence.

Apply `AdminOperationsRemediation`, `PaymentDifferenceReconciliation` and
`AuditManualStockAdjustments` through the migration job. Their Down paths reject
removing stored reconciliation/audit evidence; use forward fixes after intake.
