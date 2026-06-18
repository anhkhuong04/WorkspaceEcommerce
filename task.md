# Task - Customer Account + Checkout + Coupon

Ngay cap nhat: 2026-06-18

## Muc tieu

Hoan thien cac feature lien quan truc tiep den customer purchase flow:

1. Customer account/dashboard MVP.
2. Checkout end-to-end co customer context, van giu guest checkout.
3. Coupon cho checkout: Admin tao/quan ly coupon, user nhap va dung ma giam gia khi dat hang.

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
- P5 da hoan thanh:
  - Application tests da cover customer register/login/profile validation, customer order authorization, authenticated checkout gan `CustomerId`, guest checkout tao order `CustomerId = null`.
  - API integration tests da cover customer register/login/me/orders, checkout happy path, checkout conflict/not found flows, authenticated checkout order xuat hien trong customer dashboard.
  - Frontend storefront typecheck va build da pass.
- P6 da hoan thanh:
  - Da them coupon domain enum/entity: `CouponDiscountType`, `Coupon`, `CouponProductTarget`, `CouponRedemption`.
  - Da them domain methods activate/deactivate, update details, validate effective window, calculate discount, reserve usage, product target eligibility.
  - Da mo rong `Order` voi coupon snapshot fields va `ApplyCoupon`.
  - Da them DbSet/IQueryable, EF configurations, migration `AddCouponSchema`, model configuration tests va coupon domain tests.
- P7 da hoan thanh:
  - Da them application module `Modules/Coupons` voi DTO/request/validators/service cho admin coupon.
  - Da them admin API `GET/POST/PUT/PATCH/DELETE /api/admin/coupons`.
  - Da ho tro list/search/filter active/effective date, get detail, create, update, activate/deactivate.
  - Delete coupon chua dung se xoa; coupon da co redemption/order usage se deactivate thay vi xoa.
  - Da validate code unique normalized, discount value/type, date range, usage limit, product target ids ton tai va unique.
- P8 da hoan thanh:
  - Da them public endpoint `POST /api/checkout/coupons/validate`.
  - `CheckoutRequest` da co `CouponCode` optional va checkout response/order lookup expose coupon snapshot fields.
  - Checkout transaction validate lai coupon, tinh eligible subtotal theo product targets, apply discount vao order snapshot.
  - Checkout thanh cong ghi `CouponRedemption`, increment `UsedCount`, tru stock va xoa cart trong cung transaction.
  - Invalid/inactive/expired/not eligible/min subtotal tra validation; usage limit reached tra conflict.
- P9 da hoan thanh:
  - `api-types` da co coupon discount type, admin coupon DTO/request, checkout coupon validate request/response.
  - `CheckoutRequest` va `OrderDto` frontend da expose coupon code/snapshot fields.
  - Customer/admin order detail types da co coupon snapshot fields de UI hien thi khi backend expose.
  - `api-client` da co storefront `validateCheckoutCoupon`, admin coupon CRUD/status methods va HTTP `PATCH`.

## Viec con lai

- Customer Account + Checkout MVP da xong.
- Coupon P6-P9 da xong; tiep theo la P10-P11 cho admin/storefront UI.
- Neu can release voi full-suite xanh, xu ly rieng loi cu cua `DemoDataSeeder_SeedsExpectedDataAndIsIdempotent` trong API integration suite.

## Feature moi - Coupon cho san pham

### Muc tieu Coupon MVP

- Admin co the tao, sua, bat/tat va xem danh sach coupon.
- User storefront co the nhap coupon trong checkout, xem discount truoc khi submit va dat hang voi coupon do.
- Coupon phai duoc validate lai trong transaction checkout; khong tin vao discount tinh tu frontend.
- Order phai luu snapshot coupon/discount de lich su don hang khong thay doi khi admin sua coupon sau nay.
- Guest checkout va customer checkout deu dung duoc coupon.

### Quyet dinh de xuat cho MVP

- Moi checkout chi ap dung 1 coupon, khong stacking.
- Coupon code la unique case-insensitive; backend normalize uppercase/trim.
- Ho tro 2 kieu giam gia:
  - `Percentage`: giam theo phan tram tren subtotal eligible, co `MaxDiscountAmount` optional.
  - `FixedAmount`: giam so tien co dinh, khong vuot qua subtotal eligible.
- Coupon co thoi gian hieu luc `StartsAt`/`EndsAt`, `IsActive`, `UsageLimit` optional va `MinimumSubtotal` optional.
- Scope san pham:
  - MVP nen ho tro `AllProducts` va `Product` targets.
  - Co the them `Category` target neu khong lam tang qua nhieu UI/API complexity.
  - Discount chi tinh tren line items eligible; neu gio hang khong co san pham eligible thi tra validation error.
- Redemption/usage chi ghi nhan sau khi checkout thanh cong trong cung transaction voi order.
- Khi coupon het luot do concurrent checkout, checkout tra `409 Conflict`.
- Chua lam coupon auto-suggest, coupon stacking, free shipping, rule theo payment method, rule theo customer segment, rule theo first order trong MVP.

