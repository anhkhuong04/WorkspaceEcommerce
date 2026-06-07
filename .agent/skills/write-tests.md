# Skill: Write Tests

Use this when adding, fixing, or improving tests.

Required reading:

- `overview.md` for expected business behavior
- `.agent/quality-standards.md`
- `.agent/backend-rules.md`, `.agent/api-rules.md`, `.agent/data-rules.md`, or `.agent/frontend-rules.md` as relevant

Priorities:

- Test Application use cases for business rules and branching workflows.
- Test validators for required fields, invalid values, and edge cases.
- Test API endpoints for routing, status codes, DTO shape, validation errors, and authorization.
- Test EF Core persistence when behavior depends on mappings, transactions, indexes, or PostgreSQL semantics.
- Test frontend forms for validation, submission payloads, loading/error states, and important user flows.

Project-specific focus:

- Checkout and order creation.
- Order status changes and history creation.
- Inventory updates and insufficient stock cases.
- Catalog visibility and active/inactive filtering.
- Snapshot behavior for order items.

Test quality:

- Use behavior-focused names following the existing project convention.
- Prefer clear arrange/act/assert structure.
- Use builders or fixtures only when they reduce repeated setup noise.
- Avoid testing private implementation details.
- Avoid EF InMemory for persistence behavior that must match PostgreSQL.
- Keep tests deterministic; control time, IDs, and external dependencies where needed.
