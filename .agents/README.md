# Agent guide

WorkspaceEcommerce is a .NET 10/PostgreSQL ecommerce modular monolith with React/Vite storefront and admin apps. It covers catalog, cart/checkout, orders, coupons, VNPay, MiniLogistics, customer accounts/2FA, loyalty, media, blogs/reviews, receipts, and feature-flagged warranties.

## Source-of-truth order

1. Current user request and constraints.
2. Source, tests, migrations/model snapshot, configuration validators, and CI.
3. Current-state product/architecture docs, accepted ADRs, and runbooks under `docs/`.
4. Root README and historical ticket prose, which may lag code.

`overview.md` is not required. If evidence conflicts, preserve behavior for fixes/refactors; request a product decision only when the choice materially changes money, identity, fulfillment, warranty, retention, or a public contract.

## Repository map

| Path | Purpose |
|---|---|
| `src/WorkspaceEcommerce.Domain` | Entities and invariants |
| `src/WorkspaceEcommerce.Application` | Use cases, DTOs, validation, ports |
| `src/WorkspaceEcommerce.Infrastructure` | EF/PostgreSQL, providers, hosted workers |
| `src/WorkspaceEcommerce.Api` | Composition root and HTTP/SignalR boundary |
| `tests/*` | Application, Infrastructure, API integration tests |
| `frontend/apps/*` | Storefront and admin React apps |
| `frontend/packages/*` | API types/client and domain-neutral utilities |
| `docs/adr`, `docs/runbooks` | Accepted decisions and operations |

## Task routing

- Any code change: `architecture.md`, `standards.md`, `workflows.md`.
- Business/payment/auth/shipping/warranty: also `domain.md` and relevant ADR/tests.
- Database/migration or frontend: relevant `standards.md` section plus `commands.md`.
- Work near debt: `known-issues.md`.

Load only relevant guidance; prefer repository evidence over generic framework advice.
