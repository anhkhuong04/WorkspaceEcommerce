# Skill: Review Code

Use this when reviewing code, diffs, or implementation plans.

Required reading:

- `overview.md` if behavior is business-related
- `.agent/architecture.md`
- `.agent/coding-rules.md`
- `.agent/quality-standards.md`
- Specialized rule files for the changed area

Review order:

- Scope: behavior matches `overview.md` and does not add unsupported features.
- Architecture: dependencies point inward, controllers are thin, modules remain isolated.
- Clean code: naming, cohesion, SOLID, method size, duplication, explicit intent.
- Domain/Application: business rules, validation, transactions, result handling, testability.
- Data: EF Core mappings, migrations, PostgreSQL compatibility, indexes, precision, query shape.
- API: DTOs, route consistency, status codes, validation errors, authorization, response format.
- Frontend: TypeScript types, form validation, API boundaries, UI state, accessibility basics.
- Tests: coverage for business rules, edge cases, regressions, and persistence/API behavior.

Output format:

- Findings first, ordered by severity, with file and line references when possible.
- Include open questions or assumptions.
- Keep summary brief and secondary.
- If no findings exist, say so and list remaining verification gaps.
