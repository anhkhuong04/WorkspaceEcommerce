# Deloys.md - Azure Deployment Plan cho WorkspaceEcommerce Ecosystem

Ngay lap: 2026-07-14

Pham vi tai lieu:

- E-commerce trong repo hien tai: `.NET WebAPI + PostgreSQL + React Storefront/Admin`.
- MiniLogistics: `.NET API + Blazor UI + SQL Server`, duoc xem nhu service rieng cua ecosystem.
- OmniRAG: RAG SaaS, duoc xem nhu service rieng can API + database/vector/search.
- Muc tieu: portfolio deploy that, tiet kiem chi phi bang Azure for Students, co du duong nang cap len production-grade.

Nguon Azure da doi chieu:

- Azure for Students: https://azure.microsoft.com/en-us/free/students
- Azure Static Web Apps: https://learn.microsoft.com/en-us/azure/static-web-apps/overview
- Azure Static Web Apps custom domain/SSL: https://learn.microsoft.com/en-us/azure/static-web-apps/custom-domain
- Azure Container Apps: https://learn.microsoft.com/en-us/azure/container-apps/overview
- Azure App Service custom container: https://learn.microsoft.com/en-us/azure/app-service/quickstart-custom-container
- Azure Database for PostgreSQL Flexible Server: https://learn.microsoft.com/en-us/azure/postgresql/overview
- PostgreSQL backup/restore: https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore
- Azure Monitor/Application Insights: https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview
- Azure Static Web Apps + Front Door/CDN: https://learn.microsoft.com/en-us/azure/static-web-apps/front-door-manual

## 1. Ket luan nhanh

Phuong an toi uu cho giai doan portfolio + student budget:

```text
Frontend React Storefront/Admin
-> Azure Static Web Apps

E-commerce API, MiniLogistics API, OmniRAG API
-> Azure Container Apps

Container images
-> Azure Container Registry

E-commerce DB
-> Azure Database for PostgreSQL Flexible Server

MiniLogistics DB
-> Azure SQL Database serverless/dev tier

OmniRAG DB/vector
-> PostgreSQL Flexible Server + pgvector neu duoc bat extension
   hoac Azure AI Search free/small tier cho search layer

Secrets
-> Azure Key Vault hoac Container Apps secrets o giai doan dau

Monitoring
-> Azure Monitor + Application Insights + Log Analytics
```

Khong nen dung AKS o giai doan nay. AKS the hien ky thuat tot nhung chi phi va van hanh lon hon nhieu so voi nhu cau portfolio.

Khong nen dua thang vao "production 100%" theo checklist trong anh. Repo hien tai da la MVP kha day du ve business, nhung ha tang, security hardening, 2FA/RBAC/rate-limit/monitoring/load test chua dat production-grade.

## 2. Azure for Students fit

Azure for Students hien co:

- $100 credit trong 12 thang.
- Khong can credit card.
- Co free monthly amounts cho nhieu dich vu.
- PostgreSQL Flexible Server co muc free 12 thang trong offer hien tai.
- Azure Container Registry co muc free/12 thang trong offer hien tai.
- Static Web Apps co free hosting/SSL/custom domain theo pricing hien tai.

Do do kien truc nen toi uu theo huong:

- Frontend static de gan nhu mien phi.
- API container scale-to-zero hoac min replica thap.
- Managed database nho nhat co backup.
- Chua bat Front Door/WAF neu chua can custom domain production, vi co the ton chi phi.

## 3. Current Codebase Readiness theo checklist

### 3.1 Frontend

Checklist yeu cau:

- Trang chu
- Danh muc
- Tim kiem + filter
- Chi tiet san pham
- Gio hang
- Checkout
- Responsive + mobile first

Trang thai repo:

- Storefront da co routes: home, products, product detail, news/blog, cart, checkout, checkout success, payment result, login, order lookup, account.
- Checkout co shipping quote, coupon, COD/manual transfer/VNPay.
- Admin la app rieng.

Danh gia:

- Business feature: gan du cho MVP/portfolio.
- Production readiness: chua co bang chung Core Web Vitals/PageSpeed, chua co E2E visual/mobile regression test.
- Muc do hien tai: 80-90%, khong nen ghi 100% truoc khi co build audit va mobile QA.

Can lam truoc deploy:

