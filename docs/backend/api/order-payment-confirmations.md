# Operational payment confirmations (SHIP-09)

Admin-only endpoints under `/api/admin/orders/{id}/payment`:

- `GET`: exact order amount, payment state, permitted actions and bounded audit
  facts (at most one per kind).
- `POST /confirm-transfer`: verifies manual transfer against the exact frozen
  amount and `VND`; commits Paid and eligible fulfillment work together.
- `POST /collect-cod`: records the full amount actually received for a COD order
  out for delivery. It marks Paid; a recorded Delivered shipment can then complete
  the order using the existing status/history/loyalty workflow.
- `POST /remit-cod`: records full remittance after collection. It does not change
  order revenue, total or payment state.

Each POST body is `{ amount, currency, reference, reason }`. All fields are
required. Reference is at most 120 characters, reason 500. Actor comes from the
authenticated admin session and time from the server. The receipt is immutable.
Only new orders with a persisted shipping selection use these commands. Legacy
orders return 409 and keep their previous payment/import workflow.

Missing authentication gives 401, Customer role 403. Invalid fields/currency give
400; wrong method/stage, cancelled/returned order, partial or excessive amounts
give 409. The minimal workflow does not book partial collections, edit totals,
automatically reconcile banks or pay refunds. Such cases need operational
exception handling. Repeating the same order/kind/reference and exact amount
returns the existing fact; a different reference conflicts. PostgreSQL enforces
one fact per order/kind and unique reference per kind across orders. All mutations
lock pending payment transactions before order, then shipment, matching callback
and cancellation lock order. Cancellation after payment retains RefundPending;
it never silently erases a receipt or marks a refund paid.

A new COD order remains Unpaid when a partner reports Delivered. The order stays
Shipping until full collection is confirmed. Collection and remittance are
separate audit facts. Direct admin completion cannot bypass the collection guard.
Prepaid orders cannot enter Shipping before Paid. WorkspaceEC dispatch/delivery
commands and their screens remain SHIP-10; the payment panel is available in the
admin order detail delivery section. New issuance stays off by default until the
remaining delivery/storefront/release gates are satisfied.

The `AddAuditedOrderPaymentReceipts` migration is additive in `payments`. Its Down
refuses to delete recorded evidence. Deploy via the migration job; replicas never
migrate at boot. Use disable-new-intake and forward fixes once data exists.
