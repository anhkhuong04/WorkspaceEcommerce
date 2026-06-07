# Data Rules

EF Core and PostgreSQL:

- Use EF Core migrations for schema changes.
- Design mappings for PostgreSQL, not EF InMemory behavior.
- Configure precision for money fields.
- Configure max lengths for names, slugs, SKU, email, phone, codes, and URLs where appropriate.
- Configure required fields explicitly.
- Use indexes for slugs, SKU, order codes, lookup fields, foreign keys, and common filters.
- Use unique constraints for domain identifiers that must be unique.

Query rules:

- Use `AsNoTracking()` for read-only queries.
- Project to DTOs for read APIs instead of loading full entity graphs.
- Include only required navigation properties.
- Avoid N+1 queries.
- Use async EF Core methods and pass `CancellationToken`.
- Keep filtering, sorting, and pagination in database queries, not in memory.

Write rules:

- Use transactions for checkout, order creation, inventory updates, and status history writes when multiple rows must stay consistent.
- Protect stock updates from race conditions; do not rely only on frontend checks.
- Do not hard delete records that `overview.md` says must remain historically valid.
- Keep audit/snapshot fields set by backend logic, not client input.
- Persist order snapshots so historical orders do not change when product data changes.

Migration rules:

- Migration names must describe the schema change.
- Do not edit old migrations after they are shared unless the user approves.
- Check generated migrations for unintended table, column, nullable, precision, or cascade changes.
