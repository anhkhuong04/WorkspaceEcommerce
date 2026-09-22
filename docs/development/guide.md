# Development guide

The root [README](../../README.md) is the shortest setup path. This guide records
the commands and configuration behavior that contributors need to preserve.

## Toolchain

- .NET SDK `10.0.202` from `global.json`
- Node.js `22.18.0` from `.node-version`
- pnpm `10.24.0` through Corepack
- PostgreSQL 17 (local or Docker)
- Docker Desktop/Engine for Compose and integration tests

## First run with Docker

```powershell
Copy-Item .env.example .env
# Replace every required CHANGE_ME value in the ignored .env file.

New-Item -ItemType Directory -Force .certs
dotnet dev-certs https --export-path .certs/workspace-ecommerce-devcert.pfx `
  --password YOUR_CERT_PASSWORD --format pfx

docker compose up -d postgres
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm seed-demo
docker compose up -d api
```

The API is normally available at `http://localhost:5080` and
`https://localhost:5443`. Health endpoints are `/health/live` and
`/health/ready`; Development OpenAPI is `/openapi/v1.json`.

Use `docker compose --profile storage up -d` only when exercising the
S3-compatible MinIO path. Plain `docker compose down` preserves volumes;
`docker compose down -v` destroys local database/media/key data and should be
used only when that reset is intentional.

## Direct backend development

```powershell
Copy-Item src/WorkspaceEcommerce.Api/appsettings.Local.example.json `
  src/WorkspaceEcommerce.Api/appsettings.Local.json

dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet run --project src/WorkspaceEcommerce.Api
```

`appsettings.Local.json` is ignored and loaded only in Development. Environment
variables override JSON configuration. Do not put real secrets in tracked
`appsettings.json`, examples, commands, tickets, or logs. Development can set
`EmailDelivery:Provider=Log`; it records metadata only and does not emit action
links.

## Frontend

```powershell
Push-Location frontend
corepack pnpm install --frozen-lockfile
corepack pnpm dev:storefront   # http://localhost:5173
# or: corepack pnpm dev:admin  # http://localhost:5174
Pop-Location
```

Frontend configuration is build-time `VITE_*` data. `VITE_API_BASE_URL` points
both apps at the API; `VITE_GOOGLE_CLIENT_ID` is public client configuration,
not a secret. Google remains disabled server-side until the allowed audience is
configured.

## EF Core migrations

```powershell
dotnet tool restore

dotnet tool run dotnet-ef migrations add <MigrationName> `
  --project src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj `
  --startup-project src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj

dotnet tool run dotnet-ef database update `
  --project src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj `
  --startup-project src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj

dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj `
  --startup-project src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj
```

Direct EF tooling needs `ConnectionStrings__DefaultConnection` or the ignored
local settings file. Never rewrite an applied migration to make the snapshot
look clean; add a forward migration.

## Configuration ownership

Use [the configuration matrix](../runbooks/configuration-matrix.md) for the
authoritative sections, environment policy, owner, and rotation expectation.
Important groups are `ConnectionStrings`, `AdminAuth`, `Jwt`, `GoogleAuth`,
`TwoFactor`, `CustomerAccountLifecycle`, `EmailDelivery`, `MediaStorage`,
`Warranty`, `MiniLogistics`, `Payment:VNPay`, `Loyalty`, `Cors`,
`ForwardedHeaders`, `RuntimeLimits`, `Storefront`, and telemetry.

Non-Development startup is deliberately fail-closed for placeholder/missing
security and provider configuration. Do not weaken validators to make a deploy
start; fix the deployment authority.

## Common failures

| Symptom | Check |
| --- | --- |
| API cannot connect to PostgreSQL | Database health, connection-string source, mapped port, and whether migration has run |
| Compose rejects interpolation | Required `.env` values still contain/miss provider credentials |
| HTTPS container fails | `.certs/workspace-ecommerce-devcert.pfx` exists and its password matches `.env` |
| Browser receives CORS/cookie failure | Exact storefront/admin origin, credentialed CORS, HTTPS/same-site topology, and `Storefront:BaseUrl` |
| VNPay callback succeeds but browser result is wrong | Public return/IPN URLs, signature secret, stored amount/txnRef, and server logs by trace/order code |
| Shipment remains queued | MiniLogistics base URL/key, provider health, outbox due/dead-letter state, and worker metrics |
| TOTP/session data fails after restart | Persistent shared Data Protection key ring and unchanged application name |
| Media upload is unavailable | size/dimensions, object-store access, lifecycle row, and active NoOp risk metadata outside Development |
| Integration tests cannot start | Docker is running and PostgreSQL Testcontainers can pull/start `postgres:17-alpine` |

Operational recovery belongs in [runbooks](../runbooks/), not ad-hoc database edits.
