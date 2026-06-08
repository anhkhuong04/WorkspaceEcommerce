# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-08

## Nguyên tắc trước khi làm task mới

- Luôn đọc `overview.md` trước khi thay đổi nghiệp vụ.
- Đọc `.agent/README.md` và rule/skill liên quan trước khi code.
- Không triển khai tính năng ngoài phạm vi `overview.md` nếu chưa được yêu cầu rõ.
- Trước khi sửa code phải nêu kế hoạch, file bị ảnh hưởng và hướng dependency.
- Không sửa file không liên quan.
- Sau khi hoàn thành và test ổn cho mỗi task, commit code ngay với tên commit phù hợp.
- Mục `Đã hoàn thành` chỉ giữ 3 task lớn `###` gần nhất để giảm dung lượng; task cũ được tóm tắt ở trạng thái hiện tại, xác minh gần nhất và lịch sử commit.

## Trạng thái hiện tại

Backend đã có nền tảng Clean Architecture Modular Monolith cho Catalog, Cart, Checkout, Ordering và Content:

- `WorkspaceEcommerce.Domain`: Catalog, Cart và Ordering entities với invariant/domain method cơ bản.
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validators và services cho Admin Auth, Admin Catalog, Admin Product, Admin Banner, Admin Dashboard, Admin Order, Storefront Catalog, Storefront Cart, Checkout, Storefront Order Lookup và Storefront Banner.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog/Cart/Ordering/Content mappings, migrations, JWT/config validation và transaction-backed checkout store.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Auth, Admin Catalog, Admin Product, Admin Banner, Admin Dashboard, Admin Order, Storefront Catalog, Storefront Cart, Checkout, Storefront Order Lookup và Storefront Banners; có JWT, response envelope, global exception handling, local Development CORS cho frontend dev servers và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: tests cho Domain, validators và Application services thuộc Catalog, Cart, Auth, Product, Storefront Catalog, Checkout, Order Lookup, Admin Order, Admin Banner và Admin Dashboard.
- `WorkspaceEcommerce.Infrastructure.Tests`: tests cho configuration validation, JWT token generation và EF Core mappings của Catalog, Cart, Ordering, Content.
- `WorkspaceEcommerce.Api.IntegrationTests`: hạ tầng API integration test dùng `WebApplicationFactory` và Testcontainers PostgreSQL.
- PostgreSQL local và API backend chạy được bằng Docker Compose service `postgres`, `migrate`, `api`, `seed-demo`.
- Frontend stack đã chốt: Storefront dùng React + TypeScript + Tailwind CSS + React Hook Form + Zod; Admin dùng React + TypeScript + Ant Design.
- Frontend monorepo đã scaffold trong `frontend/` với Storefront app, Admin app và shared packages.
- Storefront đã tích hợp API thật cho Home, Catalog, Product Detail, Cart, Checkout và Order Lookup.
- Admin portal đã có login/logout, JWT session persistence, protected routes và xử lý unauthorized/expired session ở mức MVP.
- Home demo hiện có đủ dữ liệu thật theo `overview.md`: banners, featured categories và featured products.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`, `ICartStore`, `ICheckoutStore`.
- `Infrastructure` phụ thuộc `Application` và `Domain`, triển khai EF Core/PostgreSQL, JWT và config validation.
- `Api` phụ thuộc `Application` và `Infrastructure`, không chứa business logic.
- Frontend dùng shared `@workspace-ecommerce/api-client` và `@workspace-ecommerce/api-types`; UI pages không gọi `fetch` trực tiếp.

## Đã hoàn thành

### Demo Image Assets & Seeder Update

- Thêm demo assets thật cho banners và products vào `frontend/apps/storefront/public/demo` và `frontend/apps/admin/public/demo`.
- Cập nhật `DemoDataSeeder.cs` dùng URL relative `/demo/...` thay vì URL giả external.
- Bổ sung logic update existing seeded `ProductImage` và `Banner` records để database cũ cũng chuyển sang URL `/demo/...` khi chạy lại `seed-demo`.
- Commit riêng assets: `22df442 Add demo image assets`.
- Commit riêng seed update: `39d1904 Update demo seed image records`.

### Storefront Home UI/UX Polish

- Thêm `BannerCarousel.tsx`: auto-slide, arrows, dots, pause on hover, progress bar, skeleton và empty state.
- Thêm `ProductCard.tsx`: image hover scale, badge nổi bật/hết hàng, compare-at-price, category label và hover lift.
- Viết lại `HomePage.tsx` để dùng API thật cho banners, categories và featured products.
- Thêm category pills, responsive skeletons, CTA strip và layout sáng/clean phù hợp demo Full HD.
- Fix lint phần Home/Banner, gồm lỗi unused variable trong `BannerCarousel.tsx`.
- Thêm CSS `banner-progress` keyframe và `.scrollbar-hidden` utility trong `globals.css`.
- Commit riêng: `285c5b6 Polish storefront home demo`.

### Admin Auth & Route Protection

- Cập nhật Admin Login UI dùng React Hook Form + Zod, Ant Design form controls, loading state và API error state.
- Tích hợp `POST /api/admin/auth/login` qua shared Admin API client.
- Lưu JWT session vào `localStorage` với `accessToken`, `tokenType`, `expiresAt`, `email`.
- Gắn Authorization header tự động qua `ApiClient.getAccessToken`.
- Thêm `AdminAuthProvider`, `useAdminAuth` và `ProtectedRoute`.
- Bảo vệ Dashboard/Categories/Products/Orders/Banners; truy cập khi chưa login redirect về `/login` và sau login quay lại route ban đầu.
- Thêm logout trong Admin header, clear session và clear React Query cache.
- Xử lý token hết hạn/unauthorized: session hết hạn bị clear theo timeout; API `401` gọi callback clear session.
- Cập nhật API client để hỗ trợ `onUnauthorized` callback.
- Cập nhật backend Development CORS cho frontend dev origins `5173/5174` để browser gọi API local được.

## Xác minh gần nhất

Đã chạy backend build:

```powershell
dotnet build WorkspaceEcommerce.slnx
```

Kết quả:

- Build succeeded.
- Warnings: 0.
- Errors: 0.

Đã chạy frontend checks trong `frontend/`:

```powershell
corepack pnpm typecheck
corepack pnpm build
corepack pnpm lint
```

Kết quả:

- Typecheck passed cho `api-types`, `api-client`, `shared-utils`, `storefront`, `admin`.
- Production build passed cho Storefront và Admin.
- Admin build có warning chunk lớn do Ant Design; chưa phải lỗi build.
- Lint passed cho Storefront và Admin.

Đã chạy Docker/API verification cho Admin auth:

```powershell
docker compose build api
docker compose up -d api
```

Kết quả:

- API Docker image build thành công.
- API container restart thành công sau PostgreSQL healthcheck.
- `POST /api/admin/auth/login`: success, có token, expiry và email.
- `GET /api/admin/dashboard` không token: `401`.
- `GET /api/admin/dashboard` với bearer token: success.

Đã chạy UI smoke-test bằng Admin dev server + Playwright headless Edge:

- Truy cập `/orders` khi chưa login redirect về `/login`.
- Login thành công bằng admin credential local và quay lại `/orders`.
- Session được lưu vào `localStorage` sau login.
- Logout quay về `/login`.
- Session bị xóa khỏi `localStorage` sau logout.

Smoke-test/API/test coverage đã có từ các task trước:

- Cart, Checkout, Order Lookup, Admin Order Management smoke-test qua API local: passed.
- PostgreSQL verification sau checkout: order, order items, status history, stock trừ đúng, cart checkout đã clear/remove đúng.
- API integration infrastructure với Testcontainers PostgreSQL: passed.
- Integration coverage cho Auth/Admin authorization, Catalog, Cart, Checkout, Order Lookup, Admin Order và edge cases chính: passed.
- Full `dotnet test WorkspaceEcommerce.slnx` gần nhất đã passed khi Docker Desktop hoạt động bình thường.

## Commit gần nhất

- `06ff8ec Add storefront banner API`
- `22df442 Add demo image assets`
- `39d1904 Update demo seed image records`
- `285c5b6 Polish storefront home demo`

## Rủi ro và khoảng trống

- Config dùng placeholder nên app sẽ fail sớm nếu chưa override `DefaultConnection`, `AdminAuth` và `Jwt` bằng secret/config local hợp lệ.
- API integration tests đã cover luồng chính và một số edge cases quan trọng, nhưng chưa cover exhaustively mọi biến thể validation/conflict.
- Docker Compose đã chạy API/PostgreSQL/migration/seed local; chưa có production image hardening như non-root user, SBOM, image signing hoặc CI publish.
- Storefront Home đã đủ điều kiện demo dữ liệu thật, nhưng visual polish cuối vẫn có thể tiếp tục nâng cấp khi chốt branding.
- Admin auth hiện lưu JWT bằng `localStorage` theo MVP; production nên đánh giá lại threat model, token lifetime, refresh/session strategy và cookie/httpOnly nếu cần.
- Admin frontend đã có auth guard nhưng các màn Categories/Products/Orders/Banners vẫn chủ yếu là scaffold/list foundation; chưa hoàn thiện CRUD/mutation flows.
- Admin build hiện có warning chunk lớn do Ant Design; nên code-split routes khi triển khai sâu.
- Dữ liệu smoke-test local cũ có thể vẫn còn trong PostgreSQL dev; seed demo idempotent nhưng chưa có cleanup/reset script riêng cho demo.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 1 - Admin operational flows

Mục tiêu: Admin xử lý được các nghiệp vụ MVP chính trong `overview.md` sau khi đã có auth guard.

1. Dashboard: rà lại loading/error/empty states và polish dữ liệu thật từ `GET /api/admin/dashboard`.
2. Banners: list/create/update/activate/deactivate/sort order.
3. Categories: list/create/update/activate/deactivate, parent-child UX.
4. Products và variants: list/create/update, active toggle, stock/price/compare-at/requires-installation.
5. Orders: list/detail/status transition, render status history và note.

### Ưu tiên 2 - Backend/API gaps theo overview

Mục tiêu: đóng các khoảng trống khiến Admin Product Management chưa đúng đủ mô tả MVP.

1. Bổ sung Admin Product Image API nếu cần quản lý ảnh sản phẩm từ Admin UI.
2. Bổ sung Admin Product Specification API nếu cần quản lý thông số kỹ thuật từ Admin UI.
3. Thêm tests cho image/specification service, validation và API integration.
4. Tích hợp image/specification flows vào Admin Product UI.

### Ưu tiên 3 - Demo readiness end-to-end

Mục tiêu: có kịch bản demo ổn định từ setup sạch.

1. Chạy Docker Compose từ clean volume: `postgres` -> `migrate` -> `seed-demo` -> `api`.
2. Smoke-test Storefront: Home -> Catalog -> Product Detail -> Cart -> Checkout -> Success -> Order Lookup.
3. Smoke-test Admin: Login -> Dashboard -> Products/Banners -> Orders -> Status update.
4. Chạy full suite: `dotnet test WorkspaceEcommerce.slnx`.
5. Cập nhật README/demo script với lệnh chạy và dữ liệu demo cần dùng.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
git status --short
```