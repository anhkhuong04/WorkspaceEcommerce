# Startup - WorkspaceEcommerce

Tai lieu nay mo ta cach khoi dong MVP tren may local. Backend API va PostgreSQL chay bang Docker Compose; frontend hien chay bang Vite local vi repo chua co Dockerfile cho Storefront/Admin.

## 1. Yeu cau

- Docker Desktop hoac Docker Engine co Docker Compose.
- .NET SDK 10 de tao HTTPS dev certificate.
- Node/Corepack neu can chay frontend local.
- PowerShell tai root repo: `D:\Projects\WorkspaceEcommerce`.

## 2. Cau hinh `.env`

Neu chua co `.env`, tao tu file mau:

```powershell
Copy-Item .env.example .env
```

Kiem tra cac bien bat buoc trong `.env`:

```env
POSTGRES_DB=workspace_ecommerce_dev
POSTGRES_USER=workspace_ecommerce
POSTGRES_PASSWORD=<local-password>
POSTGRES_PORT=5432
API_PORT=5080
API_HTTPS_PORT=5443
ASPNETCORE_HTTPS_CERT_PASSWORD=<local-cert-password>
ASPNETCORE_ENVIRONMENT=Development
AdminAuth__Email=admin@example.com
AdminAuth__Password=<admin-password>
Jwt__Issuer=WorkspaceEcommerce
Jwt__Audience=WorkspaceEcommerce.Admin
Jwt__SigningKey=<at-least-32-bytes>
Jwt__AccessTokenMinutes=60
```

`ASPNETCORE_HTTPS_CERT_PASSWORD` phai khop voi password dung de tao file `.certs/workspace-ecommerce-devcert.pfx`.

## 3. Tao HTTPS certificate cho Docker API

Tao thu muc cert va export dev certificate:

```powershell
New-Item -ItemType Directory -Force -Path .\.certs | Out-Null
dotnet dev-certs https -ep .\.certs\workspace-ecommerce-devcert.pfx -p '<ASPNETCORE_HTTPS_CERT_PASSWORD trong .env>'
```

Neu trinh duyet hoac PowerShell can trust certificate tren may host:

```powershell
dotnet dev-certs https --trust
```

`.certs/` da duoc ignore va khong nen commit.

## 4. Khoi dong backend HTTPS bang Docker

Chay PostgreSQL:

```powershell
docker compose up -d postgres
```

Chay migration:

```powershell
docker compose --profile tools run --rm migrate
```

Seed demo data neu database sach hoac can bo sung demo data:

```powershell
docker compose --profile tools run --rm seed-demo
```

Khoi dong API voi HTTP va HTTPS:

```powershell
docker compose up -d --build api
```

Endpoints sau khi API chay:

```text
HTTP:  http://localhost:5080
HTTPS: https://localhost:5443
OpenAPI HTTPS: https://localhost:5443/openapi/v1.json
```

Kiem tra nhanh:

```powershell
Invoke-WebRequest -Uri https://localhost:5443/openapi/v1.json -UseBasicParsing
```

Neu certificate chua duoc trust, dung trinh duyet chap nhan dev certificate hoac chay `dotnet dev-certs https --trust`.

## 5. Khoi dong frontend local

Cai dependency neu can:

```powershell
cd frontend
corepack pnpm install
```

Neu muon frontend goi API HTTPS, tao file `frontend/apps/storefront/.env`:

```env
VITE_API_BASE_URL=https://localhost:5443
VITE_CART_SESSION_ID=demo-checkout-session
```

Tao file `frontend/apps/admin/.env`:

```env
VITE_API_BASE_URL=https://localhost:5443
```

Khoi dong Storefront:

```powershell
cd frontend
corepack pnpm dev:storefront
```

Khoi dong Admin:

```powershell
cd frontend
corepack pnpm dev:admin
```

Frontend URLs:

```text
Storefront: http://localhost:5173
Admin:      http://localhost:5174
```

## 6. Kiem tra trang thai va logs

Xem container:

```powershell
docker compose ps
```

Xem logs API:

```powershell
docker compose logs -f api
```

Xem logs PostgreSQL:

```powershell
docker compose logs -f postgres
```

## 7. Dung chuong trinh

Dung API va PostgreSQL nhung giu volume database:

```powershell
docker compose down
```

Dung va xoa volume PostgreSQL de setup lai database sach:

```powershell
docker compose down -v
```

Chi dung frontend Vite local thi quay lai terminal dang chay Vite va bam `Ctrl+C`.

## 8. Ghi chu troubleshooting

- Neu `seed-demo` loi duplicate key, database local da co du lieu cu. API van co the chay tren database hien tai. Neu can demo tu dau, dung `docker compose down -v`, sau do chay lai `postgres`, `migrate`, `seed-demo`, `api`.
- Neu port `5432`, `5080` hoac `5443` bi chiem, doi `POSTGRES_PORT`, `API_PORT`, `API_HTTPS_PORT` trong `.env`.
- Neu HTTPS khong load do certificate, chay lai buoc tao cert va `dotnet dev-certs https --trust`.
- Frontend hien khong chay bang Docker trong repo nay; chi backend API va PostgreSQL chay Docker Compose.
