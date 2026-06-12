# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-12

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
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validators và services cho Admin Auth, Admin Catalog, Admin Product, Admin Product Image/Specification, Admin Banner, Admin Dashboard, Admin Order, Storefront Catalog, Storefront Cart, Checkout, Storefront Order Lookup và Storefront Banner.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog/Cart/Ordering/Content mappings, migrations, JWT/config validation và transaction-backed checkout store.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Auth, Admin Catalog, Admin Product, Admin Product Image/Specification, Admin Banner, Admin Dashboard, Admin Order, Storefront Catalog, Storefront Cart, Checkout, Storefront Order Lookup và Storefront Banners; có JWT, response envelope, global exception handling, local Development CORS cho frontend dev servers và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: tests cho Domain, validators và Application services thuộc Catalog, Cart, Auth, Product, Product Image/Specification, Storefront Catalog, Checkout, Order Lookup, Admin Order, Admin Banner và Admin Dashboard.
- `WorkspaceEcommerce.Infrastructure.Tests`: tests cho configuration validation, JWT token generation và EF Core mappings của Catalog, Cart, Ordering, Content.
- `WorkspaceEcommerce.Api.IntegrationTests`: hạ tầng API integration test dùng `WebApplicationFactory` và Testcontainers PostgreSQL.
- PostgreSQL local và API backend chạy được bằng Docker Compose service `postgres`, `migrate`, `api`, `seed-demo`.
- Frontend stack hiện tại: Storefront và Admin đều dùng React + TypeScript + Tailwind CSS; forms dùng React Hook Form + Zod; server state dùng React Query.
- Frontend monorepo đã scaffold trong `frontend/` với Storefront app, Admin app và shared packages.
- Storefront đã tích hợp API thật cho Home, Catalog, Product Detail, Cart, Checkout và Order Lookup.
- Admin portal đã có login/logout, JWT session persistence, protected routes và xử lý unauthorized/expired session ở mức MVP.
- Admin operational flows đã có UI thao tác MVP chính: Dashboard, Banners, Categories, Products/Variants và Orders.
- Admin UI hardening sau khi bỏ Ant Design đã hoàn thành ở mức MVP: modal/drawer có focus trap, focus restore, phím `Escape`; Product asset delete có confirm; Order detail/status workflow đã được làm rõ hơn.
- Admin Product backend/API đã hỗ trợ CRUD ảnh sản phẩm và thông số kỹ thuật; shared frontend API types/client đã có contract tương ứng.
- Admin Product UI đã tích hợp list/create/update/delete cho ảnh sản phẩm và thông số kỹ thuật.
- Home demo hiện có đủ dữ liệu thật theo `overview.md`: banners, featured categories và featured products.
- Storefront header navigation và login page đã được polish ở mức UI hiện tại; social login placeholder và asset imports cũ đã được gỡ để phù hợp asset cleanup.
- Browser manual verification cho Admin Product asset flows và Storefront Product Detail đã hoàn tất theo cập nhật 2026-06-11.
- Demo readiness end-to-end đã hoàn tất theo cập nhật 2026-06-11: setup sạch, smoke-test Storefront/Admin, full suite và tài liệu/demo script ở mức MVP.

## Tiến độ MVP theo `overview.md`

Ước tính hiện tại: **100% tiêu chí chức năng MVP đã hoàn thành**, **khoảng 90-95% demo readiness/operational hardening tổng thể**.

Đối chiếu tiêu chí hoàn thành MVP trong `overview.md`:

- Admin tạo được danh mục, sản phẩm và SKU: hoàn thành.
- Khách xem được sản phẩm trên storefront: hoàn thành.
- Khách thêm sản phẩm vào giỏ: hoàn thành.
- Khách checkout thành công: hoàn thành.
- Hệ thống tạo đơn hàng có mã đơn: hoàn thành.
- Admin xem và cập nhật trạng thái đơn: hoàn thành.
- Khách tra cứu được đơn hàng: hoàn thành.
- Tồn kho được cập nhật cơ bản: hoàn thành.
- Source code chia module rõ ràng: hoàn thành.
- Có seed data để demo: hoàn thành.

