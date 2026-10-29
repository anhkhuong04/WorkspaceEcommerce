# WorkspaceEC rate administration (SHIP-00–04)

Implemented administrative backend only. This does not enable the new checkout,
issue customer quotes, dispatch local deliveries, or replace MiniLogistics.
The remaining feature is tracked in [task_shipment](../../../task/task_shipment.md).
Business decisions are recorded in [ADR 007](../adr/007-workspace-shipping-rates.md).

All endpoints below require an Admin bearer token and use `ApiResponse<T>`.
Writes identify the actor from the validated token's session claim, require a
reason, and commit definition changes and audit records in one PostgreSQL
transaction. The current single-admin authentication remains unchanged; this is
session attribution, not a new operator directory or fine-grained RBAC system.

## Routes

Base: `/api/admin/shipping`.

| Method | Path | Result |
| --- | --- | --- |
| GET | `/catalog` | Catalog version and province code/name summaries; no duplicated ward payload per province. |
| GET | `/catalog/provinces/{provinceCode}/wards` | Paged ward code/name list for that parent. |
| GET | `/catalog/weight-issues` | Paged variants missing a positive actual weight, including inactive variants marked as such; no fallback applied. |
| GET | `/rate-plans` | Paged summaries, optional numeric `status` (Draft=0, Published=1, Retired=2). |
| GET | `/rate-plans/{id}` | Definition, revision, timestamps and coverage. |
| POST | `/rate-plans` | Create draft; 201. |
| PUT | `/rate-plans/{id}` | Replace the complete draft definition, including all zones. |
| POST | `/rate-plans/{id}/clone` | Create a new draft from a source revision; 201. |
| GET | `/rate-plans/{id}/validation` | Missing/unknown provinces and coverage errors. |
| POST | `/rate-plans/{id}/preview` | Simulate rates for an exact draft/published/retired revision; never a checkout quote. |
| POST | `/rate-plans/{id}/publish` | Atomically retire the expected active version and publish this complete draft. |
| POST | `/rate-plans/{id}/retire` | Retire a published version; it cannot be edited or republished. |
| GET | `/rate-plans/{id}/history` | Paged revisions/actions/reasons/session attribution and before/after JSON. |

Pagination is one-based, `pageSize` 1–100 (default 20, wards default 100),
`pageNumber` 1–100000. Definitions have at most 100 zones and each zone at most
100 distinct province codes. Drafts can be incomplete; publishing requires exact
coverage of the pinned catalog and a known origin. Each assigned province is in
one zone. Names are never province/ward identifiers.

## Definition and preview input

Create/update use `SaveShippingRatePlanRequest`:

```json
{
  "name": "Administrator-entered rate version",
  "catalogVersion": "<version returned by catalog>",
  "originProvinceCode": "<selected province code>",
  "expectedRevision": 0,
  "reason": "<configuration reason>",
  "zones": []
}
```

`expectedRevision` is zero for create and the last-read positive revision for
update. An empty-zone draft is valid; it cannot publish. Each zone has `code`,
`name`, `provinceCodes`, positive `baseWeightGrams` and `stepWeightGrams`, whole
nonnegative VND `baseFee` and `stepFee`, and optional `maximumWeightGrams` at
least the base weight. Configure real values; production contains no seeded prices.

Preview accepts `provinceCode`, `wardCode`, `expectedRevision`, and `items` of
`{ weightKg, quantity }` (1–1000 lines). It verifies parent membership and uses
the deterministic calculator. These are administrator simulation values, not
trusted customer checkout weights. Its output includes `isPreview`, actual plan
`status`, `canPublish`, zone/destination and the weight/fee breakdown.
An incomplete draft can preview a configured destination but reports that it
cannot publish. No customer quote row is created and no carrier is called.

## Publishing, immutability and concurrency

Publish accepts `expectedRevision`, `reason`, `expectedActivePlanId` and
`expectedActiveRevision`. For the first publication the active fields are null.
For replacement both must match the last-read published plan. A concurrent
publication changes that compare condition, so a second stale request gets 409.
Re-read before a deliberate replacement. All configuration writes share a
module-specific PostgreSQL transaction advisory lock; database uniqueness and
revision concurrency tokens add independent safeguards.

Clone accepts source `expectedRevision`, new `name` and `reason`. It creates
fresh identifiers and a draft against the current catalog, copying compatible
zone assignments/rates. Removed province codes are not silently reassigned;
coverage reports any new missing provinces. Origin migration requiring a new
choice returns a validation error. Publish/clone never rewrite existing quotes,
orders, prices or published zone membership.

Retire accepts `expectedRevision` and `reason`. It stops that version being
active without erasing history; quote suspension/revocation is part of SHIP-06,
not an implicit side effect of this endpoint. Draft deletion is deliberately
absent; incomplete drafts can be retained for audit/continued editing.

400 indicates invalid definitions/input/coverage; 404 is an absent plan/province;
409 indicates stale revision/active-plan expectations or an illegal current
lifecycle. 401/403 follow the standard authentication/role boundaries. Request
limits are 256 KiB for definitions and 128 KiB for previews.

## Storage and deployment boundary

Migration `20261029041328_AddWorkspaceShippingFoundation` adds rate configuration,
audit, quote and order-selection tables without rewriting legacy orders or
pending work. Local shipments have a real local row ID and no provider ID;
`Order.ShipmentId` still has its legacy external meaning. `ActualCostAmount` is
nullable when unknown. The legacy `ShippingFeeAmount` remains for existing
contracts; new views must distinguish actual cost from customer shipping charge.

The new checkout has not been wired to these tables yet. Do not infer method
WorkspaceEC from absence of an order selection, fabricate provider IDs, or run
old code against enabled local orders. A migration Down refuses to discard new
shipping facts; after those facts exist use forward fixes and disable new intake.

Shared frontend contracts are in `frontend/packages/api-types/src/shipping.ts`.
Catalog provenance/update procedure is in
[shipping administrative catalog](../architecture/shipping-administrative-catalog.md).
UI integration belongs to SHIP-05; runtime customer quote/fulfillment/payment
integration belongs to SHIP-06 onward.
