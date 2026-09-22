# WorkspaceEcommerce

WorkspaceEcommerce is a full-stack ecommerce modular monolith with an ASP.NET
Core API, PostgreSQL, and separate React storefront/admin applications. It
supports catalog and content management, cart/checkout, coupons, COD/manual
bank transfer/VNPay, MiniLogistics fulfillment, customer accounts/2FA, loyalty,
durable media, PDF receipts, and feature-flagged serialized warranties.

The canonical product and architecture documentation starts at
[`docs/README.md`](docs/README.md). Coding-agent instructions start at
[`AGENTS.md`](AGENTS.md).

## Architecture

```text
React storefront ----+
                     +--> ASP.NET Core API/SignalR --> PostgreSQL 17
React admin ----------+              |
                                    +--> VNPay / MiniLogistics / SMTP / object storage
```

Backend dependency direction is `Api -> Infrastructure/Application -> Domain`.
The frontend is a pnpm workspace with two apps and shared API/client/utility
packages. See the [architecture overview](docs/architecture/overview.md).

## Toolchain

| Tool | Repository version |
| --- | --- |
| .NET SDK | 10.0.202 |
| PostgreSQL | 17 |
| Node.js | 22.18.0 |
| pnpm | 10.24.0 via Corepack |
| Frontend | React 19, TypeScript 6, Vite 8, Tailwind CSS 4 |

Docker Desktop/Engine with Compose is required for the standard local topology
and API integration tests.

## Quick start

Copy the ignored local configuration and replace every required placeholder:

```powershell
Copy-Item .env.example .env
```

Create the HTTPS certificate expected by the API container:

```powershell
New-Item -ItemType Directory -Force .certs
dotnet dev-certs https --export-path .certs/workspace-ecommerce-devcert.pfx `
  --password YOUR_CERT_PASSWORD --format pfx
```

Start PostgreSQL, migrate/seed, then run the API:

```powershell
docker compose up -d postgres
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm seed-demo
docker compose up -d api
```

Default endpoints:

- API: `http://localhost:5080` / `https://localhost:5443`
- Development OpenAPI: `http://localhost:5080/openapi/v1.json`
- Liveness/readiness: `/health/live` and `/health/ready`
- Storefront: `http://localhost:5173`
- Admin: `http://localhost:5174`

Start the frontend apps:

```powershell
Push-Location frontend
corepack pnpm install --frozen-lockfile
corepack pnpm dev:storefront
# In another shell: corepack pnpm dev:admin
Pop-Location
```

For direct `dotnet run`, copy
`src/WorkspaceEcommerce.Api/appsettings.Local.example.json` to the ignored
`appsettings.Local.json`, configure local values, then run:

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet run --project src/WorkspaceEcommerce.Api
```

Full setup, configuration precedence, migrations, and troubleshooting are in
the [development guide](docs/development/guide.md).

## Validation

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --disable-build-servers -m:1
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1

Push-Location frontend
corepack pnpm lint
corepack pnpm test
corepack pnpm typecheck
corepack pnpm build
Pop-Location
```

API integration tests use PostgreSQL 17 Testcontainers and therefore require a
running Docker engine. See [testing and quality](docs/development/testing.md)
for focused tests, migration checks, browser tests, and risk-based expectations.

## Configuration and operations

- Never commit `.env`, `appsettings.Local.json`, certificates, provider secrets,
  tokens, Data Protection keys, or production evidence.
- Compose requires MiniLogistics and VNPay sandbox credentials even when a local
  flow does not exercise them.
- Production startup rejects unsafe placeholder/provider/topology configuration.
  Do not weaken validators to make deployment pass.
- Apply production migrations from one migration job, not every API replica.

Use the [configuration matrix](docs/runbooks/configuration-matrix.md) for config
ownership and the [production release runbook](docs/runbooks/production-release.md)
for release evidence and operational gates.

## Repository map

| Path | Purpose |
| --- | --- |
| `src/WorkspaceEcommerce.Domain` | Entities and invariants |
| `src/WorkspaceEcommerce.Application` | Use cases, DTOs, validation, and ports |
| `src/WorkspaceEcommerce.Infrastructure` | EF/PostgreSQL, providers, storage, workers |
| `src/WorkspaceEcommerce.Api` | HTTP/SignalR boundary and composition root |
| `frontend/apps` | Storefront and admin applications |
| `frontend/packages` | Shared API types/client and utilities |
| `tests` | Application, infrastructure, and API integration tests |
| `docs` | Product, architecture, API, development, ADRs, and runbooks |
| `scripts` | Repeatable validation, migration, performance, and release checks |
