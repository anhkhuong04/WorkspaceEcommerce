# Architecture

```text
Api ───────────────> Application ─────────> Domain
 │                         ▲
 └──────> Infrastructure ──┘
                └────────────────────────> Domain

storefront/admin ──HTTP/SignalR──> Api
Infrastructure ──> PostgreSQL, S3/MinIO, SMTP, Google, VNPay, MiniLogistics
```

- **Domain:** entities and invariant-preserving methods; no Application, Infrastructure, HTTP, EF mapping, or UI dependency.
- **Application:** module use-case services, DTOs, FluentValidation, results, ports, and transaction orchestration.
- **Infrastructure:** `AppDbContext`, EF mappings/migrations, PostgreSQL locking, providers, documents/media, security implementations, workers.
- **Api:** composition root, middleware, auth, controllers, response mapping, health checks, SignalR. Controllers never own business rules or query EF.
- **Frontend:** transport types, React Query state, forms, routing, presentation. Backend remains authoritative.

## Intentional persistence approach

ADR 001 accepts direct DbContext-backed interfaces. Application intentionally references EF Core and composes `IQueryable` through focused stores plus compatibility interface `IAppDbContext`.

- Do not add a generic repository/unit-of-work wrapper.
- Prefer the smallest existing store; narrow `IAppDbContext` incrementally only within task scope.
- Keep PostgreSQL locks, leases, raw SQL, and provider details behind Application ports.
- Fakes do not prove EF translation or concurrency; use PostgreSQL integration tests.

## Boundaries

Business modules are Blogs, Cart, Catalog, Content, Coupons, Customers, Loyalty, Media, Ordering, Payments, Reviews, Shipments, and Warranties. Admin/Operations provide orchestration/read models.

- Cross-module writes require an explicit Application use case and transaction/idempotency decision.
- External work uses existing ports/outboxes; do not create parallel shipment/email paths.
- API is the composition root; register services in the existing Application/Infrastructure DI files.
- Frontend `api-types` owns contracts, `api-client` owns low-level HTTP, and `shared-utils` owns domain-neutral helpers.

Avoid architecture churn: no speculative CQRS/MediatR, generic repositories, AutoMapper, event bus, microservices, or competing frontend libraries without an authorized concrete need.