Phần còn lại không còn là blocker chức năng MVP. Trọng tâm tiếp theo là nâng Admin Dashboard từ màn hình số liệu cơ bản thành màn hình vận hành rõ ràng, nhất quán và có hành động trực tiếp; browser automation và theo dõi bundle tiếp tục là hardening không chặn MVP.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`, `ICartStore`, `ICheckoutStore`.
- `Infrastructure` phụ thuộc `Application` và `Domain`, triển khai EF Core/PostgreSQL, JWT và config validation.
- `Api` phụ thuộc `Application` và `Infrastructure`, không chứa business logic.
- Frontend dùng shared `@workspace-ecommerce/api-client` và `@workspace-ecommerce/api-types`; UI pages không gọi `fetch` trực tiếp.
- Admin frontend không còn phụ thuộc Ant Design; dùng Tailwind/native controls và local UI primitives.

## Đã hoàn thành

### Admin UI hardening

- Hoàn tất hardening chính sau khi bỏ Ant Design trong commit `08af060`.
- `Modal` và `Drawer` đã có focus trap, focus restore, đóng bằng phím `Escape` và semantics dialog cơ bản.
- Product image/specification delete đã có confirm dialog và trạng thái pending.
- Order detail chuyển sang drawer, hiển thị snapshot, status transition và status history rõ ràng hơn.
- Loading, disabled, validation và API error state đã được chuẩn hóa ở các flow Admin chính ở mức MVP.

### Demo readiness end-to-end

- Hoàn tất kịch bản demo từ setup sạch theo cập nhật 2026-06-11.
- Luồng setup mục tiêu: `postgres` -> `migrate` -> `seed-demo` -> `api`.
- Smoke-test Storefront mục tiêu đã hoàn tất: Home -> Catalog -> Product Detail -> Cart -> Checkout -> Success -> Order Lookup.
- Smoke-test Admin mục tiêu đã hoàn tất: Login -> Dashboard -> Products/Banners -> Orders -> Status update.
- Full suite và README/demo script đã được cập nhật hoặc xác minh ở mức MVP theo cập nhật 2026-06-11.

### Browser Manual Verification

- Hoàn tất kiểm tra thao tác click/form trực tiếp trên trình duyệt thật theo cập nhật 2026-06-11.
- Admin Product asset flows đã được kiểm tra: create/update/delete image và specification.
- Storefront Product Detail đã được kiểm tra để xác nhận image/spec hiển thị đúng khi dữ liệu được giữ lại.
- Xác nhận luồng browser cho phần đã thêm ở Admin Product Asset UI không còn là blocker MVP.

## Xác minh gần nhất

Theo kiểm tra code ngày 2026-06-12:

- Admin UI hardening đã hoàn thành và được loại khỏi danh sách nhiệm vụ tiếp theo.
- Commit `08af060` đã bổ sung accessibility/focus behavior cho modal/drawer, confirm delete cho Product assets và hoàn thiện Order drawer/status workflow.
- Admin Dashboard contract đã được chốt: giữ bốn số liệu MVP, bổ sung `lowStockThreshold`, summary đủ 7 trạng thái đơn và 5 đơn gần nhất.
- `totalRevenue` được khóa bằng test là tổng đơn `Completed`; `newOrders` là số đơn `Pending`.
- Dashboard UI đã dùng `lowStockThreshold` từ API, không còn hard-code ngưỡng tồn kho.
- Dashboard query đã được tối ưu qua `IAdminDashboardQuery`: EF Core aggregate/project trực tiếp tại PostgreSQL bằng async query và `AsNoTracking`, không còn materialize toàn bộ Orders/Products/ProductVariants trong Application service.
- Low-stock query join và project trực tiếp sang DTO, lọc threshold và `Take(10)` trong database; recent orders được sắp xếp `CreatedAt DESC, Id DESC` và `Take(5)` trong database.
- `DashboardController` giữ nguyên route `GET /api/admin/dashboard`, authorization và response envelope.

Đã chạy cho Admin Dashboard contract:

```powershell
dotnet test .\tests\WorkspaceEcommerce.Application.Tests\WorkspaceEcommerce.Application.Tests.csproj --filter "FullyQualifiedName~AdminDashboardServiceTests"
dotnet test .\tests\WorkspaceEcommerce.Api.IntegrationTests\WorkspaceEcommerce.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AdminDashboardIntegrationTests"
dotnet build WorkspaceEcommerce.slnx
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
```

Kết quả:

- Admin Dashboard Application tests passed: 4 tests.
- Admin Dashboard API integration tests passed: 3 tests.
- Backend build passed, warnings 0, errors 0.
- Frontend typecheck, lint và production build passed.

Đã chạy cho tối ưu Admin Dashboard Application/API:

```powershell
dotnet test .\tests\WorkspaceEcommerce.Application.Tests\WorkspaceEcommerce.Application.Tests.csproj --filter "FullyQualifiedName~AdminDashboardServiceTests"
dotnet test .\tests\WorkspaceEcommerce.Api.IntegrationTests\WorkspaceEcommerce.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AdminDashboardIntegrationTests"
dotnet build WorkspaceEcommerce.slnx
```

Kết quả:

- Admin Dashboard Application tests passed: 3 tests.
- Admin Dashboard API integration tests passed: 3 tests trên PostgreSQL/Testcontainers.
- Integration coverage xác nhận threshold boundary `5`, loại stock `6`, low-stock ordering, recent-order ordering/limit `5`, status summary và empty data.
- Backend build passed, warnings 0, errors 0.
- Full backend suite passed: Application 132 tests, Infrastructure 60 tests, API integration 23 tests.

Đã chạy cho nâng UI Admin Dashboard:

```powershell
corepack pnpm --filter @workspace-ecommerce/admin typecheck
corepack pnpm --filter @workspace-ecommerce/admin lint
corepack pnpm --filter @workspace-ecommerce/admin build
```

Kết quả:

- Admin typecheck và lint passed.
- Admin production build passed; output JS khoảng `489.13 kB`, gzip khoảng `143.51 kB`; CSS khoảng `31.01 kB`, gzip khoảng `6.64 kB`.
- Full frontend monorepo typecheck, lint và production build passed.
- API login local passed và Admin dev server trả HTTP 200.
- Browser headless screenshot automation qua Chrome DevTools không hoàn tất do protocol timeout; không để lại profile hoặc screenshot artifact trong repo.

Đã chạy cho liên kết Dashboard với Orders/Products và Admin shell:

```powershell
corepack pnpm --filter @workspace-ecommerce/admin typecheck
corepack pnpm --filter @workspace-ecommerce/admin lint
corepack pnpm --filter @workspace-ecommerce/admin build
```

Kết quả:

- Admin typecheck, lint và production build passed.
- Dashboard status link tạo `/orders?status=...`; recent order tạo `/orders?search=...`.
- Orders dùng URL làm nguồn filter/pagination, hỗ trợ reload, bookmark và browser back/forward.
- Low-stock action tạo `/products?productId=...&variantId=...`; Products tự expand, scroll và highlight đúng product/variant.
- Admin navigation đã thay icon ký tự lỗi encoding bằng inline SVG và có active/focus-visible state rõ ràng.
- HTTP smoke bằng dữ liệu Dashboard thật passed: Orders search resolve đúng recent order, Products resolve đúng product/variant và các URL `/orders?status=...`, `/orders?search=...`, `/products?productId=...&variantId=...` trả app shell 200.

Theo cập nhật người dùng 2026-06-11:

- Browser Manual Verification đã hoàn tất.
- Demo readiness end-to-end đã hoàn tất.
- Ưu tiên 1 và Ưu tiên 2 trong danh sách nhiệm vụ trước đã xong.

Đã chạy cho Admin Product Asset UI:

```powershell
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
dotnet test .\tests\WorkspaceEcommerce.Api.IntegrationTests\WorkspaceEcommerce.Api.IntegrationTests.csproj
docker compose up -d postgres
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm seed-demo
docker compose build api
docker compose up -d api
```

Kết quả:

- Typecheck passed cho `api-types`, `api-client`, `shared-utils`, `storefront`, `admin`.
- Lint passed cho Storefront và Admin.
- Production build passed cho Storefront và Admin.
- Admin build output gần nhất: CSS khoảng `24.01 kB`, JS khoảng `479.14 kB`, gzip JS khoảng `141.11 kB`.
- Storefront build output gần nhất: CSS khoảng `50.50 kB`, JS khoảng `471.03 kB`, gzip JS khoảng `140.95 kB`.
- API integration tests passed: 22 tests.
- PostgreSQL local container chạy healthy.
- Migration passed; database already up to date.
- Seed demo passed; seed idempotent nên dữ liệu demo cũ được giữ nguyên.
- API local container chạy trên `http://localhost:5080`.
- HTTP smoke-test Admin Product assets passed:
  - Admin login passed.
  - Create/update/delete product image passed.
  - Create/update/delete product specification passed.
  - Storefront Product Detail API thấy image/spec mới trước khi xóa.
