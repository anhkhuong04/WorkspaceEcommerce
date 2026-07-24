# Skill: Refactor Code

Use this when improving structure without changing observable behavior.

Required reading:

- `.agent/architecture.md`
- `.agent/coding-rules.md`
- `.agent/quality-standards.md`
- Specialized rule files for the changed area
- `overview.md` if business behavior, names, or rules may be affected

Before editing:

- Confirm whether behavior must remain identical.
- Explain the refactor plan and affected files.
- Define the safety check: tests, build, typecheck, or manual comparison.

Allowed refactors:

- Move business logic from controllers or React components into Application/Domain.
- Split large services into cohesive use cases.
- Extract duplicated mapping, validation, or query code when abstraction improves clarity.
- Replace primitive-heavy domain logic with clearer types when behavior stays equivalent.
- Improve dependency direction without changing public contracts.

Forbidden without explicit approval:

- Renaming public routes, DTO fields, statuses, database columns, or business concepts.
- Rewriting module boundaries broadly.
- Editing shared migrations that may already be applied.
- Introducing new frameworks or libraries.

Completion:

- Run before/after tests when feasible.
- Report behavior-preservation evidence and remaining risk.
