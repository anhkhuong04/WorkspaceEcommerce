# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-07

## Nguyên tắc trước khi làm task mới

- Luôn đọc `overview.md` trước khi thay đổi nghiệp vụ.
- Đọc `.agent/README.md` và rule/skill liên quan trước khi code.
- Không triển khai tính năng ngoài phạm vi `overview.md` nếu chưa được yêu cầu rõ.
- Trước khi sửa code phải nêu kế hoạch, file bị ảnh hưởng và hướng dependency.
- Không sửa file không liên quan.

## Trạng thái hiện tại

Backend đã có nền tảng Clean Architecture Modular Monolith cho Catalog/Admin Category:

- `WorkspaceEcommerce.Domain`: Catalog entities và domain invariant cơ bản.
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validator và service cho Admin Category.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog mappings, migration đầu tiên và configuration validation.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Category, response envelope, global exception handling và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: test cho Catalog Domain và Admin Category service/validator.
- `WorkspaceEcommerce.Infrastructure.Tests`: test cho configuration validation.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`.
- `Infrastructure` phụ thuộc `Application` và `Domain`, triển khai EF Core/PostgreSQL.
- `Api` phụ thuộc `Application` và `Infrastructure`, không chứa business logic.

## Đã hoàn thành

### Agent rules và backend foundation

- Tạo `.agent` với rule cho architecture, coding, backend, API, data, frontend, quality, workflow và skill.
- Tạo solution/project structure trong `src/` và `tests/`.
- Thêm `.gitignore` cho `bin/`, `obj/`, `.idea/`, log, coverage và file môi trường local.
- Commit/push backend foundation ban đầu lên `origin/main`.

### Catalog Domain

- Triển khai entity: `Category`, `Product`, `ProductVariant`, `ProductImage`, `ProductSpecification`.
- Thêm helper: `Entity`, `DomainException`, `Guard`.
- Domain không có EF attribute hoặc EF package reference.
- Entity dùng private setter khi phù hợp.
- Đã có domain method cho activate/deactivate, update, parent category, pricing, stock, image/specification.
- Chưa triển khai Cart, Order, Checkout, Customer, Content hoặc Inventory aggregate.

### Catalog persistence và migration

- Thêm `AppDbContext` và DbSet cho Catalog.
- Mapping bằng `IEntityTypeConfiguration<T>` trong Infrastructure.
- Mapping PostgreSQL-compatible với schema `catalog`.
- Config required fields, max length, relationship, index, delete behavior và decimal precision.
- Unique index cho category slug, product slug và variant SKU.
- Tạo migration `InitialCatalogSchema` trong Infrastructure migration assembly.
- Migration chỉ tạo các bảng Catalog: `categories`, `products`, `product_images`, `product_specifications`, `product_variants`.

### Application common contracts/models

- Thêm `IAppDbContext`.
- Thêm `Result`, `Result<T>`, `ResultStatus`, `PagedResult<T>`, `PaginationRequest`.
- Application không expose EF Core implementation.

### Admin Category Management

- Thêm API:
  - `GET /api/admin/categories`
  - `POST /api/admin/categories`
  - `PUT /api/admin/categories/{id}`
- Controller mỏng, delegate sang Application service.
- Dùng DTO request/response và FluentValidation.
- Business logic nằm trong Application service.
- Hỗ trợ parent/child category.
- Dùng `IsActive`, không hard delete.
- Chặn duplicate slug, invalid parent, self-parent và parent cycle.
- Chưa triển khai Product APIs.

### API foundation

- Thêm global exception middleware.
- Unexpected exception trả response `500` an toàn, không expose stack trace, SQL error hoặc internal exception message.
- Chuẩn hóa response bằng `ApiResponse<T>`.
- Thêm mapper từ Application `Result` sang HTTP response.
- Chuẩn hóa model-state error response của `[ApiController]`.
- Thêm OpenAPI bằng `Microsoft.AspNetCore.OpenApi`.
- Chỉ bật `/openapi/v1.json` trong Development.

### Configuration validation

- Validate `ConnectionStrings:DefaultConnection` sớm khi app start qua Infrastructure DI.
- Kiểm tra connection string bị thiếu, sai format, thiếu `Host`, thiếu `Database` hoặc còn placeholder.
- `appsettings.json` và `appsettings.Development.json` chỉ giữ placeholder `Password=CHANGE_ME`, không chứa secret thật.
- Runtime local cần override bằng user-secrets, environment variable hoặc local config không commit.
- Thêm test cho `ConnectionStringValidator` trong Infrastructure test project.

### Tests

- Application tests cho Admin Category validator/service.
- Domain tests cho Category parent rules, Product variant SKU uniqueness, ProductVariant price/stock invariants.
- Infrastructure tests cho configuration validation.

## Xác minh gần nhất

Đã chạy:

```powershell
dotnet build WorkspaceEcommerce.slnx
```

Kết quả:

- Build succeeded.
- Warnings: 0
- Errors: 0

Đã chạy:

```powershell
dotnet test WorkspaceEcommerce.slnx
```

Kết quả:

- `WorkspaceEcommerce.Application.Tests`: 28 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 8 passed.
- Failed: 0.
- Skipped: 0.

## Rủi ro và khoảng trống

- Catalog migration đã tạo nhưng chưa apply vào PostgreSQL local.
- Vì config dùng `Password=CHANGE_ME`, app sẽ fail sớm nếu chưa override `DefaultConnection` bằng secret/config local hợp lệ.
- Admin endpoints chưa có authentication/authorization vì Admin Authentication chưa triển khai.
- Chưa có API integration tests cho Admin Category endpoints.
- Chưa có Infrastructure tests cho EF Core model mapping/migration.
- Chưa có Docker Compose hoặc runtime setup cho PostgreSQL.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 1 - Database/runtime

1. Apply Catalog migration vào PostgreSQL local.
   - Override `ConnectionStrings:DefaultConnection` bằng secret/config local hợp lệ.
   - Chạy `dotnet ef database update` với Infrastructure project và API startup project.
   - Verify schema `catalog`, tables, indexes, delete behavior và decimal precision trong PostgreSQL.

2. Thêm Infrastructure tests cho EF Core mapping.
   - Verify schema `catalog`.
   - Verify unique indexes slug/SKU.
   - Verify decimal precision.
   - Verify delete behavior.
   - Không dùng EF InMemory cho behavior PostgreSQL.

### Ưu tiên 2 - Admin security

3. Triển khai Admin Authentication.
   - Theo `overview.md`: login email/password.
   - Chưa cần phân quyền phức tạp trong MVP.
   - Thêm JWT authentication.
   - Bảo vệ `/api/admin/*` bằng `[Authorize]` sau khi auth sẵn sàng.

### Ưu tiên 3 - Catalog Product Management

4. Triển khai Admin Product Management.
   - `GET /api/admin/products`
   - `POST /api/admin/products`
   - `PUT /api/admin/products/{id}`
   - `POST /api/admin/products/{id}/variants`
   - `PUT /api/admin/variants/{id}`
   - Dùng Application service, DTO, FluentValidation và `IsActive`.

5. Triển khai Storefront Catalog read APIs.
   - `GET /api/categories`
   - `GET /api/products`
   - `GET /api/products/{slug}`
   - Chỉ trả active category/product/variant.
   - Product listing có pagination/filter trong phạm vi MVP.

### Ưu tiên 4 - Module MVP sau Catalog

6. Triển khai Cart module.
7. Triển khai Checkout và Ordering với snapshot OrderItem.
8. Triển khai Admin Order Management và OrderStatusHistory.
9. Triển khai Banner Management và Dashboard.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
git status --short
```