- Vite dev servers đã start và trả HTML 200:
  - Admin: `http://localhost:5174`
  - Storefront Product Detail: `http://localhost:5173/products/atlas-standing-desk`

Đã chạy cho Admin Product Asset API:

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test .\tests\WorkspaceEcommerce.Application.Tests\WorkspaceEcommerce.Application.Tests.csproj
dotnet test .\tests\WorkspaceEcommerce.Infrastructure.Tests\WorkspaceEcommerce.Infrastructure.Tests.csproj
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
```

Kết quả:

- Backend build passed, warnings 0, errors 0.
- Application tests passed: 130 tests.
- Infrastructure tests passed: 60 tests.
- Frontend typecheck/lint/build passed cho shared packages, Storefront và Admin ở thời điểm backend contract mới được thêm.

API integration tests từng bị chặn ở lượt trước:

```powershell
dotnet test .\tests\WorkspaceEcommerce.Api.IntegrationTests\WorkspaceEcommerce.Api.IntegrationTests.csproj
```

Kết quả:

- Lượt cũ không chạy được do Docker/Testcontainers không kết nối được Docker endpoint `npipe://./pipe/docker_engine`.
- Lượt 2026-06-10 sau khi Docker hoạt động đã chạy lại passed 22/22 tests.

Đã chạy frontend dependency update:

