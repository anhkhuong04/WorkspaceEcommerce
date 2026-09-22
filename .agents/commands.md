# Development commands

Run from repository root unless noted. Pins: .NET `10.0.202` (`global.json`), Node `22.18.0`, pnpm `10.24.0`.

## Backend

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --configuration Release
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --configuration Release
```

Focused iteration:

```powershell
dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~TargetName"
dotnet test tests/WorkspaceEcommerce.Infrastructure.Tests/WorkspaceEcommerce.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~TargetName"
dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~TargetName"
```

API integration tests require Docker for PostgreSQL Testcontainers. Docker-unavailable fixture errors are environmental blocks, not test outcomes.

## Frontend

Run inside `frontend/`:

```powershell
corepack pnpm install --frozen-lockfile
corepack pnpm lint
corepack pnpm test       # Storefront Vitest only
corepack pnpm typecheck
corepack pnpm build
corepack pnpm dev:storefront
corepack pnpm dev:admin
```

Browser smoke from root: `./scripts/run-prh-015-storefront-e2e.ps1`.

## Local runtime

Use ignored `.env`/`appsettings.Local.json` with non-placeholder values; never copy them into tracked files.

```powershell
dotnet run --project src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj
docker compose --env-file .env up -d postgres api
docker compose --env-file .env --profile storage up -d
docker compose logs -f api
```

Do not run `docker compose down -v` or delete volumes unless explicitly asked.

## EF Core

Design time requires `ConnectionStrings__DefaultConnection` or ignored API local settings.

```powershell
dotnet tool run dotnet-ef migrations add <MigrationName> --project src/WorkspaceEcommerce.Infrastructure --startup-project src/WorkspaceEcommerce.Api --context AppDbContext --output-dir Persistence/Migrations
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/WorkspaceEcommerce.Infrastructure --startup-project src/WorkspaceEcommerce.Api --context AppDbContext
docker compose --env-file .env --profile tools run --rm migrate
docker compose --env-file .env --profile tools run --rm seed-demo
```

Deploy via the one-shot migration job, never API replicas. For migration work, run `./scripts/verify-prh-009-migrations.ps1` when Docker is available.

Full release gates are in `.github/workflows/ci.yml`; run the relevant subset and disclose omissions.
