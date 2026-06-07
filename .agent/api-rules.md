# API Rules

Contracts:

- Use request DTOs and response DTOs; never expose entities directly.
- Keep public field names stable once introduced.
- Use route groups and naming conventions already present in the project.
- Keep admin APIs under the admin route prefix used by `overview.md`.
- Keep storefront APIs separate from admin APIs.

Validation and errors:

- Use FluentValidation for request validation.
- Return validation errors in the project's standard API response format.
- Use `400` for invalid input, `401` for unauthenticated access, `403` for forbidden access, `404` for missing resources, and `409` for business conflicts.
- Do not return `500` for expected business failures.
- Do not expose stack traces, SQL errors, or internal exception messages to clients.

Response design:

- Project query results to response DTOs at the query boundary.
- Include only fields needed by the current API contract.
- Use pagination for list endpoints when result size can grow.
- Keep money, quantity, status, and timestamp fields typed consistently across APIs.
- Snapshot fields in order-related responses should reflect persisted order snapshots, not current product state.

Security:

- Admin endpoints must be protected once authentication is implemented.
- Never trust client-provided price, subtotal, total, stock, status history, or identity fields for authoritative backend decisions.
- Validate IDs, slugs, quantities, and status transitions server-side.
