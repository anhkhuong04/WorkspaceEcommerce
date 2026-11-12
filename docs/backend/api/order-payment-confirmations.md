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
New orders with a persisted shipping selection and new manual imports carrying
`ImportSource` use these commands. Old unmarked legacy orders return 409 and keep
their previous workflow; historical records are not automatically reclassified.

Missing authentication gives 401, Customer role 403. Invalid fields/currency give
400; wrong method/stage, cancelled/returned order, partial or excessive amounts
give 409. Short/excess receipts use the audited exceptions workflow below. These
commands do not edit totals, connect to banks or initiate outgoing payments.
Repeating the same order/kind/reference and exact amount
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

## Payment differences

`GET /exceptions?pageNumber=1` returns ten evidence records per page and the
permitted next settlement. `POST /exceptions` takes
`{ requestId, actualAmount, reference, reason }`. Record a positive, whole VND
receipt that differs from the frozen total before full payment confirmation.
Manual transfer must be Pending; COD must be Unpaid and Shipping. Only one open
difference per order is permitted. Request ID and original reference are durable
retry identities; conflicting reuse is rejected.

`POST /exceptions/{exceptionId}/resolve` takes `{ amount, reference, reason }`.
Verify the distinct settlement evidence first: collect the exact shortfall or
return the exact excess. For an open order, settlement and the existing full
collection command commit together, including eligible fulfillment/loyalty. An
open difference blocks direct full confirmation. If cancellation/expiry won the
order lock first, resolution instead requires returning the actual funds
received and never marks the cancelled order Paid. Retrying the same resolution
does not add a receipt. VNPay remains on its verified gateway callback path.

`POST /confirm-full-refund` remains for audited full refunds on eligible closed
orders. Once an RMA exists, record each refund through its
[return case](order-returns.md) so quantities, warranty and loyalty reconcile.
