# Skill: Create Feature

Use this when adding a scoped backend, frontend, or full-stack feature.

Required reading:

- `overview.md`
- `.agent/project-context.md`
- `.agent/workflow.md`
- `.agent/architecture.md`
- `.agent/coding-rules.md`
- Specialized rule files for the changed area

Before editing:

- Confirm the feature is in scope according to `overview.md`.
- Identify the target module, use case, API route, entity, and business rule.
- Explain the implementation plan and affected files.
- If schema changes are needed, name the migration and expected database impact.

Backend implementation:

- Add or update Domain only for real business concepts and invariants.
- Add Application command/query, DTOs, result handling, and FluentValidation.
- Add Infrastructure persistence, EF mappings, repositories, services, and migrations when needed.
- Add Api endpoint/controller with thin request/response orchestration only.
- Use transactions for multi-row writes and business consistency.

Frontend implementation:

- Add typed API models and client calls.
- Add Zod schemas and React Hook Form for non-trivial forms.
- Keep UI state separate from server state.
- Do not duplicate backend business authority in React.

Completion:

- Add or update focused tests.
- Run relevant build/test/typecheck commands when available.
- Report changed files, verification, risks, and skipped checks.
