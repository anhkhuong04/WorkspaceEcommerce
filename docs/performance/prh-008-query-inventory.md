# PRH-008 IQueryable inventory and prioritization

Run `./scripts/performance/inventory-prh-008-query-terminators.ps1` from the repository root to produce the complete line-level inventory for Application and Infrastructure. The generated report is intentionally an artifact (not source-controlled) so it stays accurate after every query change.

Prioritized request paths and outcomes:

| Priority | Path | Change / invariant |
| --- | --- | --- |
| P0 | Customer orders | Filtered `CountAsync`, bounded page projection, correlated item count, no full customer-order materialization. |
| P0 | Admin orders | Case-normalized filters precede count/page; correlated item/shipment fields execute in the page query, with a query-count guardrail. |
| P0 | Admin reviews | SQL count/page plus product/customer joins; localized display text is mapped only after the bounded page returns. |
| P0 | Admin coupons | Active/effective/search filters run before count/page; targets and redemption counts are fetched only for page coupon IDs. |
| P1 | Catalog | Existing count/page flow remains server-side and uses bounded batched child reads; read queries are no-tracking. |
| P1 | Blogs | Public/admin collection contracts now use async no-tracking reads, deterministic ordering, and a server-side 100-item cap; related-product reads are batched by bounded related IDs. |
| P1 | Dashboard | Group/aggregate/recent-order projections remain database-side and no-tracking. |
| P2 | Authentication/profile/lookups | Customer auth/profile/address/session/2FA/account-lifecycle, order lookup, and payment result reads use the safe async adapter; write/locking paths intentionally remain tracked. |

The `QueryableAsyncExtensions` adapter calls EF Core async APIs for database providers while retaining deterministic in-memory behavior for unit-test fakes. `AsNoTrackingIfEf` is used only on read paths, never on mutation or explicit locking paths.
