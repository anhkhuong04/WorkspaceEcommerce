# ADR 007: WorkspaceEC delivery, versioned rates and frozen customer charges

## Status and scope

Accepted on 2026-10-29 by the product owner's approval of the recommendations in
`task/task_shipment.md` (SHIP-00 through SHIP-04 authorized for implementation).
This ADR records decisions for the whole feature; later checkout/fulfillment
tasks remain unimplemented until their work is explicitly undertaken.

## Decisions

### Rates and geography (D01/D02)

- WorkspaceEC operates its own delivery throughout Vietnam. Versioned destination
  zones group province codes, independently of their names. The initial scope is
  one configured origin and one delivery method/shipment per order.
- The administrative catalog is pinned to a documented official code-list
  version. A published rate plan covers every province of that catalog exactly
  once, including its wards/special administrative units; future catalog changes
  require a new catalog/rate-plan version. Counts are evidence, not business constants.
- Each zone has a base weight, base fee, positive weight step, nonnegative step
  fee and optional maximum weight. All delivery fees are whole nonnegative VND.
  Rates and geographic assignments are entered by administrators; no commercial
  rates, origin or weight limits are fabricated or published by migrations.
- Calculate actual merchandise weight as `sum(weightKg * quantity)` and round
  the aggregate upward once to an integer number of grams. Missing/nonpositive
  weights are errors, not the existing MiniLogistics parcel fallback. Volumetric
  weight, packaging, insurance, COD surcharge and installation are outside v1.
- Fee is `baseFee + ceil(max(0, grams-baseGrams)/stepGrams) * stepFee`.
  Unsupported configured weight is explicit unavailability, never a zero quote.

Calculator test examples only (not production prices): base 1,000 g / 10,000 VND,
step 500 g / 5,000 VND gives 10,000 VND at 1,000 g, 15,000 VND at 1,001 g and
20,000 VND at 1,501 g. Aggregate fractional grams before rounding once. Real
rates remain administrator-entered configuration.

### Publishing and quote validity (D03)

- A rate plan is Draft, Published or Retired. Only drafts may change. Zone
  memberships and rates form one immutable published version. Cloning creates a
  new draft; publishing replaces the active plan atomically. Published plans are
  unique per v1 scope, enforced in PostgreSQL as well as the application.
- Revision checks reject stale admin edits. Configuration writes use a shared
  PostgreSQL transaction lock; publish, retirement and audit commit together.
- Quotes will expire after ten minutes (configurable when SHIP-06 is implemented).
  They are server-owned, bound to customer/session, method, address, cart/payment
  inputs and a rate version. Publishing replacement rates does not rewrite a
  valid quote; emergency suspension may revoke unused quotes explicitly.
- Quotes do not reserve inventory. Checkout revalidates authoritative inputs and
  consumes a quote atomically with the order. Changed inputs/expiry/revocation
  require a new customer confirmation, not silently accepted repricing.

### Money and dispatch (D04/D05/D06)

- The customer shipping charge is frozen on order acceptance. Provider actual
  cost is separate and nullable until known; cost differences are borne/reviewed
  operationally without changing a committed order or VNPay amount.
- COD can create delivery work on order acceptance, with dispatch gated by order
  confirmation. VNPay dispatch requires verified payment; manual bank transfer
  dispatch requires an audited administrator confirmation of the exact amount.
- A local pending shipment may exist before payment. Creation is not permission
  to dispatch. Local delivery never needs a fabricated external provider ID.
- COD collection and remittance are separate facts. Delivery alone does not mark
  payment Paid; completing a COD order requires confirmed full collection.
  Partial/excess collections require an exception workflow, not editing totals.

### Compatibility and lifecycle (D07/D08)

- Existing routes, DTO values, enum numbers, provider protocols and historical
  snapshots remain valid. SHIP-01 adds separate shipping-selection/quote storage;
  absent selection means legacy, not an implicit WorkspaceEC order.
- New checkout selection/quote contracts are additive and introduced with
  SHIP-06/07. Old clients continue the legacy route until a deliberate cutover.
  Admin imports retain explicit legacy/import semantics until SHIP-12.
- `Order.ShipmentId` retains its historical external-provider meaning. New local
  shipment references use `OrderShipment.Id`; do not reinterpret old IDs. The new
  read contract must name local and external IDs explicitly.
- No automatic carrier switch, address change or customer charge change after
  acceptance. Failed deliveries may retry through the existing order graph;
  cancellation uses existing stock/refund/loyalty/warranty rules. Extra delivery
  costs do not automatically become additional customer charges.

The later fulfillment implementation follows this payment gate matrix:

| Payment | Work acceptance | Dispatch/completion guard |
| --- | --- | --- |
| COD | Durable work on order acceptance | Dispatch after order confirmation; completion requires confirmed full collection. Remittance is a separate fact. |
| VNPay | Idempotent work with verified successful payment | Dispatch only for Paid; callback amount equals the frozen order amount. |
| Manual transfer | Pending local record is allowed | Audited exact-amount confirmation commits Paid and eligible work together; no dispatch while Pending. |

Local dispatch maps to the existing order Shipping stage; failed delivery can
retry Shipping or cancel through its existing guards. Delivery confirmation,
payment collection and order completion must not be conflated. Implement that
coordinator in SHIP-09/10 rather than changing existing transitions in SHIP-00–04.

## Architecture and rollout

Keep the modular monolith and direct EF querying through focused stores. The
rate calculator is deterministic and independent of HTTP. Administrative catalog
and PostgreSQL configuration storage have Application ports and Infrastructure
implementations. API controllers only authorize/bind/map results.

ADR 006 remains in force for MiniLogistics: outbox mutations, leased retries,
stable idempotency and authenticated inbox callbacks. A local shipment transition
can commit within the same database transaction; it must not enqueue MiniLogistics
work or impersonate a partner webhook. Quote expiry and local workflows do not
exist merely because their schema exists.

Deploy additive schema, then compatible code on all replicas/workers, configure
and publish real rates, finish SHIP-05 through SHIP-14, and only then enable new
checkout traffic. The SHIP-00–04 delivery is administrative backend groundwork;
it does not enable WorkspaceEC fulfillment. Prefer disable-new-intake/forward
fix after new orders exist, preserving published history, quotes and audits.

## Validation baseline

Existing stock concurrency, payment callback, webhook, cancellation/import and
query-budget tests characterize legacy behavior. Record current commands/results
in the task ledger; new schema and admin operations additionally require clean
and upgrade PostgreSQL migration tests, revision/publish races, authorization,
coverage validation and calculator boundary tests.
