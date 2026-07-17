# ADR 001: Direct DbContext Over Repository

## Status

Accepted

## Context

WorkspaceEcommerce is a medium-scope modular monolith. Application services already express use cases directly, while EF Core provides unit-of-work behavior, change tracking, query composition, transactions, and testable `IQueryable` seams through persistence interfaces.

Adding a broad repository layer over every aggregate would mostly mirror EF Core APIs and make query-heavy flows harder to optimize, especially catalog listing, checkout, and admin search screens where projection, filtering, and pagination need to stay close to the use case.

## Decision

Use direct DbContext-backed persistence interfaces in application services instead of a generic repository pattern.

Read access is split by domain-oriented interfaces such as `ICatalogReadStore`, `IOrderReadStore`, and `ILoyaltyReadStore`. Write access is exposed through `IAppWriteStore`. The legacy `IAppDbContext` remains as a compatibility aggregate while services are gradually narrowed to the smallest interface they need.

## Consequences

- Services can compose EF queries directly and keep pagination/filtering in the database.
- Interfaces remain small enough for tests without hiding important query behavior.
- EF Core remains an intentional dependency of the application layer.
- Cross-cutting write behavior should stay in DbContext/unit-of-work infrastructure, not in generic repositories.

If a repository is needed later, start with a focused `IOrderRepository` for order-specific invariants and loading rules instead of introducing a generic repository across all modules.
