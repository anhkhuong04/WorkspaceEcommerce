# WorkspaceEcommerce documentation

This directory is the long-lived source of truth for product behavior, domain
rules, architecture decisions, integration boundaries, and operations. The
root `README.md` is the quick start; `.agents/` contains short operational
instructions for coding agents and must not become a second product manual.

When documents conflict, use this order:

1. Executable source, tests, migrations/model snapshot, configuration validators, and CI.
2. Accepted architecture decision records (ADRs).
3. The current-state documents linked below.
4. Historical tickets, generated artifacts, and external-provider documentation.

## Read by intent

| Need | Start here |
| --- | --- |
| Product scope, users, terminology | [Product overview](product/overview.md) |
| Commerce invariants and lifecycle rules | [Business rules](product/business-rules.md) |
| End-to-end customer and operator behavior | [User flows](product/user-flows.md) |
| Components and dependency direction | [Architecture overview](architecture/overview.md) |
| Transactions, locking, snapshots, outbox/inbox | [Data and consistency](architecture/data-and-consistency.md) |
| VNPay, MiniLogistics, email, media, Google, SignalR | [Integrations](architecture/integrations.md) |
| HTTP envelope, errors, auth, paging, localization | [API conventions](api/conventions.md) |
| Local setup, configuration, migrations, troubleshooting | [Development guide](development/guide.md) |
| Test strategy and validation commands | [Testing and quality](development/testing.md) |
| Known limitations and protected areas | [Known limitations](development/known-limitations.md) |
| Production procedures | [Runbooks](runbooks/) |
| Why a significant decision was made | [ADR index](adr/README.md) |

Focused references:

- [Currency and money](currency-and-money.md)
- [Order receipts](order-receipts.md)
- [PostgreSQL query measurement](performance/prh-008-query-plan-runbook.md)
- [Load and resilience testing](performance/prh-016-load-resilience-runbook.md)

## Documentation policy

- Current-state documents describe behavior that exists now. Planned features belong in
  an issue or explicitly labelled proposal, not in the canonical product docs.
- ADRs preserve decision context. Do not silently rewrite an accepted decision; add a
  supersession note or a new ADR.
- Runbooks contain executable operational steps and safety boundaries. Environment
  evidence, credentials, customer data, and generated reports stay outside Git.
- Document rules whose reason or cross-module impact is difficult to infer. Do not mirror
  every controller, DTO, or folder listing.
- A change to money, identity, order state, external side effects, retention, or a public
  API contract must update the relevant document and tests in the same change.
