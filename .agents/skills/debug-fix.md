# Skill: Debug Fix

Use this when diagnosing a bug, failing test, runtime error, or incorrect behavior.

Required reading:

- `overview.md` if the bug touches business behavior
- `.agent/workflow.md`
- `.agent/quality-standards.md`
- Specialized rule files for the suspected layer

Process:

- Reproduce the issue or inspect the failing command output before editing.
- Identify expected behavior from `overview.md`, existing tests, or API contract.
- Locate the failing layer: frontend, API, Application, Domain, Infrastructure, or database.
- Explain the fix plan and affected files before modifying code.
- Prefer the smallest fix that restores intended behavior without broad rewrites.
- Add or update a regression test for business logic, validation, persistence, API, or frontend behavior when relevant.

Guardrails:

- Do not hide defects with broad catches, nullable suppression, silent defaults, or frontend-only guards.
- Do not change business scope while fixing a bug.
- Do not weaken validation to make a failing test pass unless the validation is wrong against `overview.md`.
- Verify with the narrow failing test first, then related tests or build commands.
