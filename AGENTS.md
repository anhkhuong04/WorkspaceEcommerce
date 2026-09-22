# WorkspaceEcommerce agent entry point

Applies to the whole repository. Before editing, read [`.agents/README.md`](.agents/README.md) and only the task-specific files it routes to.

- Make the smallest complete change; preserve unrelated working-tree changes.
- Current source, tests, migrations, configuration, and CI are executable truth; use [`docs/README.md`](docs/README.md) for canonical product/architecture context and accepted ADRs. Do not depend on `overview.md` or historical ticket prose.
- Preserve [the implemented architecture](.agents/architecture.md), including intentional direct EF usage. Do not add generic repositories, MediatR/CQRS, an event bus, or new framework layers without a demonstrated need.
- Do not silently change business behavior, routes/DTOs, auth boundaries, provider protocols, database semantics, or historical snapshots.
- Never commit secrets/local settings. Never edit a shared historical migration; add a migration when schema change is authorized.
- Add focused regression coverage and run relevant [commands](.agents/commands.md).
- Do not opportunistically fix [known issues](.agents/known-issues.md).

Use [`.agents/workflows.md`](.agents/workflows.md). Report skipped or blocked validation honestly.