```powershell
corepack pnpm install
```

Kết quả:

- Install passed.
- Ant Design-related packages đã được gỡ khỏi dependency graph của Admin.

Đã chạy frontend checks trong `frontend/`:

```powershell
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
```

Kết quả:

- Typecheck passed cho `api-types`, `api-client`, `shared-utils`, `storefront`, `admin`.
- Lint passed cho Storefront và Admin.
- Production build passed cho Storefront và Admin.
- Admin build không còn warning chunk lớn do Ant Design.
- Admin production output gần nhất: CSS khoảng `23.91 kB`, JS khoảng `470.32 kB`, gzip JS khoảng `139.95 kB`.

Đã kiểm tra không còn Ant Design reference trong frontend source/package ngoài `node_modules`/artifact cũ:

```powershell
Select-String -Pattern 'from "antd"|@ant-design|antd/dist|"antd"|Ant Design'
```

Kết quả:

- Không còn match trong source/package frontend sau khi loại trừ `node_modules` và `dist`.

Đã chạy backend build gần nhất trước đó:

```powershell
dotnet build WorkspaceEcommerce.slnx
```

Kết quả:

- Build succeeded.
- Warnings: 0.
- Errors: 0.

Smoke-test/API/test coverage đã có từ các task trước:

- Admin auth route guard/login/logout smoke-test: passed.
- Cart, Checkout, Order Lookup, Admin Order Management smoke-test qua API local: passed.
- PostgreSQL verification sau checkout: order, order items, status history, stock trừ đúng, cart checkout đã clear/remove đúng.
- API integration infrastructure với Testcontainers PostgreSQL: passed.
- Integration coverage cho Auth/Admin authorization, Catalog, Cart, Checkout, Order Lookup, Admin Order và edge cases chính: passed.
- Full `dotnet test WorkspaceEcommerce.slnx` gần nhất đã passed khi Docker Desktop hoạt động bình thường.

