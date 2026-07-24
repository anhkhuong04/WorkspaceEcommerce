# Agent Workflow

Before implementation:

- Read `overview.md` for any business feature or behavior change.
- Read `.agent/README.md`, `project-context.md`, and the relevant rule files for the task.
- Inspect the existing solution structure before deciding file locations.
- Identify the module, layer, API route, data model, and business rule references from `overview.md`.
- State a short implementation plan and list affected files before editing code.
- If the request is outside `overview.md`, stop and ask whether scope should change.

During implementation:

- Change only files required for the task.
- Preserve existing patterns unless they conflict with these rules or the user asks for a change.
- Keep module and layer boundaries intact.
- Add validation, authorization, transactions, persistence, API, frontend, and tests where required by the task.
- Do not silently change business rules, routes, statuses, payment methods, field names, or database semantics from `overview.md`.
- Do not introduce libraries, frameworks, or architectural patterns without a concrete need.

After implementation:

- Run the narrowest useful tests first.
- Run build/typecheck/lint when the changed area requires it and tooling exists.
- Check modified files for unrelated changes.
- Report changed files, verification commands, and skipped checks.
- Call out assumptions, risks, and follow-up work separately from completed work.
