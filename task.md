# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-08

## Nguyên tắc trước khi làm task mới

- Luôn đọc `overview.md` trước khi thay đổi nghiệp vụ.
- Đọc `.agent/README.md` và rule/skill liên quan trước khi code.
- Không triển khai tính năng ngoài phạm vi `overview.md` nếu chưa được yêu cầu rõ.
- Trước khi sửa code phải nêu kế hoạch, file bị ảnh hưởng và hướng dependency.
- Không sửa file không liên quan.
- Sau khi hoàn thành và test ổn cho mỗi task, commit code ngay với tên commit phù hợp.

## Trạng thái hiện tại

Backend đã có nền tảng Clean Architecture Modular Monolith cho Catalog, Cart, Checkout, Ordering và Content:

- `WorkspaceEcommerce.Domain`: Catalog, Cart và Ordering entities với invariant/domain method cơ bản.
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validators và services cho Admin Auth, Admin Catalog, Admin Product, Admin Banner, Admin Dashboard, Admin Order, Storefront Catalog, Storefront Cart, Checkout và Storefront Order Lookup.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog/Cart/Ordering/Content mappings, migrations, JWT/config validation và transaction-backed checkout store.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Auth, Admin Catalog, Admin Product, Admin Banner, Admin Dashboard, Admin Order, Storefront Catalog, Storefront Cart, Checkout và Storefront Order Lookup; có JWT, response envelope, global exception handling và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: tests cho Domain, validators và Application services thuộc Catalog, Cart, Auth, Product, Storefront Catalog, Checkout, Order Lookup, Admin Order, Admin Banner và Admin Dashboard.
- `WorkspaceEcommerce.Infrastructure.Tests`: tests cho configuration validation, JWT token generation và EF Core mappings của Catalog, Cart, Ordering, Content.
- `WorkspaceEcommerce.Api.IntegrationTests`: hạ tầng API integration test dùng `WebApplicationFactory` và Testcontainers PostgreSQL.
- PostgreSQL local và API backend chạy được bằng Docker Compose service `postgres`, `migrate` và `api`.
- Demo data chạy được bằng Docker Compose service `seed-demo` sau khi apply migration.
- Frontend stack đã chốt: Storefront dùng React + TypeScript + Tailwind CSS + React Hook Form + Zod; Admin dùng React + TypeScript + Ant Design.
- Frontend monorepo đã scaffold trong `frontend/` với Storefront app, Admin app và shared packages.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`, `ICartStore`, `ICheckoutStore`.
- `Infrastructure` phụ thuộc `Application` và `Domain`, triển khai EF Core/PostgreSQL, JWT và config validation.
- `Api` phụ thuộc `Application` và `Infrastructure`, không chứa business logic.

## Đã hoàn thành

Ghi chú duy trì: để giảm dung lượng và độ phức tạp, mục này chỉ giữ 3 task lớn `###` gần nhất. Các task cũ được tóm tắt trong `Trạng thái hiện tại`, `Xác minh gần nhất` và lịch sử commit.

### Demo data seed

- Thêm `IDemoDataSeeder` và `DemoDataSeeder`.
- Seeder chạy bằng API process với argument `--seed-demo`.
- Thêm Docker Compose service `seed-demo` trong profile `tools`.
- Demo seed idempotent, chạy lại không tạo trùng dữ liệu theo bộ seed cố định.
- Demo seed bao gồm:
  - Catalog: 3 categories, 4 products, 5 variants, product images và specifications.
  - Banner: 3 active homepage banners.
  - Cart: cart checkout-ready với session id `demo-checkout-session`.
  - Order: 3 orders demo gồm `Pending`, `Confirmed`, `Completed` và status history tương ứng.
- Demo data có SKU low-stock để phục vụ Dashboard.
- Cập nhật README hướng dẫn chạy `docker compose --profile tools run --rm seed-demo`.

### Frontend scaffold