- Chay `corepack pnpm build`, `typecheck`, `lint`.
- Tao `.env.production` cho tung app:
  - `VITE_API_BASE_URL=https://api.<domain>`
  - `VITE_GOOGLE_CLIENT_ID=<prod-client-id>`
- Them fallback routing cho SPA tren Azure Static Web Apps.
- Kiem tra mobile viewport va checkout flow tren domain HTTPS that.

### 3.2 User Management

Checklist yeu cau:

- Dang ky/dang nhap
- Social + OTP
- Quan ly tai khoan, dia chi
- Xac thuc 2 lop

Trang thai repo:

- Customer register/login email/password.
- Google login da co endpoint va frontend env.
- Customer profile/address/order/account pages da co.
- Password hashing va JWT da co.
- Login history cho customer da co.
- Chua thay OTP/2FA.
- Admin login la mot credential tu env, role `Admin` chung.

Danh gia:

- Dat cho MVP/portfolio basic.
- Chua dat "100% production" vi thieu OTP/2FA, refresh token, lockout/brute-force protection, forgot password/email verification, RBAC admin chi tiet.
- Muc do hien tai: 65-75%.

P0 truoc public demo:

- Them rate limit cho login/register/payment/webhook.
- Them account lockout hoac delay/backoff cho login sai.
- Xac nhan Google OAuth production client/domain.

P1 neu muon dat checklist anh:

- Them email verification.
- Them OTP/2FA cho admin va optional cho customer.
- Tach admin user table + RBAC roles: Admin, Ops, Warehouse, Marketing, Support.

### 3.3 Order & Payment

Checklist yeu cau:

- Tao don
- Quan ly don hang
- Tich hop thanh toan VNPay/Momo/COD/OnePay/Stripe...
- Test transaction that

Trang thai repo:

- COD, manual bank transfer, VNPay da co.
- VNPay return/IPN verify HMAC-SHA512, idempotency transaction terminal.
- Payment status va transaction table da co.
- Shipping integration voi MiniLogistics da co nen tang, nhung chua full production.
- Chua co Momo/OnePay/Stripe.

Danh gia:

- Dat cho portfolio Viet Nam neu chon VNPay + COD + manual transfer.
- Chua dat 100% neu checklist bat buoc nhieu gateway va test transaction that.
- Muc do hien tai: 75-85%.

P0 truoc deploy:

- Dung VNPay sandbox config that, khong dung `DEMO`.
- Set ReturnUrl/IpnUrl dung HTTPS public API.
- Smoke test VNPay sandbox end-to-end.
- Hoan thien MiniLogistics webhook secret va endpoint public.

P1:

- Them retry/manual retry shipment.
- Them cancel shipment sync.
- Them tracking timeline UI.
- Them refund/return neu muon flow sau ban hang day du.

### 3.4 Admin/Dashboard

Checklist yeu cau:

- Quan ly san pham
- Quan ly don hang
- Quan ly kho
- Quan ly khach hang
- Bao cao doanh thu co ban
- Role-based access

Trang thai repo:

- Admin co products, categories, coupons, orders, banners, blogs, reviews, dashboard.
- Dashboard co tong orders, revenue completed, new orders, low-stock variants, status summary, recent orders.
- Inventory dang nam trong product variant stock, chua co module warehouse nhap/xuat ton chuyen nghiep.
- Chua thay customer management page trong Admin.
- Chua co RBAC chi tiet, chi `Authorize(Roles = "Admin")`.

Danh gia:

- Dat cho MVP admin.
- Chua dat checklist production 100%.
- Muc do hien tai: 70-80%.

P0 truoc deploy:

- Bao ve admin route voi HTTPS only.
- Doi admin password secret manh.
- Han che CORS theo domain production.

P1:

- Them customer management.
- Them RBAC.
- Them audit log cho admin actions.

### 3.5 Infrastructure

Checklist yeu cau:

- HTTPS/SSL
- CDN
- Load Balancer
- Auto-scale
- Backup & Monitoring
- Sentry/Prometheus/Grafana hoac tuong duong

Trang thai repo:

- Backend co Dockerfile va docker-compose local cho API + PostgreSQL.
- HTTPS dev cert cho Docker local.
- Chua co IaC Bicep/Terraform.
- Chua co GitHub Actions/Azure Pipelines.
- Chua co health check endpoint.
- Chua co Application Insights/Sentry/OpenTelemetry.
- Chua co backup script ngoai managed DB.
- Chua co WAF/CDN/Front Door config.