### Data model de xuat

- Them module domain `Coupons` hoac `Promotions`.
- `Coupon`:
  - `Id`, `Code`, `Name`, `Description`
  - `DiscountType`, `DiscountValue`, `MaxDiscountAmount`
  - `MinimumSubtotal`
  - `StartsAt`, `EndsAt`, `IsActive`
  - `UsageLimit`, `UsedCount`
  - `CreatedAt`, `UpdatedAt`
- `CouponProductTarget`:
  - `Id`, `CouponId`, `ProductId`
  - unique `(CouponId, ProductId)`
- Neu chon category scope: `CouponCategoryTarget` voi unique `(CouponId, CategoryId)`.
- `CouponRedemption`:
  - `Id`, `CouponId`, `OrderId`, `CustomerId`, `CustomerPhone`, `CodeSnapshot`, `DiscountAmount`, `RedeemedAt`
  - unique `OrderId` de dam bao mot order chi co mot redemption.
- Mo rong `Order`:
  - `CouponId`
  - `CouponCodeSnapshot`
  - `CouponNameSnapshot`
  - tiep tuc dung `DiscountAmount` va `TotalAmount` hien co.
- EF migration:
  - schema co the la `promotions` hoac `ordering` tuy muc so huu.
  - can index unique normalized coupon code.
  - can FK coupon/product/order/customer voi delete restrict.

### Backend/API plan

#### P6 - Coupon domain + persistence - da hoan thanh

- Them enum `CouponDiscountType`.
- Them entity `Coupon`, target entity va redemption entity.
- Them DbSet/IQueryable vao `AppDbContext` va `IAppDbContext`.
- Them EF configurations, migration, model configuration tests.
- Them domain methods:
  - activate/deactivate,
  - update details,
  - validate effective window,
  - calculate discount tu eligible subtotal,
  - reserve/increment usage trong checkout transaction.

#### P7 - Admin coupon API - da hoan thanh

- Them application module `Modules/Coupons`.
- DTO/request:
  - `AdminCouponDto`
  - `AdminCouponListRequest`
  - `CreateCouponRequest`
  - `UpdateCouponRequest`
- Service:
  - list/search/filter active/date,
  - get detail,
  - create,
  - update,
  - activate/deactivate,
  - delete chi cho coupon chua co redemption; neu da dung thi deactivate thay vi delete.
- Controller:
  - `GET /api/admin/coupons`
  - `GET /api/admin/coupons/{id}`
  - `POST /api/admin/coupons`
  - `PUT /api/admin/coupons/{id}`
  - `PATCH /api/admin/coupons/{id}/status`
  - `DELETE /api/admin/coupons/{id}` neu safe.
- Validate:
  - code required/max length/allowed chars/unique,
  - discount value hop le theo type,
  - date range hop le,
  - usage limit > used count,
  - target product/category ton tai va active warning/validation tuy quyet dinh.

#### P8 - Storefront coupon evaluate + checkout integration - da hoan thanh

- Them public endpoint validate coupon truoc checkout:
  - `POST /api/checkout/coupons/validate`
  - request gom `sessionId`, `couponCode`.
  - response gom normalized code, discount amount, eligible subtotal, subtotal, total after discount, message.
- Mo rong `CheckoutRequest` them `CouponCode`.
- `CheckoutService` flow moi:
  - load cart,
  - build item snapshots,
  - neu co coupon code thi load coupon voi lock/transaction,
  - validate active/date/min subtotal/targets/usage,
  - tinh discount tu item snapshots,
  - tao order voi coupon snapshot va discount amount,
  - ghi `CouponRedemption`,
  - increment `UsedCount`,
  - tru stock, xoa cart, save.
- API error mapping:
  - invalid/inactive/expired/not eligible => 400 validation,
  - usage limit exhausted/concurrent race => 409 conflict,
  - missing cart/items => existing checkout behavior.

#### P9 - Frontend shared types/client - da hoan thanh

- Cap nhat `api-types`:
  - coupon admin DTO/request,
  - checkout coupon validate request/response,
  - order DTO coupon snapshot fields.
- Cap nhat `api-client`:
  - storefront `validateCheckoutCoupon`,
  - admin coupon CRUD methods.
- Cap nhat order/customer/admin order types hien thi coupon fields.

#### P10 - Admin UI

- Them nav item va route `/coupons`.
- Coupon list:
  - search code/name,
  - filter active/inactive,
  - columns code/name/type/value/date/usage/status/actions.
- Coupon create/edit modal/page:
  - code, name, description,
  - discount type/value/max discount,
  - min subtotal,
  - starts/ends,
  - active toggle,
  - usage limit,
  - product target picker.
- UX rules:
  - coupon da co redemption thi canh bao khi sua rule anh huong don sau, lich su don cu khong doi.
  - delete disabled neu da co redemption; de deactivate.

#### P11 - Storefront checkout UI