- Thêm frontend workspace tại `frontend/` dùng pnpm qua Corepack.
- Thêm app Storefront:
  - `frontend/apps/storefront`
  - React + TypeScript + Vite + Tailwind CSS 4.
  - React Router, React Query, React Hook Form và Zod.
  - Layout tone sáng, trắng chủ đạo, clean, hiện đại.
  - Pages scaffold: Home, Product Listing, Product Detail, Cart, Checkout, Order Lookup.
- Thêm app Admin:
  - `frontend/apps/admin`
  - React + TypeScript + Vite + Ant Design.
  - Layout admin sidebar/topbar tối ưu desktop Full HD.
  - Pages scaffold: Login, Dashboard, Categories, Products, Orders, Banners.
- Thêm shared packages:
  - `@workspace-ecommerce/api-types`
  - `@workspace-ecommerce/api-client`
  - `@workspace-ecommerce/shared-utils`
- API calls được gom trong shared `api-client` và app service adapter, không rải trực tiếp trong UI components.
- Cập nhật `.gitignore` và `.dockerignore` để loại `node_modules` và `dist`.
- Cập nhật README hướng dẫn cài dependencies, chạy Storefront/Admin và verify frontend.

### Storefront API integration

- Tích hợp Storefront Home với API thật:
  - `GET /api/categories` để render category links.
  - `GET /api/products` để render featured/live product cards.
- Tích hợp Catalog với API thật:
  - Filter `search`, `categorySlug`, `inStock` lưu trên URL query string.
  - Gọi `GET /api/products` theo filter và pagination.
  - Render đúng backend DTO `compareAtPrice`, `isInStock`, `imageUrl`.
- Tích hợp Product Detail với API thật:
  - Gọi `GET /api/products/{slug}`.
  - Render gallery, variants, specifications.
  - Gọi `POST /api/cart/items` để add selected variant vào cart.
- Tích hợp Cart với API thật:
  - Gọi `GET /api/cart`.
  - Gọi `PUT /api/cart/items/{id}` khi đổi quantity.
  - Gọi `DELETE /api/cart/items/{id}` khi remove item.
  - Quản lý cart session bằng localStorage, mặc định `demo-checkout-session` để kiểm tra seed demo.
- Cập nhật shared API types để khớp contract backend hiện tại của Catalog/Cart.
- Phân tích readiness cho Home demo theo `overview.md`:
  - Home MVP cần banner, danh mục nổi bật và sản phẩm nổi bật.
  - Danh mục nổi bật và sản phẩm nổi bật đã có API thật và frontend đã consume.
  - Banner mới có Admin Banner API; chưa có public Storefront Banner API để Home consume.
  - Seed hiện dùng URL `https://images.example.test/...`, chưa phù hợp demo UI vì ảnh không render thật trong browser.
  - Kết luận: Home hiện đủ kiểm tra category/product API, chưa đủ điều kiện UI/UX hoàn thiện để demo khách hàng.

## Xác minh gần nhất

Đã chạy sau task Storefront API integration:

```powershell
dotnet build WorkspaceEcommerce.slnx
```

Kết quả:

- Build succeeded.
- Warnings: 0.
- Errors: 0.

Đã chạy:

```powershell
cd frontend
corepack pnpm install
corepack pnpm typecheck
corepack pnpm build
corepack pnpm lint
```

Kết quả:

- Frontend dependencies installed bằng pnpm `10.24.0`.
- Typecheck passed cho shared packages, Storefront và Admin.
- Storefront production build passed.
- Admin production build passed.
- Frontend lint passed.
- Admin build có warning chunk lớn do Ant Design bundle; chưa code-split ở scaffold.

Đã chạy:

```powershell
dotnet test .\tests\WorkspaceEcommerce.Application.Tests\WorkspaceEcommerce.Application.Tests.csproj --no-build
dotnet test .\tests\WorkspaceEcommerce.Infrastructure.Tests\WorkspaceEcommerce.Infrastructure.Tests.csproj --no-build
```

