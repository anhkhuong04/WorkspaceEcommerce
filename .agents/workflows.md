# Workflows

## Core loop

1. **Inspect:** relevant guidance, `git status`, nearby code/tests/configuration/ADR.
2. **Classify:** owning module/layer, observable contract, invariants, security, persistence/provider effects, validation.
3. **Scope:** short plan for non-trivial work; separate required work from optional cleanup.
4. **Implement:** smallest end-to-end change across only necessary layers.
5. **Verify:** narrow test first, then build/typecheck/lint/broader checks proportional to risk; inspect final diff.
6. **Handoff:** outcome, material changes, commands run, blocked/skipped checks, assumptions and risk. Never claim an unrun check passed.

Ask only when evidence cannot resolve a material decision about money, identity, order/fulfillment, warranty, retention, or public contract. Otherwise make and state the smallest evidence-based assumption.

## Feature

- Extend the closest vertical slice; define success/failure behavior before adding abstractions.
- Put invariants in Domain, orchestration/validation/DTOs in Application, persistence/providers in Infrastructure, HTTP in Api.
- Trace transaction/idempotency, stock/snapshots, auth/ownership, outbox/provider work, audit/history, and frontend cache effects.
- Schema changes require a reviewed migration and relevant clean/upgrade validation.
- Add focused tests plus PostgreSQL/API coverage when provider semantics matter.

## Bug fix

- Reproduce or establish concrete evidence; trace UI/HTTP through database/provider and response.
- Define expected behavior from executable code/tests and accepted ADRs, not stale prose.
- Fix the earliest owning layer. Do not mask defects with broad catches, nullable suppression, silent defaults, sleeps, or frontend-only guards.
- Add a regression test; use real integration boundaries for concurrency, translation, constraints, auth, or routing.

## Refactor

- State behavior/contracts to preserve and proof to use.
- Keep it incremental and module-local; do not combine with route/DTO/status/schema/provider/dependency changes unless authorized.
- Do not replace direct EF, outboxes, auth/session, media lifecycle, or warranty identifier design as cleanup.

## Review

- Stay read-only unless fixes are requested.
- Findings first, ordered by severity, with file/line evidence, impact, missing guard, and scoped fix.
- Prioritize correctness, ownership/security, money, consistency/concurrency, provider failure/idempotency, migrations, query shape, tests; style comes later.
- Label verified bug, conditional risk, design debt, or recommendation. Respect intentional ADR decisions.

## Definition of Done

- Complete requested behavior, no unrelated/speculative changes.
- Correct boundaries, validation, ownership, transaction/idempotency, snapshots, and errors.
- Focused coverage exists; relevant checks pass or each omission is reported.
- Migrations/contracts/generated types are reviewed; no secret/local configuration added.
- Final diff inspected; durable developer/operator changes documented; assumptions/risks handed off.
