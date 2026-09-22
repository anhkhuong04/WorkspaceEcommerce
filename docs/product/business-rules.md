# Business rules

This document records cross-module invariants. Exact validation limits remain
in validators and configuration; these rules explain behavior that must stay
coherent across use cases.

## Money and pricing

- Commerce uses actual Vietnamese dong (`VND`), whole non-negative amounts,
  backend `decimal`, and an order exchange-rate snapshot of `1`.
- UI language never selects or converts currency. Formatting is presentation
  only. See [currency and money](../currency-and-money.md).
- Server-side catalog price, discounts, fees, stock, payment state, and totals
  are authoritative. Never trust totals posted by a client.
- A cart item retains the price observed when it was added. Checkout currently
  uses that `UnitPriceSnapshot`; changing this to automatic repricing is a
  product decision, not a refactor.
- Order items and applied coupons retain snapshots. Receipts render those
  snapshots and are proof-of-purchase documents, not tax invoices. See
  [order receipts](../order-receipts.md).

## Cart, stock, coupons, and checkout

- A cart belongs to either a customer or an anonymous session. A variant occurs
  at most once per cart/order; adding it again increases quantity.
- Checkout revalidates active variants and stock, locks variant and coupon rows
  before mutation, decrements stock, reserves coupon usage, creates the
  order/payment record, removes the cart, and enqueues applicable shipment work
  in one database transaction.
- Coupons must be active and within their effective window. Product-scoped
  coupons discount only eligible product subtotal. Usage limits are reserved
  under a lock during checkout.
- Loyalty vouchers are customer-scoped, fixed-amount, single-use coupons whose
  codes start with `LOYAL-`.

## Order lifecycle

Allowed order transitions are owned by the `Order` aggregate:

| From | Allowed next states |
| --- | --- |
| `Pending` | `Confirmed`, `Cancelled` |
| `Confirmed` | `Processing`, `Cancelled` |
| `Processing` | `Shipping` |
| `Shipping` | `Completed`, `FailedDelivery` |
| `FailedDelivery` | `Shipping`, `Cancelled` |
| `Completed` | `Returned` |
| `Cancelled`, `Returned` | Terminal |

Every transition retains status history. `CompletedAt` is set only the first
time the order reaches `Completed` and is the business timestamp used for
warranty eligibility.

## Payments and fulfillment

- Supported order payment methods are COD, manual bank transfer, and VNPay.
  COD starts `Unpaid`; bank transfer and VNPay start `Pending`.
- VNPay callbacks must have a valid signature and matching amount. The payment
  transaction and order are locked so browser return, IPN, and provider retries
  cannot finalize the same attempt twice.
- Accepted COD orders enqueue shipment creation during checkout. Successful
  VNPay callbacks enqueue it in the payment transaction. The background worker
  is the only component allowed to mutate MiniLogistics.
- Shipment provider events are authenticated, deduplicated, retained on a
  timeline, and applied monotonically to shipment/order state. `Delivered`
  advances an order to `Completed`; failed/returned delivery maps to
  `FailedDelivery`, not the commerce `Returned` state.

## Cancellation and return

- Customers may cancel only `Pending` or `Confirmed` orders; administrators are
  still constrained by the aggregate transition table. A reason is required.
- Cancellation restores item stock once within the locked transaction. Pending
  VNPay transactions become cancelled. A paid order changes payment status to
  `RefundPending`; no automatic refund is implied.
- If a provider shipment exists, cancellation queues a durable provider cancel.
- A customer return currently changes a `Completed` order directly to
  `Returned`. It does not create an approval case, refund transaction, stock
  movement, loyalty reversal, or warranty reconciliation. Do not assume those
  side effects occurred.

## Loyalty

- Only authenticated customers earn points, once per completed order.
- Earned points are `floor(max(0, subtotal - discount) * exchangeRate /
  MoneyPerPoint)`. Shipping does not earn points. Defaults are `10,000 VND` per
  point, `1,000 VND` voucher value per point, and 30-day voucher validity.
- `TotalPointsEarned` drives upgrades from Bronze through Platinum; tiers do not
  downgrade. Tier discount/free-shipping fields are currently display metadata
  and are not applied by checkout.
- Redeeming points is concurrency-protected and creates one voucher plus one
  negative ledger entry. Returns/cancellations do not currently reverse earned
  points.

## Identity and ownership

- Guest checkout and customer checkout before email verification are deliberate.
- Admin and Customer are separate roles. Authorization attributes never replace
  order/customer ownership predicates.
- Guest order, tracking, and receipt access requires order code plus phone.
  Account recovery responses must not disclose whether an account exists.
- Refresh tokens and account-action tokens are stored only as hashes. Refresh
  reuse revokes its whole family. TOTP secrets are Data Protection payloads;
  recovery codes are one-time password hashes.

## Warranty and media

- Warranty is disabled by default and separately gates admin, activation, and
  public lookup. Activation requires an assigned serialized unit, the owning
  authenticated customer, and a completed platform order. Non-COD orders also
  require `PaidAt`; COD uses `CompletedAt` as its purchase timestamp. Coverage
  is snapshotted when activated.
- Raw Serial/IMEI values must never be stored or logged. Persist only masked
  identifiers and versioned HMAC fingerprints; key rotation is dual-read and
  requires reconciliation.
- Uploaded images pass through size/dimension validation, metadata stripping,
  canonical WebP encoding, object storage, and lifecycle metadata. The current
  `NoOp` malware scanner is an accepted risk, not malware detection.

Related decisions: [ADR 002](../adr/002-customer-totp-authentication.md),
[ADR 003](../adr/003-customer-account-lifecycle.md),
[ADR 004](../adr/004-durable-media-storage.md), and
[ADR 005](../adr/005-warranty-activation-and-identifier-protection.md).
