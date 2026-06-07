# Task - WorkspaceEcommerce

Cập nhật lần cuối: 2026-06-07

## Nguyên tắc trước khi làm task mới

- Luôn đọc `overview.md` trước khi thay đổi nghiệp vụ.
- Đọc `.agent/README.md` và rule/skill liên quan trước khi code.
- Không triển khai tính năng ngoài phạm vi `overview.md` nếu chưa được yêu cầu rõ.
- Trước khi sửa code phải nêu kế hoạch, file bị ảnh hưởng và hướng dependency.
- Không sửa file không liên quan.

## Trạng thái hiện tại

Backend đã có nền tảng Clean Architecture Modular Monolith cho Catalog/Admin/Cart:

- `WorkspaceEcommerce.Domain`: Catalog và Cart entities với invariant cơ bản.
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validator và service cho Admin Auth, Admin Catalog, Storefront Catalog và Storefront Cart.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog/Cart mappings, migrations và configuration validation.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Auth/Admin Catalog/Storefront Catalog/Storefront Cart, JWT authentication, response envelope, global exception handling và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: test cho Catalog/Cart Domain, Admin Auth, Admin Catalog, Storefront Catalog và Storefront Cart service/validator.
- `WorkspaceEcommerce.Infrastructure.Tests`: test cho configuration validation, EF Core Catalog/Cart mapping và JWT token generation.
- PostgreSQL local chạy bằng Docker Compose service `postgres`.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`, `ICartStore`.
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

### Storefront Catalog read APIs

- Thêm API public:
  - `GET /api/categories`
  - `GET /api/products`
  - `GET /api/products/{slug}`
- Không yêu cầu JWT cho Storefront APIs.
- `GET /api/categories` chỉ trả active category và build tree parent/child.
- `GET /api/products` chỉ trả active product thuộc active category.
- Product listing chỉ dùng active variants để tính giá, compare-at price và trạng thái còn hàng.
- Product listing hỗ trợ pagination với `pageNumber`, `pageSize`.
- Product listing hỗ trợ filter MVP:
  - `categorySlug`
  - `search`
  - `minPrice`
  - `maxPrice`
  - `inStock`
- `GET /api/products/{slug}` chỉ trả active product thuộc active category.
- Product detail chỉ trả active variants; images/specifications không có `IsActive` trong data model nên trả theo product.

### Cart module

- Triển khai Domain entities:
  - `Cart`
  - `CartItem`
- Domain không phụ thuộc EF Core.
- Cart invariant:
  - Cart phải thuộc customer hoặc session.
  - CartItem quantity phải lớn hơn 0.
  - `UnitPriceSnapshot` không âm.
  - Tổng tiền tính từ `UnitPriceSnapshot * Quantity`.
- Application service `IStorefrontCartService` xử lý:
  - lấy cart theo `SessionId`
  - thêm item
  - cập nhật quantity
  - xóa item
- Application validators cho:
  - `GetCartRequest`
  - `AddCartItemRequest`
  - `UpdateCartItemRequest`
  - `RemoveCartItemRequest`
- Service chỉ cho add/update active variant thuộc active product và active category.
- Service kiểm tra tồn kho cơ bản trước khi add/update quantity.
- Cart item lưu `UnitPriceSnapshot` từ giá hiện tại của variant khi thêm item mới.
- Khi add cùng variant vào cart, service tăng quantity và giữ price snapshot cũ.
- Chưa triển khai Checkout hoặc Order trong Cart task.

### Cart persistence và Storefront Cart APIs

- Thêm EF Core mappings cho Cart bằng `IEntityTypeConfiguration<T>`.
- Tạo schema `cart`.
- Tạo bảng:
  - `cart.carts`
  - `cart.cart_items`
- Mapping PostgreSQL-compatible:
  - `session_id` max length 128
  - `unit_price_snapshot numeric(18,2)`
  - FK `cart_items.cart_id -> cart.carts.id` dùng cascade delete
  - FK `cart_items.product_variant_id -> catalog.product_variants.id` dùng restrict delete
- Thêm index:
  - `ix_carts_session_id`
  - `ix_carts_customer_id`
  - `ix_cart_items_cart_id`
  - `ix_cart_items_product_variant_id`
  - `ux_cart_items_cart_id_product_variant_id`
- Tạo migration `AddCartSchema`.
- `AppDbContext` triển khai `ICartStore`.
- Đăng ký DI cho `IStorefrontCartService` và `ICartStore`.
- Thêm API public:
  - `GET /api/cart`
  - `POST /api/cart/items`
  - `PUT /api/cart/items/{id}`
  - `DELETE /api/cart/items/{id}`
- Cart APIs không yêu cầu JWT vì thuộc Storefront customer flow.
- Đã smoke-test đủ 4 endpoints trên PostgreSQL local.
- Đã fix lỗi EF tracking khi tạo cart mới: cart mới chỉ `Add(cart)`, không `Update(cart)`.

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
- Application tests cho Storefront Catalog read service.
- Application tests cho Storefront Cart validator/service.
- Domain tests cho Category parent rules, Product variant SKU uniqueness, ProductVariant price/stock invariants.
- Domain tests cho Cart và CartItem invariants.
- Infrastructure tests cho configuration validation.
- Infrastructure tests cho JWT token generation.
- Infrastructure tests cho EF Core Catalog mapping:
  - schema `catalog`
  - table names
  - unique indexes slug/SKU
  - decimal precision `numeric(18,2)`
  - delete behavior `Restrict`/`Cascade`
  - không dùng EF InMemory
- Infrastructure tests cho EF Core Cart mapping:
  - schema `cart`
  - table names
  - lookup indexes
  - unique index `(cart_id, product_variant_id)`
  - decimal precision `numeric(18,2)`
  - delete behavior `Cascade`/`Restrict`

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
- Đã apply migration `20260607133308_AddCartSchema`.
- Đã verify trực tiếp trên PostgreSQL:
  - schema `cart`
  - bảng `carts`, `cart_items`
  - indexes `ix_carts_session_id`, `ix_carts_customer_id`, `ix_cart_items_cart_id`, `ix_cart_items_product_variant_id`, `ux_cart_items_cart_id_product_variant_id`
  - FK `cart_items.cart_id` cascade
  - FK `cart_items.product_variant_id` restrict
  - `unit_price_snapshot` là `numeric(18,2)`
- Đã seed dữ liệu smoke-test tối thiểu:
  - category slug `smoke-test-category`
  - product slug `smoke-test-product`
  - variant SKU `SMOKE-CART-001`
- Đã smoke-test Cart APIs trên API local `http://localhost:5080`:
  - `GET /api/cart?sessionId=...` trả cart rỗng `200`
  - `POST /api/cart/items` thêm item `200`
  - `PUT /api/cart/items/{id}` cập nhật quantity `200`
  - `DELETE /api/cart/items/{id}?sessionId=...` xóa item `200`

