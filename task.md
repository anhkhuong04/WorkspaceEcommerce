# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-12

## Cách sử dụng

File này là baseline để bắt đầu các task hậu MVP. Không lưu checklist đã hoàn thành chi tiết; lịch sử triển khai nằm trong Git.

Trước mỗi task:

- Đọc `overview.md`, `.agent/README.md` và rule/skill liên quan.
- Xác định rõ phạm vi, business rule, file bị ảnh hưởng và hướng dependency trước khi sửa.
- Không đổi route, DTO, database schema hoặc module boundary ngoài phạm vi được yêu cầu.
- Giữ controller/page mỏng; business rule thuộc Domain/Application, API call thuộc client/service layer.
- Chạy test/build phù hợp, cập nhật file này nếu baseline hoặc rủi ro thay đổi, rồi commit với tên rõ nghĩa.

## Baseline hậu MVP

Trạng thái: **MVP đã hoàn thành về chức năng và sẵn sàng demo local**.

### Backend

- Clean Architecture Modular Monolith gồm `Domain`, `Application`, `Infrastructure`, `Api`.
- Module hiện có: Catalog, Cart, Checkout, Ordering, Inventory cơ bản và Content/Banner.
- ASP.NET Core API dùng JWT Admin auth, response envelope, global exception handling và OpenAPI trong Development.
- PostgreSQL/EF Core; Docker Compose có `postgres`, `migrate`, `seed-demo`, `api`.
- Application tests, Infrastructure tests và API integration tests dùng PostgreSQL/Testcontainers.

### Frontend

- Monorepo `frontend/` gồm Storefront, Admin và shared `api-types`, `api-client`, `shared-utils`.
- React + TypeScript + Tailwind CSS; React Query cho server state; React Hook Form + Zod cho form.
- Storefront có Home, Catalog, Product Detail, Cart, Checkout, Success và Order Lookup dùng API thật.
- Admin có Auth, Dashboard, Categories, Products/Variants/Images/Specifications, Banners và Orders.
- Admin dùng local UI primitives, không còn Ant Design.

### Admin Dashboard

- API: `GET /api/admin/dashboard`, giữ response envelope hiện tại.
- KPI: total orders, completed revenue, pending/new orders và low-stock variants.
- `totalRevenue` chỉ tính đơn `Completed`; `newOrders` chỉ tính đơn `Pending`.
- Threshold tồn kho lấy từ API; có summary đủ trạng thái, 5 đơn gần nhất và query aggregate/projection tại database.
- UI responsive, có refresh/last-updated, status overview, Recent orders, Low stock và điều hướng giữ context sang Orders/Products.
- Admin client có guard cho Dashboard contract để API image cũ không làm sập route.

### Delete behavior

- Product có thể xóa cứng khi chưa có order history; nếu đã được tham chiếu bởi đơn hàng thì trả `409 Conflict` và phải deactivate.
- Category chỉ được xóa khi không còn category con hoặc product; trường hợp bị tham chiếu trả `409 Conflict`.
- Banner, Product Image và Product Specification hỗ trợ xóa với confirm UI.
- Product, Category và Variant vẫn dùng `IsActive` cho luồng ẩn/hiện thông thường.

## Luồng đã ổn định

- Storefront: Home -> Catalog -> Product Detail -> Cart -> Checkout -> Success -> Order Lookup.
- Admin: Login -> Dashboard -> Categories/Products/Banners -> Orders -> cập nhật trạng thái.
- Checkout tạo order/order items/status history, trừ tồn kho và clear cart trong transaction.
- Seed demo idempotent cung cấp banner, category, product, variant và dữ liệu phục vụ demo.
- Admin URL query giữ filter/context cho Dashboard -> Orders và Dashboard -> Products.

## Kiến trúc và dependency

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc frontend.
- `Application` phụ thuộc Domain và định nghĩa persistence/service abstractions.
- `Infrastructure` triển khai EF Core/PostgreSQL, JWT và external concerns.
- `Api` chỉ bind request, authorization và map Application result sang HTTP response.
- Frontend dùng shared typed API client; page không gọi `fetch` trực tiếp.
- Không tạo abstraction generic nếu chỉ có một use case cụ thể.

## Business rule cần giữ

- Không xóa Product đã có order history.
- Product, Category, Variant dùng `IsActive` để kiểm soát hiển thị.
- SKU là đơn vị quản lý giá và tồn kho; SKU phải unique.
- Checkout snapshot dữ liệu sản phẩm vào OrderItem.
- Order status chỉ chuyển theo workflow hợp lệ; mọi thay đổi có status history.
- Dashboard low-stock threshold chỉ có một nguồn sự thật từ backend.

## Xác minh gần nhất

Ngày 2026-06-12:

- Product/Category/Banner delete: 44 Application tests và 13 API integration tests targeted passed.
- Backend solution build passed, 0 warnings, 0 errors.
- Frontend monorepo typecheck, lint và production build passed.
- API container đã rebuild; HTTP smoke create/delete Product, Category và Banner passed, dữ liệu smoke đã được dọn.
- Dashboard contract/query/UI/navigation/responsive checks đã passed trong chuỗi commit `99007ae` đến `1ef46e5`.
- Browser manual verification và demo readiness end-to-end đã hoàn tất ngày 2026-06-11.

Commit baseline gần nhất:

- `98d003b Add product category and banner deletion`
- `1ef46e5 Handle stale dashboard API contract`
- `22eba7d Refine admin dashboard responsiveness`

## Technical debt và rủi ro còn mở

- Admin JWT đang lưu trong `localStorage`; cần threat model và session strategy phù hợp trước production.
- Chưa có browser automation cho các luồng Admin/Storefront quan trọng.
- Docker image chưa harden cho production: non-root, SBOM, signing và CI publish.
- Config local cần secret thật cho database, Admin auth và JWT; placeholder phải tiếp tục fail fast.
- API integration coverage chưa exhaustive cho mọi validation/conflict edge case.
- Dashboard query cần theo dõi query plan/index khi dữ liệu tăng lớn.
- Frontend bundle cần được theo dõi khi thêm module hậu MVP.
- Seed demo idempotent nhưng chưa có cleanup/reset script chuyên dụng.
- Branding và visual polish cuối của Storefront chưa được chốt.

## Roadmap hậu MVP

Chưa chốt ưu tiên mới. Khi nhận task, thêm một mục ngắn theo mẫu sau và xóa sau khi hoàn thành:

```markdown
### [Tên task]

- Mục tiêu:
- Phạm vi:
- Ngoài phạm vi:
- Business rule/contract:
- File/module dự kiến:
- Verification:
- Trạng thái:
```

Các hướng mở rộng trong `overview.md` như payment online, customer account, loyalty, warranty, B2B và installation workflow chỉ triển khai khi được yêu cầu rõ.

## Lệnh kiểm tra chuẩn

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
Set-Location frontend
corepack pnpm typecheck
corepack pnpm lint
corepack pnpm build
Set-Location ..
git status --short
```

API integration tests cần Docker Desktop/PostgreSQL Testcontainers hoạt động.
