# User and operational flows

## Browse to checkout

1. A guest or customer browses active catalog data and adds active variants to
   a session/customer cart. The cart stores quantity and a price snapshot.
2. Checkout validates contact and structured shipping address, rebuilds item
   snapshots from server data, and optionally validates a coupon. VNPay requires
   a MiniLogistics quote before placement; COD/manual transfer refresh the quote
   after the order transaction, so provider failure does not reject an accepted
   order.
3. In one PostgreSQL transaction, checkout locks relevant rows, creates the
   order and immutable snapshots, reserves coupon use, decrements stock, removes
   the cart, and creates VNPay/payment or shipment-outbox state as applicable.
4. COD returns an accepted order with queued shipment work. VNPay returns a
   provider payment URL. Manual bank transfer remains pending for an operational
   settlement process that is not implemented in this repository.

## VNPay completion

1. VNPay sends an IPN and may redirect the browser through the return endpoint.
2. Both paths verify the provider signature; the backend also verifies the
   transaction reference and amount.
3. Locked, idempotent processing finalizes the VNPay transaction and order
   payment status once.
4. A successful result inserts durable shipment-create work before commit. The
   browser is redirected to the storefront payment-result page; it does not
   decide payment state.

## Shipment fulfillment

1. A background worker leases due create/cancel commands with
   `FOR UPDATE SKIP LOCKED` and calls MiniLogistics using stable idempotency keys.
2. Create results persist the provider shipment/tracking identity and timeline.
3. Signed timestamped webhooks or an admin refresh update tracking state. Older
   provider events cannot regress the last observed state.
4. Provider status advances the order through valid intermediate states. On the
   first transition to `Completed`, loyalty earning is attempted idempotently.
5. Dead-letter work is inspected and replayed through admin operations APIs,
   never by editing rows directly.

## Customer identity and browser session

1. Registration creates an unverified customer and queues a one-time email link.
   Customers may still checkout.
2. Password or Google authentication either completes directly or creates a
   short-lived 2FA challenge.
3. Completed authentication returns a short-lived access token and sets a
   rotating refresh credential in an `HttpOnly`, `SameSite=Strict` cookie.
4. The storefront holds access state in tab-scoped `sessionStorage`; refresh is
   cookie-backed. Password reset/change and logout-all revoke refresh families.

## Cancellation and return

1. Customer cancellation is available only before processing; an admin may
   cancel only where the aggregate permits it. The transaction records history,
   restores stock, and cancels pending payment or marks paid funds
   `RefundPending`.
2. Shipment cancellation is queued after commit when a tracking identity exists.
3. A return request currently performs a direct `Completed -> Returned`
   transition. Product approval, item inspection, refund execution, inventory,
   loyalty, and warranty handling remain outside the implemented flow.

## Warranty activation

1. An administrator creates/version-controls a plan, maps it to variants,
   imports serialized units, and assigns units to purchased order items.
2. Feature flags are enabled in stages. The customer supplies a Serial/IMEI;
   the server normalizes it and compares versioned HMAC fingerprints.
3. Ownership, paid/completed order state, activation window, plan, item, and unit
   are revalidated under a transaction.
4. Activation snapshots coverage, advances unit/entitlement state, records an
   audit event, and queues notification email. Raw identifiers never enter the
   database or telemetry.

## Administrator operations

Administrators manage catalog/content/coupons, moderate reviews/comments,
progress order status, inspect tracking, retry shipment creation, cancel
shipments, import orders/warranty units, and replay dead-letter work. The admin
identity is currently one configured credential and one `Admin` role; it is not
a fine-grained operator/RBAC model.
