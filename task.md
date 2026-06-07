# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-07

## Nguyên tắc trước khi làm task mới

- Luôn đọc `overview.md` trước khi thay đổi nghiệp vụ.
- Đọc `.agent/README.md` và rule/skill liên quan trước khi code.
- Không triển khai tính năng ngoài phạm vi `overview.md` nếu chưa được yêu cầu rõ.
- Trước khi sửa code phải nêu kế hoạch, file bị ảnh hưởng và hướng dependency.
- Không sửa file không liên quan.

## Trạng thái hiện tại

Backend đã có nền tảng Clean Architecture Modular Monolith cho Catalog/Admin:

- `WorkspaceEcommerce.Domain`: Catalog entities và domain invariant cơ bản.
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validator và service cho Admin Auth, Category, Product và Variant.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog mappings, migration đầu tiên và configuration validation.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Auth/Admin Category/Admin Product, JWT authentication, response envelope, global exception handling và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: test cho Catalog Domain, Admin Auth, Admin Category và Admin Product service/validator.
- `WorkspaceEcommerce.Infrastructure.Tests`: test cho configuration validation, EF Core Catalog mapping và JWT token generation.
- PostgreSQL local chạy bằng Docker Compose service `postgres`.

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

### Admin Product Management

- Thêm API:
  - `GET /api/admin/products`
  - `POST /api/admin/products`
  - `PUT /api/admin/products/{id}`
  - `POST /api/admin/products/{id}/variants`
  - `PUT /api/admin/variants/{id}`
- Controller mỏng, delegate sang Application service.
- Dùng DTO request/response và FluentValidation.
- Business logic nằm trong Application service.
- Dùng `IsActive` cho Product và ProductVariant.
- Hỗ trợ `IsFeatured`, description và category assignment cho Product.
- Hỗ trợ SKU, name, color, size, price, compare-at price, stock, requires installation và active flag cho Variant.
- Chặn category không tồn tại khi tạo/cập nhật Product.
- Chặn duplicate product slug.
- Chặn duplicate SKU không phân biệt hoa/thường.
- Không triển khai Product image/specification API trong task này vì request chỉ nêu Product và Variant endpoints.

### API foundation

- Thêm global exception middleware.
- Unexpected exception trả response `500` an toàn, không expose stack trace, SQL error hoặc internal exception message.
- Chuẩn hóa response bằng `ApiResponse<T>`.
- Thêm mapper từ Application `Result` sang HTTP response.
- Chuẩn hóa model-state error response của `[ApiController]`.
- Thêm OpenAPI bằng `Microsoft.AspNetCore.OpenApi`.
- Chỉ bật `/openapi/v1.json` trong Development.

### Admin Authentication

- Thêm API `POST /api/admin/auth/login`.
- Login dùng email/password theo phạm vi MVP trong `overview.md`.
- Chưa thêm phân quyền phức tạp hoặc bảng Admin vì `overview.md` chưa định nghĩa Admin data model.
- Credential admin đọc từ configuration `AdminAuth`.
- JWT đọc từ configuration `Jwt`.
- Không commit password hoặc signing key thật; appsettings chỉ giữ placeholder.
- Thêm JWT Bearer authentication.
- Bảo vệ `GET/POST/PUT /api/admin/categories` bằng `[Authorize]`.
- Chuẩn hóa response `401`/`403` theo `ApiResponse<T>`.
- Invalid login trả `401 Unauthorized`, không expose thông tin nội bộ.

### Configuration validation

- Validate `ConnectionStrings:DefaultConnection` sớm khi app start qua Infrastructure DI.
- Kiểm tra connection string bị thiếu, sai format, thiếu `Host`, thiếu `Database` hoặc còn placeholder.
- Validate `AdminAuth` và `Jwt` sớm khi app start.
- Kiểm tra admin credential, JWT issuer/audience/signing key/token lifetime bị thiếu hoặc còn placeholder.
- Signing key JWT phải đủ tối thiểu 32 bytes cho HS256.
- `appsettings.json` và `appsettings.Development.json` chỉ giữ placeholder `Password=CHANGE_ME`, không chứa secret thật.
- Runtime local cần override bằng user-secrets, environment variable hoặc local config không commit.
- Thêm test cho `ConnectionStringValidator` trong Infrastructure test project.

### Tests

- Application tests cho Admin Category validator/service.
- Application tests cho Admin Auth validator/service.
- Application tests cho Admin Product validator/service.
- Domain tests cho Category parent rules, Product variant SKU uniqueness, ProductVariant price/stock invariants.
- Infrastructure tests cho configuration validation.
- Infrastructure tests cho JWT token generation.
- Infrastructure tests cho EF Core Catalog mapping:
  - schema `catalog`
  - table names
  - unique indexes slug/SKU
  - decimal precision `numeric(18,2)`
  - delete behavior `Restrict`/`Cascade`
  - không dùng EF InMemory

### Database migration local

- Đã thêm `docker-compose.yml` cho PostgreSQL local.
- Đã thêm `.env.example`; file `.env` local không commit vì chứa password dev.
- Đã chạy PostgreSQL container `workspace-ecommerce-postgres`.
- Đã apply migration bằng `dotnet ef database update` với Infrastructure project và API startup project.
- Đã verify trực tiếp trên PostgreSQL:
  - schema `catalog`
  - bảng `categories`, `products`, `product_images`, `product_specifications`, `product_variants`
  - unique indexes `ux_categories_slug`, `ux_products_slug`, `ux_product_variants_sku`
  - `price` và `compare_at_price` là `numeric(18,2)`
  - delete behavior `Restrict`/`Cascade`
  - migration history có `20260607075432_InitialCatalogSchema`

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
- `WorkspaceEcommerce.Infrastructure.Tests`: 23 passed.
- Failed: 0.
- Skipped: 0.

Sau Admin Product Management, xác minh mới nhất:

- `WorkspaceEcommerce.Application.Tests`: 49 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 30 passed.

## Rủi ro và khoảng trống

- Vì config dùng `Password=CHANGE_ME`, app sẽ fail sớm nếu chưa override `DefaultConnection` bằng secret/config local hợp lệ.
- Vì config dùng `AdminAuth:Password=CHANGE_ME` và `Jwt:SigningKey=CHANGE_ME`, app sẽ fail sớm nếu chưa override bằng secret/config local hợp lệ.
- Chưa có API integration tests cho Admin Category endpoints.
- Chưa có API integration tests cho Admin Product endpoints.
- Chưa có API/Docker Compose setup cho backend container.
- Runtime check authorized category cần PostgreSQL container reachable; lần kiểm tra gần nhất bị chặn vì Docker Desktop đang ở trạng thái pause.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 1 - Storefront Catalog

1. Triển khai Storefront Catalog read APIs.
   - `GET /api/categories`
   - `GET /api/products`
   - `GET /api/products/{slug}`
   - Chỉ trả active category/product/variant.
   - Product listing có pagination/filter trong phạm vi MVP.

### Ưu tiên 2 - Module MVP sau Catalog

2. Triển khai Cart module.
3. Triển khai Checkout và Ordering với snapshot OrderItem.
4. Triển khai Admin Order Management và OrderStatusHistory.
5. Triển khai Banner Management và Dashboard.

### Ưu tiên 3 - Runtime/DevOps

6. Thêm Dockerfile cho backend API khi bắt đầu đóng gói app.
7. Thêm healthcheck/runtime documentation cho API + PostgreSQL.
8. Thêm API integration tests cho login, admin authorization và Admin Product endpoints.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
git status --short
```