## Commit gần nhất

- `08af060 Polish storefront and harden admin workflows`
- `5409ed8 Fix storefront logo path and header icons`
- `bad51d7 Adjust storefront header visibility and sizing`
- `bea9c6c Update task progress after admin asset UI`
- `9b72a4a Add admin product asset UI`
- `2cc7cc6 Add admin product asset APIs`

Commit lịch sử liên quan:

- `03fee0c Update task progress and clean storefront login`
- `cfd9753 Polish storefront login page`
- `b5d4901 Refine storefront header navigation`
- `513597d Update storefront header navigation`
- `5782d3a Update task progress`
- `aca1d7d Replace admin Ant Design with Tailwind`
- `2c6da04 Implement admin operational flows`
- `0d03a3a Add admin auth route protection`
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
- Admin Product Image/Specification backend API đã triển khai và API integration tests đã passed khi Docker Desktop hoạt động.
- Admin frontend chưa có browser automation để tái kiểm tra tự động các luồng modal/form và dashboard navigation.
- Dữ liệu smoke-test local cũ có thể vẫn còn trong PostgreSQL dev; seed demo idempotent nhưng chưa có cleanup/reset script riêng cho demo.
- Admin Dashboard đang có sai lệch low-stock threshold giữa UI (`10`) và backend (`5`).
- Dashboard query đã chuyển sang EF Core async aggregate/projection; cần tiếp tục theo dõi query plan/index khi dữ liệu vận hành tăng lớn.
- Dashboard đã có 4 KPI, status overview, recent orders, refresh/last-updated state và điều hướng giữ context sang Orders/Products bằng URL query.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên hiện tại - Hoàn thiện Admin Dashboard

Mục tiêu: biến Dashboard thành màn hình tổng quan vận hành hữu ích, vẫn giữ phạm vi `Basic dashboard` trong `overview.md` và tái sử dụng các flow Orders/Products hiện có.

Hiện trạng:

1. Route `/` đã được bảo vệ bằng Admin auth và gọi `GET /api/admin/dashboard` qua shared API client.
2. API đã trả `totalOrders`, `totalRevenue`, `newOrders`, `lowStockThreshold`, `lowStockVariants`, `orderStatusSummary` và `recentOrders`.
3. UI đã có compact hero, last-updated/refresh state, 4 KPI, order-status bars, recent orders và inventory attention table.
4. Low-stock threshold dùng trực tiếp từ API; inventory state phân biệt out-of-stock, critical và low.
5. Query dashboard đã aggregate/project async tại PostgreSQL qua `IAdminDashboardQuery`.

Phạm vi nâng cấp đề xuất:

1. **Chốt contract Dashboard - hoàn thành 2026-06-12**
   - Giữ nguyên bốn số liệu bắt buộc từ `overview.md`.
   - Bổ sung `lowStockThreshold` để UI không hard-code business value.
   - Bổ sung summary số đơn theo trạng thái và danh sách 5 đơn gần nhất để hỗ trợ vận hành mà không tạo module mới.
   - Định nghĩa rõ `totalRevenue` chỉ tính đơn `Completed`, `newOrders` là đơn `Pending`.
