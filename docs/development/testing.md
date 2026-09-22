# Testing and quality

Validation should match the risk of the change and use the repository's real
boundaries. Do not replace PostgreSQL-specific behavior with an in-memory
database test.

## Test layers

| Layer | Project/tool | Purpose |
| --- | --- | --- |
| Domain/application | `WorkspaceEcommerce.Application.Tests` | Invariants, validators, use-case results, and cross-module business effects |
| Persistence/provider | `WorkspaceEcommerce.Infrastructure.Tests` | EF model metadata, provider adapters, configuration, storage/PDF behavior |
| HTTP/database | `WorkspaceEcommerce.Api.IntegrationTests` | Real ASP.NET host plus PostgreSQL 17 Testcontainers, migrations, auth, envelopes, and concurrent flows |
| Storefront unit | Vitest | Client/session/i18n/formatting and focused component/service behavior |
| Browser smoke | Playwright | Isolated storefront safety and critical browser flows |
| Load/resilience | k6 scripts | Approved-environment latency, capacity, and dependency-failure evidence |

## Standard validation

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --disable-build-servers -m:1
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1
dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj `
  --startup-project src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj `
  --no-build

Push-Location frontend
corepack pnpm install --frozen-lockfile
corepack pnpm lint
corepack pnpm test
corepack pnpm typecheck
corepack pnpm build
Pop-Location
```

For a focused iteration, use `dotnet test <project> --filter
FullyQualifiedName~<namespace-or-test>` or the owning frontend package. Before
handoff, run all affected layers rather than only the new test.

## Required cases for risky changes

- Money/checkout/coupon/stock: success, invalid state, boundary values, rollback,
  and two concurrent attempts for the last stock/coupon use.
- Payment/webhook/outbox: invalid authentication, amount mismatch, duplicate,
  concurrent retry, terminal replay, provider timeout, and crash-safe enqueue.
- Auth/2FA/session: generic enumeration-safe errors, expiry, replay, concurrent
  refresh, family revocation, and protected-data/key-ring behavior.
- Ownership: anonymous, wrong customer, correct customer, and admin role cases.
- EF/model changes: configuration assertions, migration/model parity, clean
  create, and the supported upgrade path.
- Frontend contract changes: shared API types/client plus loading, empty, error,
  auth-expiry, localization, and responsive behavior as applicable.

Assertions should verify durable state and absence of forbidden side effects,
not just HTTP status. Tests must not contain real credentials or depend on
production services.

## Additional gates

- `./scripts/verify-prh-009-regressions.ps1` runs focused concurrency/payment/
  loyalty/shipment regressions.
- `./scripts/verify-prh-009-migrations.ps1` checks clean and upgrade migrations.
- `./scripts/verify-prh-010-container.ps1` validates migration image and probes.
- `./scripts/scan-tracked-runtime-secrets.ps1` checks tracked configuration.
- Query and load procedures are under [performance](../performance/).
- The full production gate is [production-release.md](../runbooks/production-release.md).

Generated results belong under ignored `artifacts/`; they are evidence for a
specific candidate, not evergreen documentation.
