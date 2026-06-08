# WorkspaceEcommerce

Backend MVP for the WorkspaceEcommerce modular monolith.

## Run API and PostgreSQL with Docker Compose

Prerequisites:

- Docker Desktop or Docker Engine with Compose.
- A local `.env` file based on `.env.example`.

Create local configuration:

```powershell
Copy-Item .env.example .env
```

Edit `.env` before starting containers:

- Set `POSTGRES_PASSWORD`.
- Set `AdminAuth__Password`.
- Set `Jwt__SigningKey` to a non-placeholder value with at least 32 bytes.
- Optionally change `POSTGRES_PORT` and `API_PORT`.

Start PostgreSQL:

```powershell
docker compose up -d postgres
```

Apply database migrations:

```powershell
docker compose --profile tools run --rm migrate
```

Seed demo data:

```powershell
docker compose --profile tools run --rm seed-demo
```

The demo seed is idempotent and includes Catalog, Banner, a checkout-ready Cart, and sample Orders.
Use this session id to smoke-test checkout:

```text
demo-checkout-session
```

Start the API:

```powershell
docker compose up -d api
```

The API listens on:

```text
http://localhost:5080
```

If `ASPNETCORE_ENVIRONMENT=Development`, OpenAPI is available at:

```text
http://localhost:5080/openapi/v1.json
```

View logs:

```powershell
docker compose logs -f api
```

Stop services:

```powershell
docker compose down
```

Remove the PostgreSQL volume when you need a clean database:

```powershell
docker compose down -v
```

## Local verification

Build and test all projects:

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
```

## Frontend

The frontend workspace lives in `frontend/` and uses pnpm through Corepack.

Install dependencies:

```powershell
cd frontend
corepack pnpm install
```

Run Storefront:

```powershell
corepack pnpm dev:storefront
```

Run Admin:

```powershell
corepack pnpm dev:admin
```

Verify frontend:

```powershell
corepack pnpm typecheck
corepack pnpm build
corepack pnpm lint
```
