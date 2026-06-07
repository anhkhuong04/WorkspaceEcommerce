# Architecture Rules

Architectural style:

- Build as a .NET Clean Architecture Modular Monolith.
- Use modules from `overview.md` as bounded areas of responsibility.
- Keep module internals private where possible; cross-module interaction should happen through Application contracts, domain events, or explicit integration services.
- Do not let controllers, React pages, or EF Core mappings become business-rule owners.

Layer responsibilities:

- `Domain`: entities, value objects, invariants, domain methods, domain events when they add clarity.
- `Application`: use cases, commands, queries, DTOs, validators, ports/interfaces, transaction orchestration.
- `Infrastructure`: EF Core, PostgreSQL, repositories, external services, file storage, authentication implementation.
- `Api`: controllers/endpoints, request binding, authorization attributes, response mapping, OpenAPI metadata.
- `Frontend`: route pages, feature components, API clients, typed UI models, forms, and presentation logic.

Dependency direction:

- Api depends on Application.
- Infrastructure depends on Application and Domain.
- Application depends on Domain and abstractions, not Infrastructure.
- Domain depends on no application, infrastructure, API, or UI code.
- Frontend must not mirror backend domain entities as mutable business objects; use API-facing types.

Modular Monolith guardrails:

- Keep each module independently understandable and testable.
- Avoid shared database writes across modules unless coordinated by an Application use case.
- Do not create a generic shared service when the behavior belongs to one module.
- Shared kernel code must be small, stable, and domain-neutral.
