# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-11

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

Phần còn lại không còn là blocker chức năng MVP, mà là hardening trước demo/vận hành:

- Admin UI hardening sau khi bỏ Ant Design.
- Browser automation hoặc checklist sâu cho toàn bộ Admin modal/form.
- Accessibility/focus/keyboard behavior cho modal và form.
- Theo dõi bundle/code splitting nếu Admin UI tiếp tục mở rộng.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`, `ICartStore`, `ICheckoutStore`.
- `Infrastructure` phụ thuộc `Application` và `Domain`, triển khai EF Core/PostgreSQL, JWT và config validation.
- `Api` phụ thuộc `Application` và `Infrastructure`, không chứa business logic.
- Frontend dùng shared `@workspace-ecommerce/api-client` và `@workspace-ecommerce/api-types`; UI pages không gọi `fetch` trực tiếp.
- Admin frontend không còn phụ thuộc Ant Design; dùng Tailwind/native controls và local UI primitives.

## Đã hoàn thành

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

### Admin Product Asset UI

- Tích hợp product image list/create/update/delete vào Admin Products expanded row.
- Tích hợp product specification list/create/update/delete vào Admin Products expanded row.
- Thêm Zod schemas và React Hook Form forms cho image/specification modals.
- Thêm mutations dùng shared `api-client`: create/update/delete image và specification.
- Admin Products table hiển thị tổng số variants, images và specs; action nhanh có `Add image` và `Add spec`.
- Sửa Storefront header/login để không còn import các asset đã xóa ở commit trước; dùng fallback text/CSS để frontend build sạch.
- Commit riêng: `9b72a4a Add admin product asset UI`.

## Xác minh gần nhất

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
- Admin Product Asset UI đã build/typecheck/lint sạch; đã smoke-test qua HTTP API local, nhưng chưa có browser automation để tự động click form trong UI.
- Dữ liệu smoke-test local cũ có thể vẫn còn trong PostgreSQL dev; seed demo idempotent nhưng chưa có cleanup/reset script riêng cho demo.
- Admin UI đã chuyển sang Tailwind/native controls; cần smoke-test trình duyệt sâu thêm cho mọi modal/form sau refactor Ant Design.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 3 - Admin UI hardening

Mục tiêu: tăng độ ổn định vận hành sau khi bỏ Ant Design.

Phạm vi nên làm:

1. Smoke-test browser thủ công hoặc Playwright cho toàn bộ Admin modal/form: Banner, Category, Product, Variant, Product Image, Product Specification và Order status.
2. Rà accessibility cho modal/form: label, `aria-*`, focus trap, focus restore sau khi đóng modal, phím `Escape`, keyboard submit/cancel.
3. Chuẩn hóa trạng thái form: loading, disabled state, validation message, API error message, confirm trước delete.
4. Rà responsive Admin ở các breakpoint chính: mobile hẹp, tablet và desktop Full HD.
5. Tách các page lớn thành component nhỏ nếu file đang ôm quá nhiều table/form/modal logic.
6. Cân nhắc route-level code splitting nếu bundle Admin tăng trở lại khi thêm flow mới.

Kế hoạch triển khai đề xuất:

1. Inventory UI: liệt kê toàn bộ Admin page/modal/form/action hiện có và xác định file chịu trách nhiệm.
2. Test matrix: tạo checklist hoặc Playwright spec cho các luồng CRUD chính và order status.
3. Accessibility pass: sửa modal/focus/label/keyboard behavior trước, vì đây là nhóm lỗi ảnh hưởng mọi form.
4. Form hardening pass: thống nhất loading/error/disabled/delete confirmation cho Banner, Category, Product, Variant, Image, Specification và Order.
5. Refactor có kiểm soát: chỉ tách component ở những page vượt ngưỡng khó bảo trì; không refactor rộng nếu không giảm rủi ro thực tế.
6. Verification: chạy `corepack pnpm typecheck`, `corepack pnpm lint`, `corepack pnpm build`, smoke-test Admin trên browser và nếu có Playwright thì chạy suite tương ứng.

Definition of Done cho Ưu tiên 3:

- Tất cả Admin modal/form chính thao tác được bằng mouse và keyboard.
- Modal có focus behavior hợp lý khi mở/đóng.
- Form có validation và API error rõ ràng.
- Delete/update action không gây mất dữ liệu do click nhầm ở các flow rủi ro.
- Admin build/typecheck/lint sạch.
- Có checklist hoặc automated test artifact để tái kiểm tra trước demo.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
git status --short
```