Danh gia:

- Local infrastructure tot cho dev.
- Production infrastructure chua co.
- Muc do hien tai: 25-35%.

P0 truoc deploy:

- Tao Azure resources bang IaC hoac CLI runbook.
- Them health endpoint: `/health`.
- Them production CORS.
- Them Application Insights.
- Thiet lap DB backup retention.
- Khong expose OpenAPI o Production.

### 3.6 Security & Compliance

Checklist yeu cau:

- OWASP Top 10
- GDPR/PDPA
- PCI-DSS neu luu card
- Rate limiting
- WAF
- Penetration testing

Trang thai repo:

- Co JWT validation issuer/audience/signing key/lifetime.
- Co config validation de chan `CHANGE_ME`.
- Co password hashing.
- Co FluentValidation input validation.
- Co webhook HMAC cho MiniLogistics.
- Khong luu card, payment redirect qua VNPay.
- Chua co rate limiting.
- Chua co security headers/CSP/HSTS explicit trong app.
- Chua co WAF.
- Chua co privacy/data retention/export/delete flow.
- Chua co penetration test.
- JWT luu localStorage tren frontend, chap nhan cho MVP nhung rui ro XSS cao hon httpOnly cookie.

Danh gia:

- MVP security kha hon co ban.
- Chua production compliance.
- Muc do hien tai: 40-55%.

P0 truoc public demo:

- Them ASP.NET Core rate limiting.
- Them security headers: HSTS, X-Content-Type-Options, X-Frame-Options/CSP, Referrer-Policy.
- Dung HTTPS-only.
- Dung Key Vault/Container Apps secrets, khong commit secret.
- Rotate JWT signing key/VNPay secret/MiniLogistics key.

P1:

- WAF qua Azure Front Door Standard/Premium.
- Data privacy page + delete/export account.
- Admin 2FA.
- Security scan dependency va container image.

### 3.7 Performance

Checklist yeu cau:

- Load test >= 1000 concurrent users
- PageSpeed > 90/Core Web Vitals

Trang thai repo:

- Chua thay load test.
- Chua thay Lighthouse/PageSpeed report.
- Frontend Vite/Tailwind co the build static tot, nhung can do that.
- Backend co EF Core queries, nhung nhieu service dang query sync/to-array trong memory; can profile truoc scale.

Danh gia:

- Chua co bang chung performance production.
- Muc do hien tai: 20-30%.

P0 truoc deploy:

- Chay smoke test va simple load test 50-100 concurrent.
- Them DB indexes neu query cham trong admin/product/order.

P1:

- k6/Azure Load Testing.
- Lighthouse CI.
- CDN/Front Door neu co domain production.

## 4. Phuong an deploy duoc khuyen nghi

### Phase 1 - Portfolio demo tiet kiem nhat

Muc tieu:

- Co domain HTTPS public.
- Demo duoc Storefront, Admin, E-commerce API, MiniLogistics, OmniRAG.
- Chi phi nam trong Azure Student credit.
- Van hanh don gian.

Kien truc:

```text
User
  |
  | HTTPS
  v
Azure Static Web Apps
  - storefront.<domain>
  - admin.<domain>
  - minilogistics-ui.<domain> neu Blazor WASM/static
  |
  | API calls HTTPS
  v
Azure Container Apps Environment
  - ca-ecom-api
  - ca-minilogistics-api
  - ca-omnirag-api
  |
  +--> Azure Database for PostgreSQL Flexible Server
  |      - db WorkspaceEcommerce
  |      - db OmniRAG neu dung chung server de tiet kiem
  |
  +--> Azure SQL Database
         - MiniLogistics

Azure Container Registry
  - ecom-api image
  - minilogistics-api image
  - omnirag-api image

Azure Monitor + Application Insights + Log Analytics
```

Ly do chon:

- Static Web Apps phu hop React/Vite, co hosting HTTPS va custom domain.
- Container Apps phu hop nhieu API container, scale linh hoat, it van hanh hon AKS.
- PostgreSQL Flexible Server dung dung stack hien tai.
- Azure SQL dung dung stack MiniLogistics.
- ACR phu hop Dockerfile hien co.

Khuyen nghi replica:

- Portfolio demo tiet kiem: `minReplicas = 0`, `maxReplicas = 2`.
- Neu can webhook/payment on dinh hon: `minReplicas = 1` cho E-commerce API trong thoi gian demo.
- Production-like: `minReplicas = 2`, autoscale HTTP concurrency/CPU.

