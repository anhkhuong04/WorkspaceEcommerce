# Domain and business context

These are high-cost rules that are easy to violate. Inspect the owning entity, use case, tests, and ADR before changing details.

## Commerce and orders

- Currency is VND: backend `decimal`, actual đồng, whole non-negative values, exchange rate `1`; no implicit conversion. See `docs/currency-and-money.md`.
- Price, totals, discounts, fees, stock, payment state, and identity are server-authoritative.
- Checkout revalidates active catalog state and stock inside a transaction using PostgreSQL locks. Stock changes through domain methods.
- Cart currently stores and checkout uses `UnitPriceSnapshot`. Preserve this existing policy unless a product decision and tests explicitly change repricing/expiry.
- Orders persist product/customer/coupon/price snapshots. `Order` owns status/payment transitions and history; never assign or duplicate transitions elsewhere.

## Payments and fulfillment

- Supported payment methods: COD, manual bank transfer, VNPay. Signed VNPay callbacks finalize pending transactions under locks; validate required fields and expected amount before mutation.
- Paid online orders enqueue shipment creation in the durable outbox. HTTP may quote shipping but must not add another provider-mutation path.
- MiniLogistics webhooks require signature/timestamp validation, atomic idempotency, timeline history, and monotonic state. Completed orders earn loyalty idempotently.
- Cancellation/return touches order, payment, stock, shipment, loyalty, and warranty. Trace the whole workflow; do not update one side in isolation.

## Identity, warranty, and media

- Admin and Customer roles are separate. HTTP authorization does not replace resource-ownership filtering.
- Guest checkout and checkout before email verification are deliberate (ADR 003).
- Refresh tokens are hash-only in storage and rotate in an `HttpOnly`, `SameSite=Strict` customer-auth cookie. Verification/reset responses must not reveal account existence. Never log raw tokens, TOTP material, or email action links.
- Anonymous order/payment/receipt/tracking access needs explicit possession/ownership proof and minimal response; object ID/order code alone is not authorization.
- Warranty is disabled by default. Activation requires the authenticated owner and a Completed platform order; coverage is snapshotted. Raw Serial/IMEI is never stored/logged—only versioned HMAC fingerprints and masks (ADR 005).
- Admin media upload goes through `IMediaStorageService` for validation, canonical WebP encoding, lifecycle persistence, and configured URLs. The current NoOp malware scanner is accepted risk, not real scanning.

Relevant accepted decisions: `docs/adr/001-005*.md`, plus module tests and source.