2. **Tối ưu Application/API - hoàn thành 2026-06-12**
   - Aggregate count/revenue theo query thay vì `ToArray()` toàn bảng.
   - Project low-stock và recent orders trực tiếp sang DTO, giới hạn số bản ghi và sắp xếp ổn định.
   - Giữ controller mỏng, route `GET /api/admin/dashboard` và response envelope hiện tại.
   - Mở rộng Application tests và API integration tests cho threshold, status summary, recent order ordering và empty data.
3. **Nâng UI Dashboard - hoàn thành 2026-06-12**
   - Làm hero/header gọn với thời điểm cập nhật gần nhất và nút refresh có trạng thái loading.
   - Thiết kế KPI cards nhất quán cho Total orders, Completed revenue, New orders và Low-stock count.
   - Thêm Order status overview bằng thanh tỷ lệ/CSS đơn giản, không thêm chart library.
   - Thêm Recent orders table với mã đơn, khách hàng, tổng tiền, trạng thái, thời gian và action mở trang Orders.
   - Nâng bảng Low stock: hiển thị threshold từ API, phân biệt out-of-stock/critical/low và action sang Products.
4. **Liên kết Dashboard với flow hiện có - hoàn thành 2026-06-12**
   - Dashboard link sang `/orders` với query `status` hoặc `search`; Orders page đọc query param để áp dụng filter ban đầu.
   - Link low-stock sang `/products` với product/variant context phù hợp; Products page mở hoặc làm nổi bật đúng sản phẩm khi có query param.
   - Sửa icon navigation đang bị lỗi encoding và chuẩn hóa active/focus state trong Admin shell.
5. **Responsive và maintainability**
   - Tối ưu 1920x1080 để KPI và hai vùng Recent orders/Low stock xuất hiện trong viewport hợp lý.
   - Giữ layout sử dụng được ở tablet/mobile, table có fallback scroll rõ ràng.
   - Tách `DashboardPage.tsx` thành component theo section nếu logic/data mapping tăng; không tạo generic abstraction ngoài nhu cầu hiện tại.
6. **Verification**
   - Chạy backend build, Application tests và Admin Dashboard API integration tests.
   - Chạy frontend typecheck, lint và production build.
   - Browser smoke-test: login -> dashboard -> refresh -> pending orders -> recent order -> low-stock product -> quay lại dashboard.

File dự kiến bị ảnh hưởng:

- `src/WorkspaceEcommerce.Application/Modules/Admin/Dashboard/*`
- `src/WorkspaceEcommerce.Api/Controllers/Admin/DashboardController.cs` nếu metadata contract thay đổi.
- `tests/WorkspaceEcommerce.Application.Tests/Modules/Admin/Dashboard/*`
- `tests/WorkspaceEcommerce.Api.IntegrationTests/AdminDashboard/*`
- `frontend/packages/api-types/src/admin.ts`
- `frontend/apps/admin/src/pages/dashboard/*`
- `frontend/apps/admin/src/pages/orders/OrdersPage.tsx`
- `frontend/apps/admin/src/pages/products/ProductsPage.tsx`
- `frontend/apps/admin/src/components/layout/AdminLayout.tsx`
- Shared Admin UI/CSS chỉ sửa khi Dashboard thực sự cần primitive hoặc token chung.

Dependency hướng triển khai:

1. Backend DTO/query/test trước để chốt contract.
2. Shared TypeScript API types theo contract backend.
3. Dashboard UI và các link điều hướng.
4. Orders/Products nhận query param.
5. Responsive/accessibility pass và full verification.

Definition of Done:

- Bốn số liệu Dashboard trong `overview.md` hiển thị đúng và có định nghĩa nhất quán.
- Low-stock threshold chỉ có một nguồn sự thật từ backend.
- Dashboard có status overview, recent orders và low-stock actions dùng dữ liệu thật.
- Điều hướng từ Dashboard sang Orders/Products giữ đúng context lọc/chọn.
- Loading, error, empty, refresh và responsive states hoạt động rõ ràng.
- Backend không còn tải toàn bộ bảng vào memory chỉ để tính Dashboard.
- Application/API tests, frontend typecheck/lint/build và browser smoke-test đều pass.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
git status --short
```