Trade-off:

- Scale-to-zero co cold start; webhook/payment callback co the cham lan dau.
- Khong co WAF neu chua them Front Door.
- Chua co private networking day du neu lam toi gian chi phi.

### Phase 2 - Production-like portfolio

Them:

- Azure Front Door Standard/Premium + WAF truoc frontend/API.
- Custom domain chuan:
  - `shop.<domain>`
  - `admin.<domain>`
  - `api.<domain>`
  - `logistics.<domain>`
  - `rag.<domain>`
- Azure Key Vault cho secrets.
- Private endpoint/VNet integration cho DB.
- Container Apps min replica 1-2.
- Application Insights OpenTelemetry.
- Azure SignalR Service neu realtime notification can scale.
- Azure Load Testing/k6.
- Backup restore drill.

### Phase 3 - Khong khuyen nghi luc nay: AKS

Chi nen chon AKS khi:

- Ban muon portfolio DevOps/Kubernetes rieng.
- Chap nhan chi phi node.
- Can service mesh, ingress controller, multi-tenant, complex orchestration.

Voi muc tieu hien tai, AKS se lam tang do phuc tap nhieu hon gia tri demo.

## 5. Resource naming de xuat

Dung 1 resource group rieng:

```text
rg-workspace-portfolio-sea
```

Region de xuat:

```text
Southeast Asia
```

Resources:

```text
acrworkspaceportfolio
cae-workspace-portfolio
ca-ecom-api
ca-minilogistics-api
ca-omnirag-api
swa-ecom-storefront
swa-ecom-admin
swa-minilogistics-ui
pg-workspace-portfolio
sqldb-minilogistics
sql-minilogistics-server
kv-workspace-portfolio
appi-workspace-portfolio
log-workspace-portfolio
```

Neu chua co custom domain, dung domain mac dinh cua Azure trong giai doan smoke test.

## 6. E-commerce deployment details

### 6.1 Backend API

Build image:

```powershell
docker build -f src/WorkspaceEcommerce.Api/Dockerfile -t workspace-ecommerce-api:prod .
```

Push vao ACR:

```powershell
az acr login --name acrworkspaceportfolio
docker tag workspace-ecommerce-api:prod acrworkspaceportfolio.azurecr.io/workspace-ecommerce-api:prod
docker push acrworkspaceportfolio.azurecr.io/workspace-ecommerce-api:prod
```

Container App env vars/secrets:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=<Azure PostgreSQL connection string>
AdminAuth__Email=<admin email>
AdminAuth__Password=<strong secret>
Jwt__Issuer=WorkspaceEcommerce
Jwt__Audience=WorkspaceEcommerce.Admin
Jwt__SigningKey=<min 32 bytes random secret>
Jwt__AccessTokenMinutes=60
MiniLogistics__BaseUrl=https://logistics-api.<domain>/api/v1/partner
MiniLogistics__ApiKey=<partner key>
MiniLogistics__WebhookSecret=<webhook secret>
Storefront__BaseUrl=https://shop.<domain>
Payment__VNPay__TmnCode=<sandbox/prod tmn code>
Payment__VNPay__HashSecret=<secret>
Payment__VNPay__PaymentUrl=<VNPay sandbox/prod url>
Payment__VNPay__ReturnUrl=https://api.<domain>/api/payments/vnpay/return
Payment__VNPay__IpnUrl=https://api.<domain>/api/payments/vnpay/ipn
Payment__VNPay__Version=2.1.0
Payment__VNPay__Command=pay
Payment__VNPay__Locale=vn
Payment__VNPay__CurrCode=VND
```

Important:

- Khong can mount dev HTTPS cert tren Azure. TLS nen terminate o Azure ingress/App Gateway/Front Door.
- `Program.cs` hien chi enable CORS trong Development. Neu frontend khac domain voi API, production se bi CORS. Can them production CORS policy doc/implementation truoc deploy.
- `MapOpenApi()` hien chi Development, tot cho production.

### 6.2 Database migration

Hien Dockerfile co stage `migrate` de chay EF migration.

Khuyen nghi:

- Khong chay migration tu app startup.
- Chay migration nhu one-off job trong pipeline/deploy step.
- Backup DB truoc migration.

Run migration container:

```powershell
docker build -f src/WorkspaceEcommerce.Api/Dockerfile --target migrate -t workspace-ecommerce-api-migrate:prod .
docker run --rm `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ConnectionStrings__DefaultConnection="<prod postgres connection>" `
  -e AdminAuth__Email="<admin email>" `
  -e AdminAuth__Password="<admin pass>" `
  -e Jwt__Issuer="WorkspaceEcommerce" `
  -e Jwt__Audience="WorkspaceEcommerce.Admin" `
  -e Jwt__SigningKey="<jwt signing key>" `
  -e Jwt__AccessTokenMinutes="60" `
  workspace-ecommerce-api-migrate:prod
