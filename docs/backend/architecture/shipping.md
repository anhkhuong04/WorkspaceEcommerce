# Shipping selection and delivery

Implemented 2026-11-01. Decision: [ADR 007](../adr/007-workspace-shipping-rates.md).
Operations and release gates: [shipping runbook](../../services/runbooks/workspace-shipping.md).

## Acceptance and money

`ShippingCheckout.Enabled` defaults to false. The additive flow issues a server-owned,
ten-minute quote bound to customer/session, cart price snapshots and current parcel,
canonical province/ward codes, street, payment and coupon. Quotes do not reserve stock.
Publishing new rates leaves existing valid quotes unchanged; revocation affects unused
quotes only. Explicit zero is a price; unavailable/expired is never a zero price.

Checkout locks quote, cart, variants and coupon. Order/items, stock/coupon reservation,
quote consumption, shipping selection and eligible fulfillment commit together.
An identical accepted-request retry recovers the same order even after cart removal
or quote expiry; changed requests conflict. Customer shipping charge and payment
amount are immutable. Provider actual cost remains separate and nullable.

WorkspaceEC prices use actual aggregate variant weight times quantity, rounded up
once to grams, with versioned region base/step fees in whole VND. Missing weight or
coverage prevents issuance. No implicit free shipping, COD surcharge or installation
pricing is introduced. Drafts are editable; Published/Retired plans are historical.
Revision checks and a configuration transaction lock protect publish and replacement.

## Fulfillment and payment

| Method | Durable work | Operational boundary |
| --- | --- | --- |
| WorkspaceEC | Local `OrderShipment`, initially AwaitingRelease | Reviewed admin assignment, dispatch, delivery, failure/retry; no carrier HTTP/outbox work |
| MiniLogistics | Existing leased shipment outbox | Provider protocol/idempotency preserved; quoted parcel/address/fee frozen |
| Legacy (no selection) | Existing behavior | No implicit WorkspaceEC conversion or historical repricing |

COD work can be accepted before payment. Dispatch requires order confirmation.
Prepaid dispatch additionally requires Paid. Manual transfer confirmation requires
the exact order amount, VND, unique reference and authenticated admin reason/actor.
Delivery alone does not make new COD orders Paid/Completed; full collection does.
Remittance is a separate audited fact. Partial/excess money requires an operational
exception, never an edit to a committed total.

Local actions lock payment, order and shipment, check expected revision, and persist
an operation request ID/payload hash. Identical retries return the persisted result;
different payloads/stale revisions conflict. Completion reuses order history/loyalty.
Cancellation goes through the existing order workflow (stock, refunds, loyalty and
warranty guards); no direct local status override. External creation rechecks order
cancellation after HTTP and commits a compensating cancel command when needed.

## Read contracts and access

Order details, customer/admin lists, lookup/tracking, checkout success, payment result
and receipt read the accepted selection. `shipping` is null for legacy orders.
Details expose method, customer charge, currency, grams, rate/zone revision, canonical
destination and explicit local/provider identifiers. Lists project shipping for the
bounded page in one query, without snapshot JSON, audit or operational cost.

`Order.ShipmentId` keeps its historical **provider** meaning. WorkspaceEC's internal
identifier is `shipping.localShipmentId`; `shipping.providerShipmentId` stays null.
Tracking text is not an authorization credential. Customer routes enforce ownership;
guest lookup/tracking/PDF still require order code plus phone. Payment result needs
owner or result-access proof. Local reads make no provider call. Private operation
actor/reason/assignee and actual cost are absent from public DTOs and local timelines.

Admin import remains `LegacyImport`: supplied fees retain legacy import validation
and no selection/dispatch is fabricated. Explicit new shipping selection headers
(`shippingMethod`, `shippingQuoteId`, `ratePlanId`) are rejected rather than silently
ignored. Import does not implement quoted WorkspaceEC checkout.

## HTTP boundaries

All JSON uses the existing envelope/errors. Existing routes retain their contracts;
new optional `shipping` properties are additive. Authoritative API inventory is the
controllers/OpenAPI, with these route families:

- `GET /api/checkout/shipping/capabilities`; `POST .../options`, `POST .../quotes`.
- Admin-only `/api/admin/shipping`: catalog, rate-plan draft/preview/validation,
  publish/retire/clone/history, weight readiness, unused quote revocation.
- Admin-only `/api/admin/orders/{id}/workspace-shipment`: GET state, POST `/actions`,
  GET `/history` (bounded paging). Actor comes from authenticated claims.
- Admin-only `/api/admin/orders/{id}/payment`: GET, POST `/confirm-transfer`,
  `/collect-cod`, `/remit-cod`.

`409` means revision/quote/workflow conflict; refresh and explicitly review a new
price/action. An uncertain checkout response must retry the **identical request**
first. Unknown method/version must never be interpreted as WorkspaceEC.