### Commit gần nhất

- `a5e49b3 Add cart domain and application service`
- `661223a Add cart persistence and storefront API`
- `93f1610 Fix cart creation persistence`

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

- `WorkspaceEcommerce.Application.Tests`: 74 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 40 passed.
- Failed: 0.
- Skipped: 0.

Sau Cart persistence/API và smoke-test, xác minh mới nhất:

- `WorkspaceEcommerce.Application.Tests`: 74 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 40 passed.
- Tổng test suite: 114 passed.
- Cart smoke-test 4 endpoints: passed.

## Rủi ro và khoảng trống

- Vì config dùng `Password=CHANGE_ME`, app sẽ fail sớm nếu chưa override `DefaultConnection` bằng secret/config local hợp lệ.
- Vì config dùng `AdminAuth:Password=CHANGE_ME` và `Jwt:SigningKey=CHANGE_ME`, app sẽ fail sớm nếu chưa override bằng secret/config local hợp lệ.
- Chưa có API integration tests cho Admin Category endpoints.
- Chưa có API integration tests cho Admin Product endpoints.
- Chưa có API integration tests cho Storefront Catalog endpoints.
- Chưa có API integration tests tự động cho Storefront Cart endpoints; hiện mới smoke-test thủ công qua API local.
- Chưa có API/Docker Compose setup cho backend container.
- Dữ liệu smoke-test local đã được insert vào PostgreSQL dev; nếu cần DB sạch cho demo thì cần seed strategy chính thức hoặc cleanup script.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 1 - Module MVP sau Catalog/Cart

1. Triển khai Checkout và Ordering với snapshot OrderItem.
2. Triển khai trừ tồn kho theo cấu hình MVP khi tạo/xác nhận đơn.
3. Triển khai Admin Order Management và OrderStatusHistory.
4. Triển khai Order Lookup cho customer.
5. Triển khai Banner Management và Dashboard.

### Ưu tiên 2 - Runtime/DevOps

6. Thêm Dockerfile cho backend API khi bắt đầu đóng gói app.
7. Thêm healthcheck/runtime documentation cho API + PostgreSQL.
8. Thêm API integration tests cho login, admin authorization, Admin Product, Storefront Catalog và Storefront Cart endpoints.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
git status --short
```
