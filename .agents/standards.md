# Engineering standards

## General and C#

- Follow nearby module/style conventions: file-scoped namespaces, nullable-aware code, explicit domain names, async I/O, propagated `CancellationToken`.
- Keep diffs scoped; do not mix behavior work with broad renames, formatting, dependency upgrades, generated churn, or cleanup.
- Inspect and preserve a dirty worktree. Add abstractions only for a real boundary/current variation.
- Use `TimeProvider` when time affects behavior; pass one use-case timestamp rather than adding scattered static clock reads.
- Use `Result`/`Result<T>` for expected validation/not-found/unauthorized/conflict. Translate invariant or unexpected infrastructure exceptions at the correct boundary.

## API

- Controllers bind/authorize, add server-owned context, call Application, and map results. They do not query EF, calculate totals, or decide provider behavior.
- Use DTOs, never entities. Preserve public routes, names, enum values, and shapes unless contract change is explicit.
- JSON uses `ApiResponse<T>` and existing `ResultExtensions`; `204` has no body. Mapping: validation `400`, unauthenticated `401`, policy forbidden `403`, absent/non-disclosing ownership `404`, conflict `409`, unexpected `500`.
- Validate in Application with FluentValidation. Keep `ProducesResponseType`/OpenAPI accurate and pass cancellation.
- Routes use existing public `api/...`, admin `api/admin/...`, and owned `api/customer/...` areas.
- Identity/IP comes from trusted `HttpContext`, not request DTOs. Anonymous responses expose only necessary data.
- Lists require bounded pagination, SQL-side filters, and deterministic ordering.

## EF Core and PostgreSQL

- Direct EF composition is intentional. Prefer focused stores; no generic repository.
- Read-only queries use `AsNoTrackingIfEf()`; use `*AsyncSafe()` only where project fakes require it. Prove provider behavior against PostgreSQL.
- Project/filter/sort/page in SQL. Avoid per-row queries, unbounded materialization, and unnecessary `Include`; test command counts on hot paths.
- Configure schema/names, required/max length, money precision, FK behavior, indexes, and uniqueness explicitly.
- Multi-row invariants need transactions. Use existing row-lock, conditional update, constraint, lease, or advisory-lock patterns with deterministic lock order. Pre-checks are not concurrency control; idempotency claims must be atomic.
- Add a migration for authorized schema changes. Review migration and snapshot for unrelated drops or nullable/precision/cascade changes; never rewrite shared migration history.
- API replicas do not migrate. Use the one-shot migration job.

## Frontend

- Keep the existing React 19, TypeScript, Vite, Tailwind 4, TanStack Query, React Hook Form, Zod, Router, and i18next stack.
- Put transport types in `api-types`, low-level HTTP in `api-client`, app endpoint adapters in `services/api`; do not scatter `fetch` in components.
- React Query owns server state. Keep UI/form state local and invalidate/update caches deliberately.
- Use React Hook Form + Zod for non-trivial forms; backend remains authoritative.
- Avoid `any`, duplicate DTOs/formatters, mutable backend-entity mirrors, and a shared component abstraction for one use.
- Preserve i18n, loading/error/empty/success states, labels, keyboard use, and visible validation.

## Testing

- Behavior changes need focused regression tests of observable outcomes, not private structure.
- Application fakes cover branching/orchestration, not EF translation, SQL count, locks, constraints, isolation, or races.
- Use Infrastructure tests for configuration/provider/mapping; API Integration + Testcontainers PostgreSQL for routes, auth, migrations, EF semantics, transactions, and concurrency. Do not use EF InMemory for PostgreSQL behavior.
- For races, use independent scopes/DbContexts. Control time, IDs, providers, and ordering; follow existing behavior-focused naming.
- Storefront has Vitest/Playwright. Admin currently has no unit-test script; run typecheck/lint/build and add tooling only when requested.

## Security

- Never commit secrets, raw tokens, keys, local settings, or production-like credentials. Tracked examples contain placeholders only.
- Enforce authentication, role, and ownership separately; preserve neutral anti-enumeration responses.
- Never trust client price/total/stock/status/role/customer/IP/path/MIME/callback outcomes.
- Verify callback signatures, then fail closed on missing/malformed required fields. Use fixed-time secret comparison where supported.
- Avoid logging PII, warranty identifiers, account links, payment secrets, refresh tokens, TOTP/recovery material, email bodies, or provider keys.
- Preserve CORS/trusted proxy rules, Data Protection persistence, secure cookies, security headers, size limits, media canonicalization, and production fail-fast validation.
- External HTTP integrations require an Application port, validated configuration, cancellation, bounded timeout/retry/response size, and SSRF/redirect review where relevant.