Kết quả:

- `WorkspaceEcommerce.Application.Tests`: 119 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 60 passed.
- Failed: 0.
- Skipped: 0.

Đã chạy:

```powershell
docker compose --profile tools config --services
```

Kết quả:

- Compose profile `tools` có đủ service `postgres`, `api`, `migrate`, `seed-demo`.

Đã chạy nhưng bị chặn bởi môi trường:

```powershell
dotnet test .\WorkspaceEcommerce.slnx --no-build
```

Kết quả:

- `WorkspaceEcommerce.Application.Tests`: 119 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 60 passed.
- `WorkspaceEcommerce.Api.IntegrationTests`: failed trước khi chạy test body vì Docker Desktop đang `manually paused`.
- Lỗi Testcontainers: không kết nối được Docker endpoint `npipe://./pipe/docker_engine`.
- Cần chạy lại full suite sau khi unpause Docker Desktop.

Đã chạy:

```powershell
docker compose --profile tools build migrate
docker compose --profile tools run --rm migrate
docker compose build api
docker compose up -d api
```

Kết quả:

- Docker image `workspace-ecommerce-api:local` và `workspace-ecommerce-api-migrate:local` build thành công.
- Migration container chạy thành công; PostgreSQL local đã apply `20260608042918_AddContentBannerSchema`.
- API container start thành công sau khi PostgreSQL healthcheck healthy.
- Admin Banner HTTP smoke-test qua API container:
  - `POST /api/admin/banners`: passed.
  - `GET /api/admin/banners`: passed.
  - `PUT /api/admin/banners/{id}`: passed.
- Admin Dashboard HTTP smoke-test qua API container:
  - `GET /api/admin/dashboard`: passed.
- Đã cleanup banner smoke-test khỏi PostgreSQL local.
- Đã stop/remove API container sau smoke-test; PostgreSQL dev container vẫn giữ nguyên.

Smoke-test đã có:

- Cart 4 endpoints trên API local `http://localhost:5080`: passed.
- Checkout endpoint `POST /api/checkout` trên API local `http://localhost:5080`: passed.
- Checkout smoke-test tạo order `ORD-20260608-8A539E82`.
- Storefront Order Lookup endpoint `GET /api/orders/lookup` trên API local `http://localhost:5080`: passed.
- Lookup đúng `orderCode=ORD-20260608-8A539E82` và `phone=0900000001` trả order snapshot.
- Lookup sai phone trả `404` với response envelope và error `Order was not found.`.
- Admin Order Management endpoints trên API local `http://localhost:5080`: passed.
- Admin login lấy JWT thành công, sau đó gọi:
  - `GET /api/admin/orders`: passed.
  - `GET /api/admin/orders/{id}`: passed.
  - `PUT /api/admin/orders/{id}/status`: passed.
- Admin status update đổi order `ORD-20260608-8A539E82` từ `Pending` sang `Confirmed`.
- API integration infrastructure smoke test dùng Testcontainers PostgreSQL: passed.
- Integration test `GET /api/categories` xác nhận API host khởi động với migrated PostgreSQL container và trả response envelope thành công.
- API integration endpoint coverage dùng Testcontainers PostgreSQL: passed.
- API integration edge case coverage dùng Testcontainers PostgreSQL: passed.
- Integration tests tự động đã cover:
  - `POST /api/admin/auth/login`.
  - Admin endpoint unauthorized response.
  - `GET /api/categories`.
  - `GET /api/products`.
  - `GET /api/products/{slug}`.
  - `POST /api/cart/items`.
  - `POST /api/checkout`.
  - `GET /api/orders/lookup`.
  - `GET /api/admin/orders`.
  - `GET /api/admin/orders/{id}`.
  - `PUT /api/admin/orders/{id}/status`.
