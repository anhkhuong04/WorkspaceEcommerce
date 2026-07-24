# Backend Rules

ASP.NET Core:

- Keep controllers thin: bind request, authorize, call Application, map result to response.
- Do not query DbContext directly from controllers.
- Do not put checkout, inventory, order status, catalog, or admin workflow decisions in controllers.
- Use route names and HTTP methods from `overview.md` unless the user approves a change.
- Use `[Authorize]` for admin-only APIs once authentication exists.
- Keep Swagger/OpenAPI metadata accurate when adding endpoints.

Application layer:

- Model each write operation as an explicit use case or command handler.
- Model read operations as query services or query handlers that return response DTOs.
- Validate commands with FluentValidation before executing state changes.
- Coordinate transactions in Application or an Application-level unit of work.
- Return predictable result types for not found, validation failure, conflict, and success.
- Keep cross-module coordination explicit and localized to the use case.

Domain layer:

- Put invariants and state transitions inside entities or domain services when they are true business rules.
- Keep entities persistence-friendly but not persistence-owned.
- Use methods that express intent, such as status transition or stock adjustment operations, rather than public setters for protected invariants.
- Avoid anemic code for rules that must stay consistent across API and background flows.

Infrastructure layer:

- Implement Application interfaces here.
- Keep EF Core configurations explicit with `IEntityTypeConfiguration<T>` when mappings grow.
- Keep file storage, authentication, email, and other external concerns behind Application abstractions.
- Do not leak Infrastructure types into Application or Domain contracts.

Dependency injection:

- Register services by module.
- Prefer scoped lifetime for DbContext-backed services.
- Avoid service locator patterns and resolving services manually inside business code.