- Kich hoat o coupon hien dang disabled trong checkout summary.
- User nhap coupon, bam apply:
  - goi validate endpoint,
  - hien applied coupon, discount amount, total after discount,
  - co remove coupon.
- Khi cart quantity thay doi:
  - revalidate coupon hoac clear coupon voi message ro rang.
- Khi submit checkout:
  - gui `couponCode`,
  - neu backend reject coupon thi hien error va giu user o checkout.
- Checkout success/order detail/admin order detail/customer order detail hien coupon code snapshot va discount.

### Test plan Coupon

- Domain/Application tests:
  - normalize/unique code,
  - percentage/fixed discount,
  - max discount cap,
  - minimum subtotal,
  - product target eligibility,
  - inactive/not started/expired,
  - usage limit exhausted,
  - checkout luu `CouponId`, `CouponCodeSnapshot`, `DiscountAmount`, `CouponRedemption`,
  - guest checkout va customer checkout deu dung coupon duoc.
- API integration tests:
  - admin CRUD coupon,
  - duplicate code returns validation,
  - validate coupon happy path,
  - validate coupon invalid/expired/not eligible,
  - checkout happy path voi coupon,
  - checkout conflict khi coupon het luot,
  - order lookup/customer orders/admin orders expose coupon snapshot.
- Frontend checks:
  - `corepack pnpm --filter @workspace-ecommerce/api-types typecheck`
  - `corepack pnpm --filter @workspace-ecommerce/api-client typecheck`
  - `corepack pnpm --filter @workspace-ecommerce/shared-utils typecheck`
  - `corepack pnpm --filter @workspace-ecommerce/admin typecheck`
  - `corepack pnpm --filter @workspace-ecommerce/admin build`
  - `corepack pnpm --filter @workspace-ecommerce/storefront typecheck`
  - `corepack pnpm --filter @workspace-ecommerce/storefront build`
- Backend checks:
  - `dotnet build WorkspaceEcommerce.slnx`
  - `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Coupon`
  - `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Coupon`
  - `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Checkout`

## Verification da chay

- `dotnet build WorkspaceEcommerce.slnx`
- `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Checkout`
- `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Customer`
- `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter "Checkout|Customer"`
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Customer` pass 5/5.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Checkout` pass 5/5.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Infrastructure.Tests/WorkspaceEcommerce.Infrastructure.Tests.csproj --filter Coupon` pass 29/29.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Infrastructure.Tests/WorkspaceEcommerce.Infrastructure.Tests.csproj --filter Ordering` pass 20/20.
- 2026-06-18: `dotnet build WorkspaceEcommerce.slnx` pass sau P6 Coupon domain + persistence.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Coupon` pass 11/11.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Coupon` pass 4/4.
- 2026-06-18: `dotnet build WorkspaceEcommerce.slnx` pass sau P7 Admin coupon API.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Checkout` pass 15/15.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Checkout` pass 8/8.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --filter Coupon` pass 15/15 sau P8.
- 2026-06-18: `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --filter Coupon` pass 7/7 sau P8.
- 2026-06-18: `dotnet build WorkspaceEcommerce.slnx` pass sau P8 checkout coupon integration.
- 2026-06-18: `corepack pnpm --filter @workspace-ecommerce/api-types typecheck` pass sau P9.
- 2026-06-18: `corepack pnpm --filter @workspace-ecommerce/api-client typecheck` pass sau P9.
- 2026-06-18: `corepack pnpm --filter @workspace-ecommerce/shared-utils typecheck` pass sau P9.
- 2026-06-18: `corepack pnpm --filter @workspace-ecommerce/admin typecheck` pass sau P9.
- 2026-06-18: `corepack pnpm --filter @workspace-ecommerce/storefront typecheck` pass sau P9.
- `corepack pnpm --filter @workspace-ecommerce/api-types typecheck`
- `corepack pnpm --filter @workspace-ecommerce/storefront typecheck`
- `corepack pnpm --filter @workspace-ecommerce/storefront build`
- HTTP smoke voi Vite storefront:
  - `GET /checkout`
  - `GET /checkout/success?orderCode=SMOKE-P4&phone=0900000000`
  - `GET /cart`
  - `GET /login`

Ghi chu: full API integration suite tung fail o `DemoDataSeeder_SeedsExpectedDataAndIsIdempotent` do fixture categories hien co khong con dung ky vong cu. Loi nay nam ngoai P0-P5.

## Ngoai pham vi MVP

- Payment gateway online.
- Shipping fee engine.
- Advanced promotion engine: coupon stacking, auto-apply promotion, free shipping coupon, customer segment coupon, first-order coupon, payment-method coupon.
- VAT invoice schema.
- Email/SMS notification.
- Loyalty, warranty, B2B, installation workflow.
- Guest order claim/link flow.
- Address book nhieu dia chi.
- Refactor Catalog/Product/Admin ngoai cac diem can de checkout/customer chay dung.

Ghi chu: API integration tests can Docker Desktop/PostgreSQL Testcontainers hoat dong.