- Integration edge cases tự động đã cover validation errors, duplicate conflicts, inactive catalog/cart/checkout failures và invalid order status transition.
- PostgreSQL verification sau admin status update:
  - `ordering.orders`: order status hiện là `Confirmed`.
  - `ordering.order_status_history`: có record `Pending -> Confirmed`, note `Confirmed by admin smoke test`, changed by `admin@example.com`.
- PostgreSQL verification sau checkout:
  - `ordering.orders`: order tồn tại, subtotal `246.90`, total `246.90`, payment method `Cod`.
  - `ordering.order_items`: snapshot SKU `SMOKE-CART-001`, product name `Smoke Test Product`, unit price `123.45`, quantity `2`, line total `246.90`.
  - `ordering.order_status_history`: có record khởi tạo `Pending` với note `Created by checkout.`.
  - `catalog.product_variants`: stock của variant smoke-test còn `8` sau khi trừ quantity `2`.
  - `cart.carts` và `cart.cart_items`: cart/session đã checkout không còn record.

## Commit gần nhất

- `e4b284b Add ordering domain model`
- `37a24b2 Add checkout application service`
- `f4af79d Add ordering persistence and checkout API`
- `9968828 Update checkout runtime verification status`
- `5e1baed Add storefront order lookup`
- `4c1a706 Add admin order management`
- `797e1e7 Add API integration test infrastructure`
- `f795250 Add API integration endpoint coverage`
- `e3814cb Add API integration edge case coverage`
- `a0b204a Add backend Docker Compose setup`
- `07765c2 Document frontend stack decision`
- `1aa9c06 Add banner management and dashboard`
- `8dcc39b Add demo data seeding`

## Rủi ro và khoảng trống

- Vì config dùng placeholder, app sẽ fail sớm nếu chưa override `DefaultConnection`, `AdminAuth` và `Jwt` bằng secret/config local hợp lệ.
- API integration tests đã cover luồng chính và một số edge cases quan trọng cho Auth/Admin authorization, Catalog, Cart, Checkout, Order Lookup và Admin Order; vẫn chưa cover exhaustively mọi biến thể validation/conflict.
- Docker Compose đã chạy API/PostgreSQL/migration local; chưa có production image hardening như non-root user, SBOM, image signing hoặc CI publish.
- Storefront đã tích hợp API thật cho Home/Catalog/Product Detail/Cart; Checkout và Order Lookup vẫn ở mức scaffold/API foundation, chưa hoàn thiện UX end-to-end.
- Home chưa đủ điều kiện demo UI/UX hoàn thiện vì thiếu public Storefront Banner API và demo asset thật; hiện mới đủ kiểm tra category/product API trên Home.
- Admin frontend mới scaffold foundation; chưa hoàn thiện auth guard, CRUD forms, mutation flows hoặc visual polish cuối.
- Admin build hiện có warning chunk lớn do Ant Design; nên code-split routes khi triển khai sâu.
- Docker Desktop đang paused nên chưa smoke-test được `docker compose --profile tools run --rm seed-demo` trong lượt này.
- Dữ liệu smoke-test local cũ đã được insert vào PostgreSQL dev; seed demo mới là idempotent nhưng chưa thay thế cleanup script.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 1 - Frontend integration

1. Unpause Docker Desktop và chạy lại full API integration tests + `seed-demo` smoke-test.
2. Bổ sung điều kiện demo Home:
   - Thêm public Storefront Banner API `GET /api/banners` hoặc endpoint tương đương.
   - Cập nhật shared API types/client cho banner public.
   - Cập nhật seed demo để dùng ảnh demo render được hoặc asset nội bộ.
   - Polish Home UI/UX với hero/banner, featured categories, featured products, loading/error/empty states và responsive Full HD.
   - Smoke-test Home với backend + PostgreSQL + seed demo.
3. Tích hợp Checkout và Order Lookup UI với API thật end-to-end.
4. Tích hợp Admin auth guard và CRUD/list flows cho Dashboard/Categories/Products/Orders/Banners.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
git status --short
```
