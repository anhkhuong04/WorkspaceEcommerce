# Task - Customer Account + Checkout

Ngay cap nhat: 2026-06-17

## Muc tieu

Hoan thien 2 feature lien quan truc tiep:

1. Customer account/dashboard MVP.
2. Checkout end-to-end co customer context, van giu guest checkout.

## Quyet dinh MVP

- Giu guest checkout song song voi customer checkout.
- Email la dinh danh dang nhap chinh; phone la contact bat buoc cho giao hang.
- Neu customer da dang nhap khi checkout, order phai gan `CustomerId`.
- Khong tu dong link guest order cu theo email/phone trong MVP.
- Guest order lookup van dung `orderCode + phone`.
- Customer dashboard chi xem profile/order/status timeline; khong cap nhat order status.
- Admin order management van la nguon van hanh don hang.

## Trang thai hien tai

- P0 da hoan thanh: customer domain/persistence, DbSet/config/migration, password hasher, customer JWT, current customer context va DI.
- P1 da hoan thanh: customer register/login/me/update profile, customer orders API, role guard `Customer`, rule chi xem/sua data cua minh.
- P2 da hoan thanh: checkout doc `ICurrentCustomerContext.CustomerId`, guest checkout khong bi break, authenticated checkout tao order co `CustomerId`, order DTO expose `customerId`.
- P3 da hoan thanh:
  - `api-types` va shared API client da co customer auth/profile/orders types va methods.
  - Storefront da co customer auth provider, local session/token, `me`, login/register/logout, unauthorized handler va token injection vao `ApiClient`.
  - `/login` khong con static: da goi API register/login that va redirect ve account/route truoc do.
  - Da them route guard va routes `/account`, `/account/profile`, `/account/orders`, `/account/orders/:id`.
  - Dashboard MVP da co overview profile/contact, update profile, order history, order detail, status timeline, link mua hang/order lookup.
  - Checkout success page hien CTA ve dashboard order detail khi order co `customerId`.
- P4 da hoan thanh:
  - Checkout prefill contact tu customer profile khi da login.
  - Checkout success navigate kem `orderCode` va `phone` tren query string.
  - Checkout success co the refresh va lookup lai order bang query string.
  - Manual bank transfer co instruction truoc submit va panel sau submit de copy order code/noi dung chuyen khoan.
  - Checkout CTA doi theo payment method: COD va manual transfer.
  - `/checkout` co empty/expired cart state thay vi redirect im lang.
  - Storefront cart provider co `resetCartSession()` de clear header/drawer state va rotate cart session sau checkout.

## Viec con lai

### P5 - Test va verification bo sung

- Application tests:
  - customer register/login/profile validation,
  - customer order authorization,
  - authenticated checkout gan `CustomerId`,
  - guest checkout van tao order `CustomerId = null`.
- API integration tests:
  - customer register/login/me/orders,
  - checkout happy path,
  - checkout stock conflict,
  - authenticated checkout order xuat hien trong customer dashboard.
- Frontend checks:
  - `corepack pnpm --filter @workspace-ecommerce/storefront typecheck`
  - `corepack pnpm --filter @workspace-ecommerce/storefront build`
- Backend checks:
  - `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Checkout`
  - `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Customer`
  - `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Customer`
  - `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Checkout`

## Verification da chay

- `dotnet build WorkspaceEcommerce.slnx`
- `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Checkout`
- `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Customer`
- `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter "Checkout|Customer"`
- `corepack pnpm --filter @workspace-ecommerce/api-types typecheck`
- `corepack pnpm --filter @workspace-ecommerce/storefront typecheck`
- `corepack pnpm --filter @workspace-ecommerce/storefront build`
- HTTP smoke voi Vite storefront:
  - `GET /checkout`
  - `GET /checkout/success?orderCode=SMOKE-P4&phone=0900000000`
  - `GET /cart`
  - `GET /login`

Ghi chu: full API integration suite tung fail o `DemoDataSeeder_SeedsExpectedDataAndIsIdempotent` do fixture categories hien co khong con dung ky vong cu. Loi nay nam ngoai P0-P3.

## Ngoai pham vi MVP

- Payment gateway online.
- Coupon/shipping fee engine.
- VAT invoice schema.
- Email/SMS notification.
- Loyalty, warranty, B2B, installation workflow.
- Guest order claim/link flow.
- Address book nhieu dia chi.
- Refactor Catalog/Product/Admin ngoai cac diem can de checkout/customer chay dung.

Ghi chu: API integration tests can Docker Desktop/PostgreSQL Testcontainers hoat dong.
