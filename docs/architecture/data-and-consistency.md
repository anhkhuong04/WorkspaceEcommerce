# Data and consistency

## Database ownership

One PostgreSQL database is divided by capability schemas:

| Schema | Main data |
| --- | --- |
| `catalog` | Categories, products, variants, images, specifications, reviews |
| `cart` | Carts and cart items |
| `ordering` | Orders, item snapshots, status history |
| `payments` | VNPay transaction attempts |
| `shipping` | Shipments, timeline, webhook inbox, command outbox |
| `promotions` | Coupons, targets, redemptions |
| `customer` | Accounts, addresses, sessions/tokens, login history, email outbox |
| `loyalty` | Accounts, point ledger, tiers |
| `content` | Banners, blog/comments, durable-media metadata |
| `warranty` | Plans, serialized units, entitlements, coverage snapshots, audit |

Use explicit EF configurations and schema-qualified tables. Relationships
default to restrictive deletion where business/audit history must survive;
cascade only when the child has no independent lifecycle. Never edit a
historical migration after it may have been applied.

## Transaction boundaries

Use a transaction when one business outcome spans multiple mutable records.
Current high-value boundaries include checkout, payment callback finalization,
cancellation, refresh-token rotation, loyalty redemption, warranty activation,
shipment webhook application, and durable outbox enqueue.

Lock rows with the focused persistence methods already provided when a decision
depends on the latest value:

- Product variants before stock decrement/restore.
- Coupons before usage reservation.
- Orders and VNPay transactions before callback/cancellation mutation.
- Refresh tokens before rotation/reuse detection.
- Warranty activation locks the serialized unit, entitlement, then order in that
  order for both admin and customer entry points.

Acquire collections in deterministic order. Translate expected EF concurrency
failures to a conflict result; do not retry a non-idempotent use case blindly.

## Snapshots and audit history

Mutable reference data must not rewrite historical commerce facts. Orders retain
customer/address, product/SKU/image, unit price, coupon, currency, and exchange-
rate snapshots. Warranty activation retains coverage and terms snapshots.
Status history, payment attempts, coupon redemptions, loyalty ledger rows,
shipment timeline, webhook inbox, outbox/dead-letter rows, and warranty audit
events are business evidence; do not delete them as cleanup.

## Durable external work

Email delivery and shipment mutations use PostgreSQL outboxes. Workers lease a
bounded due batch with `FOR UPDATE SKIP LOCKED`, commit the lease before the
provider request, and complete/retry only when the lease token still matches.
This is at-least-once delivery: consumers/provider calls must be idempotent where
possible. Shipment create uses the order code as idempotency key and cancel uses
`<order-code>:cancel`.

MiniLogistics callbacks use a durable inbox/timeline. Duplicate event IDs are
acknowledged without reapplying state, and provider event time cannot regress the
persisted shipment state. See [ADR 006](../adr/006-durable-external-side-effects.md).

## Migrations

- Generate migrations in `WorkspaceEcommerce.Infrastructure` with
  `WorkspaceEcommerce.Api` as startup project.
- Review the migration, designer, and `AppDbContextModelSnapshot` together.
- Any model change must make `migrations has-pending-model-changes` pass.
- Validate both a clean database and the supported upgrade path for risky
  migrations. Production runs one migration job, never one per API replica.
- Prefer additive, backward-compatible changes. Security, token, media, outbox,
  and audit data normally require a forward fix rather than destructive `Down`.

Commands and verification are in the [development guide](../development/guide.md)
and [production release runbook](../runbooks/production-release.md).
