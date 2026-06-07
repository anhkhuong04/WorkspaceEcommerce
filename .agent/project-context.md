# Project Context Rules

Use `overview.md` as the product contract.

Before business changes:

- Read the sections in `overview.md` related to scope, user flow, modules, business rules, data model, APIs, coding guidelines, implementation phases, and MVP acceptance criteria.
- Identify the exact module, use case, API route, entity, and business rule that justify the change.
- If a requested behavior is not described or implied by `overview.md`, ask the user before implementing it.
- Do not add future expansion items as MVP features unless the user explicitly asks.
- Do not rename product concepts, statuses, fields, or routes from `overview.md` without user approval.

Context interpretation:

- Prefer MVP simplicity over speculative extensibility.
- Keep extension points possible through clean boundaries, not through premature abstractions.
- If `overview.md` and existing code conflict, state the conflict and ask before changing behavior.
- If `overview.md` is ambiguous, implement the smallest behavior consistent with the current MVP.
- Keep generated code aligned with the proposed stack: ASP.NET Core, Clean Architecture, EF Core, PostgreSQL, FluentValidation, JWT, Swagger/OpenAPI, React, TypeScript, Tailwind CSS, React Hook Form, and Zod.