```

Tren Azure nen chay bang:

- GitHub Actions job.
- Azure Container Apps Job.
- Hoac Azure CLI one-off container.

### 6.3 Frontend Storefront/Admin

Build:

```powershell
cd frontend
corepack pnpm install --frozen-lockfile
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
```

Static output can xac nhan:

- `frontend/apps/storefront/dist`
- `frontend/apps/admin/dist`

Production env:

```env
VITE_API_BASE_URL=https://api.<domain>
VITE_GOOGLE_CLIENT_ID=<prod google client id>
```

Azure Static Web Apps can co SPA fallback. De xuat them file:

```text
frontend/apps/storefront/public/staticwebapp.config.json
frontend/apps/admin/public/staticwebapp.config.json
```

Noi dung:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html"
  },
  "globalHeaders": {
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "strict-origin-when-cross-origin"
  }
}
```

## 7. Required code/config changes before deploy

### P0 - bat buoc truoc khi public

1. Production CORS
   - Cho phep `https://shop.<domain>` va `https://admin.<domain>`.
   - Khong dung `AllowAnyOrigin`.

2. Health check
   - Them `/health` de Azure Container Apps probe.
   - Check DB basic connectivity.

3. Rate limiting
   - Login/register: thap.
   - Checkout/payment/webhook: hop ly.
   - Public catalog: cao hon.

4. Security headers/HSTS
   - `UseHsts()` trong Production.
   - CSP cho frontend.

5. Observability
   - Them Azure Monitor OpenTelemetry/Application Insights package/config.
   - Log correlation `TraceIdentifier`, `OrderCode`, `PaymentTxnRef`, `TrackingCode`.

6. Secrets
   - Chuyen tat ca secret vao Azure secrets/Key Vault.
   - Remove `DEMO` fallback cho payment/logistics tren production.

7. Deployment pipeline
   - Build/test backend.
   - Build/typecheck/lint frontend.
   - Build/push image.
   - Run migration job.
   - Deploy container app/static web apps.

8. Smoke test production
   - Catalog load.
   - Register/login.
   - Add cart.
   - Shipping quote.
   - COD checkout.
   - VNPay sandbox checkout.
   - Admin login/order status.
   - MiniLogistics webhook.

### P1 - nen co de portfolio thuyet phuc

1. Admin RBAC thay vi single admin config.
2. Customer email verification + forgot password.
3. Admin 2FA.
4. Shipment retry/cancel/tracking timeline.
5. Application Insights dashboard va alert:
   - 5xx rate
   - p95 latency
   - failed payment callback
   - shipment creation failure
   - DB CPU/storage
6. Backup restore drill.
7. Load test report va Lighthouse report.

### P2 - production-grade

1. Azure Front Door + WAF.
2. Private DB networking.
3. Blue/green deployment hoac Container Apps revision traffic split.
4. Outbox/inbox cho payment/shipment/webhook.
5. Refresh token rotation hoac httpOnly cookie auth.
6. GDPR/PDPA flows: export/delete account, data retention.
7. Penetration test va dependency/container scan.

## 8. CI/CD de xuat

Neu repo tren GitHub, dung GitHub Actions.

Backend workflow:

```text
on push main:
  dotnet restore
  dotnet build
  dotnet test
  docker build API image
  docker push ACR
  run migration job
  update Container App image
```

Frontend workflow:

```text
on push main:
  corepack pnpm install --frozen-lockfile
  corepack pnpm typecheck
  corepack pnpm lint
  corepack pnpm build
  deploy storefront dist to Azure Static Web Apps
  deploy admin dist to Azure Static Web Apps
```

Khuyen nghi:

- Tach staging va production.
- Moi PR chay test/build.
- Main deploy staging.
- Tag/release deploy production.

