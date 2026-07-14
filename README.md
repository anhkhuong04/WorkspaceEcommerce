# WorkspaceEcommerce

> A full-stack e-commerce platform built with a **modular monolith** backend (.NET) and a **pnpm monorepo** frontend (React + Vite). Supports catalog management, cart & checkout, VNPay payment, MiniLogistics shipping integration, and a dedicated admin panel.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [1. Configure Environment](#1-configure-environment)
  - [2. Generate HTTPS Dev Certificate](#2-generate-https-dev-certificate)
  - [3. Run with Docker Compose](#3-run-with-docker-compose)
  - [4. Apply Migrations & Seed Data](#4-apply-migrations--seed-data)
- [Frontend](#frontend)
  - [Install Dependencies](#install-dependencies)
  - [Run Dev Servers](#run-dev-servers)
  - [Verify Frontend](#verify-frontend)
- [API Reference](#api-reference)
- [Testing](#testing)
- [Environment Variables](#environment-variables)
- [Contributing](#contributing)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                      Frontend (pnpm)                    │
│  ┌─────────────────┐       ┌─────────────────────────┐  │
│  │   Storefront    │       │      Admin Panel        │  │
│  │  (React + Vite) │       │    (React + Vite)       │  │
│  └────────┬────────┘       └───────────┬─────────────┘  │
└───────────┼───────────────────────────┼────────────────┘
            │ REST API                  │ REST API
┌───────────▼───────────────────────────▼────────────────┐
│                  .NET API (ASP.NET Core)                │
│  ┌──────────┐  ┌─────────────┐  ┌──────────────────┐   │
│  │  Domain  │  │ Application │  │  Infrastructure  │   │
│  └──────────┘  └─────────────┘  └──────────────────┘   │
└─────────────────────────┬───────────────────────────────┘
                          │
         ┌────────────────┴───────────────┐
         │         PostgreSQL 17          │
         └────────────────────────────────┘
                          │ Webhooks / API
         ┌────────────────┴───────────────┐
         │      MiniLogistics Partner     │
         └────────────────────────────────┘
```

The backend follows **Clean Architecture** with four layers: `Domain`, `Application`, `Infrastructure`, and `Api`. Each business capability (Catalog, Orders, Payments, Logistics) is organized as an independent module within the monolith.

---

## Tech Stack

| Layer                | Technology                     |
| -------------------- | ------------------------------ |
| **Backend**          | .NET 9 / ASP.NET Core          |
| **Database**         | PostgreSQL 17                  |
| **ORM / Migrations** | Entity Framework Core          |
| **Authentication**   | JWT Bearer                     |
| **Payment**          | VNPay                          |
| **Logistics**        | MiniLogistics Partner API      |
| **Frontend**         | React 19 + TypeScript + Vite 8 |
| **Frontend Tooling** | pnpm 10 (via Corepack), ESLint |
| **Containerization** | Docker / Docker Compose        |

---

## Project Structure

```
WorkspaceEcommerce/
├── src/
│   ├── WorkspaceEcommerce.Api/           # Entry point, controllers, middleware
│   ├── WorkspaceEcommerce.Application/   # Use cases, DTOs, interfaces
│   ├── WorkspaceEcommerce.Domain/        # Entities, aggregates, domain events
│   └── WorkspaceEcommerce.Infrastructure/# EF Core, repositories, external services
├── frontend/
│   ├── apps/
│   │   ├── storefront/                   # Customer-facing shop
│   │   └── admin/                        # Back-office admin panel
│   └── packages/                         # Shared UI / utility packages
├── tests/                                # Integration & unit tests
├── docs/                                 # Feature specs and architecture notes
├── docker-compose.yml
├── .env.example
└── WorkspaceEcommerce.slnx
```

---

## Prerequisites

| Tool                                                              | Minimum Version | Notes                                |
| ----------------------------------------------------------------- | --------------- | ------------------------------------ |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 24+             | Or Docker Engine + Compose plugin    |
| [.NET SDK](https://dotnet.microsoft.com/download)                 | 9.0             | For local development without Docker |
| [Node.js](https://nodejs.org/)                                    | 22+             | Required for Corepack / pnpm         |
| [pnpm](https://pnpm.io/)                                          | via Corepack    | `corepack enable` to activate        |

---

## Getting Started

### 1. Configure Environment

Copy the example environment file and fill in the required values:

```powershell
Copy-Item .env.example .env
```

Open `.env` and update the following required values:

| Variable                         | Description                                       |
| -------------------------------- | ------------------------------------------------- |
| `POSTGRES_PASSWORD`              | Strong password for the PostgreSQL database       |
| `AdminAuth__Password`            | Password for the built-in admin account           |
| `Jwt__SigningKey`                | Secret key — **must be at least 32 bytes**        |
| `ASPNETCORE_HTTPS_CERT_PASSWORD` | Password used when generating the dev certificate |

> **Optional:** `POSTGRES_PORT` (default `5432`) and `API_PORT` (default `5080`) can be changed if the ports are already in use.

---

### 2. Generate HTTPS Dev Certificate

The API container expects a `.pfx` certificate in the `.certs/` directory for local HTTPS. Generate one with:

```powershell
# Create the directory
New-Item -ItemType Directory -Force .certs

# Generate the certificate (replace YOUR_CERT_PASSWORD with the value from .env)
dotnet dev-certs https --export-path .certs/workspace-ecommerce-devcert.pfx `
  --password YOUR_CERT_PASSWORD --format pfx
```

---

### 3. Run with Docker Compose

Start the database:

```powershell
docker compose up -d postgres
```

Start the API (the database will be started automatically as a dependency):

```powershell
docker compose up -d api
```

View live logs:

```powershell
docker compose logs -f api
```

Stop all services:

```powershell
docker compose down
```

Reset the database (removes the PostgreSQL volume):

```powershell
docker compose down -v
```

---

### 4. Apply Migrations & Seed Data

Apply all pending database migrations:

```powershell
docker compose --profile tools run --rm migrate
```

Seed demo data (catalog, banners, a checkout-ready cart, and sample orders):

```powershell
docker compose --profile tools run --rm seed-demo
```

> The seed command is **idempotent** — safe to run multiple times. Use the session ID `demo-checkout-session` to smoke-test the checkout flow.

---

## Frontend

The frontend is a **pnpm monorepo** managed via Corepack. It contains two applications:

- **Storefront** — the customer-facing shop
- **Admin** — the back-office admin panel

### Install Dependencies

```powershell
cd frontend
corepack enable   # only needed once
corepack pnpm install
```

### Run Dev Servers

```powershell
# Storefront (default: http://localhost:5173)
corepack pnpm dev:storefront

# Admin panel (default: http://localhost:5174)
corepack pnpm dev:admin
```

### Verify Frontend

```powershell
corepack pnpm typecheck   # TypeScript type checking
corepack pnpm lint        # ESLint
corepack pnpm build       # Production build
```

---

## API Reference

When running in `Development` mode, the OpenAPI specification is available at:

| Format    | URL                                      |
| --------- | ---------------------------------------- |
| JSON spec | `http://localhost:5080/openapi/v1.json`  |
| HTTPS     | `https://localhost:5443/openapi/v1.json` |

Import the JSON spec into [Postman](https://www.postman.com/), [Insomnia](https://insomnia.rest/), or any OpenAPI-compatible client to explore and test all endpoints.

---

## Testing

Build and run all backend tests:

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
```

Run tests with detailed output:

```powershell
dotnet test WorkspaceEcommerce.slnx --logger "console;verbosity=detailed"
```

---

## Environment Variables

Full reference for all variables in `.env.example`:

| Variable                         | Default                                           | Required | Description                            |
| -------------------------------- | ------------------------------------------------- | -------- | -------------------------------------- |
| `POSTGRES_DB`                    | `workspace_ecommerce_dev`                         | ✅       | Database name                          |
| `POSTGRES_USER`                  | `workspace_ecommerce`                             | ✅       | Database user                          |
| `POSTGRES_PASSWORD`              | —                                                 | ✅       | Database password                      |
| `POSTGRES_PORT`                  | `5432`                                            |          | Host port for PostgreSQL               |
| `API_PORT`                       | `5080`                                            |          | Host port for the HTTP API             |
| `API_HTTPS_PORT`                 | `5443`                                            |          | Host port for the HTTPS API            |
| `ASPNETCORE_ENVIRONMENT`         | `Development`                                     |          | `Development` or `Production`          |
| `ASPNETCORE_HTTPS_CERT_PASSWORD` | —                                                 | ✅       | Password for the dev HTTPS certificate |
| `AdminAuth__Email`               | `admin@example.com`                               | ✅       | Admin account email                    |
| `AdminAuth__Password`            | —                                                 | ✅       | Admin account password                 |
| `Jwt__Issuer`                    | `WorkspaceEcommerce`                              | ✅       | JWT issuer claim                       |
| `Jwt__Audience`                  | `WorkspaceEcommerce.Admin`                        | ✅       | JWT audience claim                     |
| `Jwt__SigningKey`                | —                                                 | ✅       | JWT signing secret (min. 32 bytes)     |
| `Jwt__AccessTokenMinutes`        | `60`                                              |          | Token expiry in minutes                |
| `MiniLogistics__BaseUrl`         | `http://host.docker.internal:5221/api/v1/partner` |          | MiniLogistics API base URL             |
| `MiniLogistics__ApiKey`          | `ml_demo_partner_key_123456`                      |          | MiniLogistics API key                  |
| `MiniLogistics__WebhookSecret`   | `minilogistics_webhook_secret_dev`                |          | Webhook verification secret            |
| `Payment__VNPay__TmnCode`        | `DEMO`                                            |          | VNPay terminal code                    |
| `Payment__VNPay__HashSecret`     | `DEMO_SECRET`                                     |          | VNPay hash secret                      |

> ⚠️ **Never commit your `.env` file.** It is already listed in `.gitignore`.

---

## Contributing

1. **Fork** the repository and create a feature branch:
   ```powershell
   git checkout -b feature/your-feature-name
   ```
2. **Make your changes** and ensure all tests pass.
3. **Verify the frontend** builds without errors.
4. **Open a Pull Request** against `main` with a clear description of your changes.

Please follow the existing code style and include tests for any new functionality.

---
