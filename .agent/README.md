# Agent Instructions

This directory tells coding agents how to work in this repository.

Primary rule:

- Always read `overview.md` before implementing or changing business behavior.
- Treat `overview.md` as the only source of truth for product scope, modules, APIs, data model, statuses, payment methods, and business rules.
- Do not copy business requirements from `overview.md` into these files.
- Use `.agent` files for engineering decisions, code generation rules, review criteria, and workflow.
- Do not implement out-of-scope features unless the user explicitly changes the product scope.
- Do not change unrelated files.

Read order for feature work:

- `project-context.md`
- `workflow.md`
- `architecture.md`
- `coding-rules.md`
- Relevant specialized files: `backend-rules.md`, `frontend-rules.md`, `data-rules.md`, `api-rules.md`, `quality-standards.md`
- Relevant skill file under `skills/`

Files:

- `project-context.md`: How to use `overview.md` without duplicating product requirements.
- `architecture.md`: Clean Architecture and Modular Monolith boundaries.
- `coding-rules.md`: General code generation rules for this stack.
- `quality-standards.md`: Clean Code, SOLID, maintainability, and quality gates.
- `backend-rules.md`: ASP.NET Core, Application, Domain, Infrastructure, and DI rules.
- `api-rules.md`: API contract, validation, response, and error-handling rules.
- `data-rules.md`: EF Core, PostgreSQL, migrations, transactions, and query rules.
- `frontend-rules.md`: React, TypeScript, forms, API clients, and UI state rules.
- `workflow.md`: Required process before, during, and after changes.
- `skills/`: Task-specific execution checklists.