## 9. Database and backup plan

E-commerce:

- Azure Database for PostgreSQL Flexible Server.
- Bat backup retention toi thieu 7 ngay; production-like 14-35 ngay.
- Test point-in-time restore sang server moi.
- Khong seed demo data vao production that.

MiniLogistics:

- Azure SQL Database.
- Dung serverless/dev tier neu chi portfolio.
- Backup built-in cua Azure SQL.

OmniRAG:

- Neu dung PostgreSQL + pgvector: dung chung PostgreSQL Flexible Server giai doan dau de tiet kiem, nhung tach database/schema.
- Neu dung Azure AI Search: bat free/small tier, gioi han dung luong theo credit.

## 10. Domain plan

Giai doan dau:

```text
swa auto domain cho frontend
container apps auto domain cho API
```

Portfolio public:

```text
shop.<domain>       -> Storefront Static Web App
admin.<domain>      -> Admin Static Web App
api.<domain>        -> E-commerce API Container App
logistics.<domain>  -> MiniLogistics UI/API
rag.<domain>        -> OmniRAG UI/API
```

Production-like:

```text
Azure Front Door
  -> route / or shop.<domain> to Storefront
  -> route admin.<domain> to Admin
  -> route api.<domain> to E-commerce API
  -> WAF managed rules
```

## 11. Environment checklist

E-commerce API:

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ConnectionStrings__DefaultConnection`
- [ ] `AdminAuth__Email`
- [ ] `AdminAuth__Password`
- [ ] `Jwt__Issuer`
- [ ] `Jwt__Audience`
- [ ] `Jwt__SigningKey`
- [ ] `Jwt__AccessTokenMinutes`
- [ ] `MiniLogistics__BaseUrl`
- [ ] `MiniLogistics__ApiKey`
- [ ] `MiniLogistics__WebhookSecret`
- [ ] `Storefront__BaseUrl`
- [ ] `Payment__VNPay__TmnCode`
- [ ] `Payment__VNPay__HashSecret`
- [ ] `Payment__VNPay__PaymentUrl`
- [ ] `Payment__VNPay__ReturnUrl`
- [ ] `Payment__VNPay__IpnUrl`

Storefront:

- [ ] `VITE_API_BASE_URL`
- [ ] `VITE_GOOGLE_CLIENT_ID`

Admin:

- [ ] `VITE_API_BASE_URL`

## 12. Go/No-Go

Go cho portfolio demo sau khi:

- Build backend/frontend pass.
- Azure resources tao thanh cong.
- DB migration chay thanh cong.
- CORS production da sua.
- Health endpoint co.
- VNPay sandbox callback thanh cong tren HTTPS public.
- MiniLogistics webhook thanh cong tren HTTPS public.
- Application Insights nhan log/exception.

No-Go neu:

- Van dung secret `CHANGE_ME`/`DEMO`.
- API production khong co HTTPS public.
- Frontend production bi CORS.
- Payment return/ipn tro ve localhost.
- Khong co backup DB.
- Khong co cach xem logs production.

## 13. Recommended deployment order

1. Sua P0 code/config: CORS production, health check, rate limit, security headers, App Insights.
2. Tao Azure resource group va databases.
3. Tao ACR va push E-commerce API image.
4. Deploy E-commerce API Container App voi env secrets.
5. Chay migration.
6. Deploy Storefront/Admin Static Web Apps.
7. Cau hinh VNPay sandbox URL public.
8. Deploy MiniLogistics API/UI va cau hinh partner API key/webhook.
9. Deploy OmniRAG.
10. Smoke test full ecosystem.
11. Gan custom domain.
12. Neu con credit va can production-like: them Front Door + WAF.

## 14. Final recommendation

Lua chon toi uu hien tai:

```text
Azure Static Web Apps + Azure Container Apps + ACR + PostgreSQL Flexible Server + Azure SQL + Application Insights
```

Day la phuong an can bang nhat giua:

- It chi phi cho Azure for Students.
- Khong can van hanh Kubernetes.
- Phu hop Dockerfile backend hien co.
- Phu hop React/Vite frontend hien co.
- Du kha nang scale va show production-thinking trong portfolio.

Muc tieu nen dat cho lan deploy dau:

- Khong tu nhan production 100%.
- Goi la "production-like portfolio deployment".
- Document ro cac gap con lai: 2FA/RBAC/rate-limit/WAF/load test/security test.
