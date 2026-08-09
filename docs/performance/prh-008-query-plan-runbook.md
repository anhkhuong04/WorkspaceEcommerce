# PRH-008 PostgreSQL query-plan runbook

The read paths that must remain bounded are customer/admin order pages, product review pages, active coupon pages, storefront catalog pages, and the dashboard's grouped aggregates. Their request code filters first, issues a `CountAsync`, and materializes only a deterministic `Skip`/`Take` page; child counts are correlated SQL subqueries or bounded page-ID batches.

## Capture plan evidence

Apply all migrations and load representative data. Then run:

```powershell
./scripts/performance/measure-prh-008-postgres.ps1 -ConnectionString '<PostgreSQL connection string>' -Analyze
```

If PostgreSQL runs in Docker and the host has no `psql` client, pass the container name and a connection string usable from that container:

```powershell
./scripts/performance/measure-prh-008-postgres.ps1 `
  -ConnectionString 'postgresql://user:password@localhost:5432/database' `
  -PsqlContainer '<postgres-container-name>' `
  -Analyze
```

The script writes timestamped `EXPLAIN (ANALYZE, BUFFERS)` output to `artifacts/performance/`. Preserve the report with the release evidence and compare it after data-volume or query-shape changes. Expected plans use the composite indexes introduced by `OptimizeReadPathIndexes`; a sequential scan on a tiny development table is normal, but a large representative table should not scan all orders/reviews/coupons for a 20-row page.

## Captured baseline evidence

On 2026-08-09, an isolated PostgreSQL 17 database was populated with 50,000 customer orders, 10,000 reviews distributed across catalog products, and 10,000 coupons. `EXPLAIN (ANALYZE, BUFFERS)` was captured after temporarily removing the PRH-008 indexes and again after restoring them. The generated reports are retained as release artifacts (`prh-008-postgres-20260809-223943.md` and `prh-008-postgres-20260809-223956.md`). Actual executor time, rather than client/`psql` startup time, is compared below.

| Query | Before | After | Plan/read evidence |
| --- | ---: | ---: | --- |
| Customer order page | 12.729 ms | 0.129 ms | 1,378 to 24 shared buffers; full scan/sort of 50,000 orders became `ix_orders_customer_created_order_code`. |
| Admin order page by status | 6.908 ms | 0.124 ms | 1,393 to 27 shared buffers; bitmap scan/sort of 16,667 rows became `ix_orders_status_created_order_code`. |
| Product review page | 0.329 ms | 0.122 ms | 93 to 22 shared buffers; product index plus sort became `ix_reviews_product_created_id`. |
| Active coupon page | 1.495 ms | 0.074 ms | 170 to 3 shared buffers; full scan/sort of 5,000 active rows became `ix_coupons_active_created_code`. |

The actual plan records include the generated SQL, returned rows, filter removals, shared-buffer reads, and executor latency. They also exposed that coupon ordering is mixed-direction (`created_at DESC, code ASC`), so its migration explicitly creates that index with matching sort directions.

## Automated guardrail

`AdminOrderIntegrationTests.ListOrders_UsesBoundedCountAndPageQueries` captures PostgreSQL commands and allows exactly two selects: count and page. It prevents a return to page-wide child lookup/N+1 behavior.
