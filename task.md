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

Backend đã có nền tảng Clean Architecture Modular Monolith cho Catalog, Cart, Checkout và Ordering:

- `WorkspaceEcommerce.Domain`: Catalog, Cart và Ordering entities với invariant/domain method cơ bản.
- `WorkspaceEcommerce.Application`: common contracts/models, DTO, validators và services cho Admin Auth, Admin Catalog, Admin Product, Admin Order, Storefront Catalog, Storefront Cart, Checkout và Storefront Order Lookup.
- `WorkspaceEcommerce.Infrastructure`: EF Core PostgreSQL persistence, Catalog/Cart/Ordering mappings, migrations, JWT/config validation và transaction-backed checkout store.
- `WorkspaceEcommerce.Api`: controller mỏng cho Admin Auth, Admin Catalog, Admin Product, Admin Order, Storefront Catalog, Storefront Cart, Checkout và Storefront Order Lookup; có JWT, response envelope, global exception handling và OpenAPI trong Development.
- `WorkspaceEcommerce.Application.Tests`: tests cho Domain, validators và Application services thuộc Catalog, Cart, Auth, Product, Storefront Catalog, Checkout, Order Lookup và Admin Order.
- `WorkspaceEcommerce.Infrastructure.Tests`: tests cho configuration validation, JWT token generation và EF Core mappings của Catalog, Cart, Ordering.
- `WorkspaceEcommerce.Api.IntegrationTests`: hạ tầng API integration test dùng `WebApplicationFactory` và Testcontainers PostgreSQL.
- PostgreSQL local chạy bằng Docker Compose service `postgres`.

Dependency hiện tại:

- `Domain` không phụ thuộc Application, Infrastructure, API hoặc EF Core.
- `Application` phụ thuộc `Domain` và định nghĩa abstraction như `IAppDbContext`, `ICartStore`, `ICheckoutStore`.
- `Infrastructure` phụ thuộc `Application` và `Domain`, triển khai EF Core/PostgreSQL, JWT và config validation.
- `Api` phụ thuộc `Application` và `Infrastructure`, không chứa business logic.

## Đã hoàn thành

### Agent rules và backend foundation

- Tạo `.agent` với rule cho architecture, coding, backend, API, data, frontend, quality, workflow và skill.
- Tạo solution/project structure trong `src/` và `tests/`.
- Thêm `.gitignore` cho `bin/`, `obj/`, `.idea/`, log, coverage và file môi trường local.
- Commit/push backend foundation ban đầu lên `origin/main`.

### Catalog module

- Triển khai Domain entities: `Category`, `Product`, `ProductVariant`, `ProductImage`, `ProductSpecification`.
- Domain không có EF attribute hoặc EF package reference.
- Entity dùng private setter khi phù hợp.
- Thêm domain behavior cho activate/deactivate, update, parent category, pricing, stock, image/specification.
- Thêm EF Core mappings cho schema `catalog` bằng `IEntityTypeConfiguration<T>`.
- Mapping PostgreSQL-compatible với required fields, max length, relationships, indexes, delete behavior và decimal precision.
- Unique index cho category slug, product slug và variant SKU.
- Tạo migration `InitialCatalogSchema`.

### Application common contracts/models

- Thêm `IAppDbContext`.
- Thêm `Result`, `Result<T>`, `ResultStatus`, `PagedResult<T>`, `PaginationRequest`.
- Application không expose EF Core implementation.

### Admin Authentication

- Thêm API `POST /api/admin/auth/login`.
- Login dùng email/password theo phạm vi MVP.
- Credential admin đọc từ configuration `AdminAuth`.
- JWT đọc từ configuration `Jwt`.
- Không commit password hoặc signing key thật; appsettings chỉ giữ placeholder.
- Thêm JWT Bearer authentication.
- Bảo vệ `/api/admin/*` endpoints đã triển khai bằng `[Authorize]`.
- Invalid login trả `401 Unauthorized`, không expose thông tin nội bộ.

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
- Dùng `IsActive` cho Product và ProductVariant.
- Hỗ trợ category assignment, featured flag, description, SKU, price, compare-at price, stock và `RequiresInstallation`.
- Chặn category không tồn tại, duplicate product slug và duplicate SKU không phân biệt hoa/thường.
- Chưa triển khai Product image/specification API vì task trước chỉ yêu cầu Product và Variant endpoints.

### Storefront Catalog read APIs

- Thêm API public:
  - `GET /api/categories`
  - `GET /api/products`
  - `GET /api/products/{slug}`
- Chỉ trả active category/product/variant.
- Product listing có pagination và filter MVP: `categorySlug`, `search`, `minPrice`, `maxPrice`, `inStock`.
- Product detail trả active variants; images/specifications không có `IsActive` trong data model nên trả theo product.

### Cart module

- Triển khai Domain entities: `Cart`, `CartItem`.
- Cart invariant:
  - Cart phải thuộc customer hoặc session.
  - CartItem quantity phải lớn hơn 0.
  - `UnitPriceSnapshot` không âm.
  - Tổng tiền tính từ `UnitPriceSnapshot * Quantity`.
- Application service `IStorefrontCartService` xử lý lấy cart, thêm item, cập nhật quantity, xóa item.
- Validators cho `GetCartRequest`, `AddCartItemRequest`, `UpdateCartItemRequest`, `RemoveCartItemRequest`.
- Chỉ cho add/update active variant thuộc active product và active category.
- Kiểm tra tồn kho cơ bản trước khi add/update quantity.
- Cart item lưu `UnitPriceSnapshot` khi thêm item mới.
- Khi add cùng variant vào cart, service tăng quantity và giữ price snapshot cũ.

### Cart persistence và Storefront Cart APIs

- Thêm EF Core mappings cho schema `cart`.
- Tạo bảng `cart.carts` và `cart.cart_items`.
- Mapping PostgreSQL-compatible:
  - `session_id` max length 128.
  - `unit_price_snapshot numeric(18,2)`.
  - FK `cart_items.cart_id -> cart.carts.id` dùng cascade delete.
  - FK `cart_items.product_variant_id -> catalog.product_variants.id` dùng restrict delete.
- Thêm index lookup và unique index `(cart_id, product_variant_id)`.
- Tạo migration `AddCartSchema`.
- `AppDbContext` triển khai `ICartStore`.
- Thêm API public:
  - `GET /api/cart`
  - `POST /api/cart/items`
  - `PUT /api/cart/items/{id}`
  - `DELETE /api/cart/items/{id}`
- Đã smoke-test đủ 4 endpoints trên PostgreSQL local.
- Đã fix lỗi EF tracking khi tạo cart mới: cart mới chỉ `Add(cart)`, không `Update(cart)`.

### Checkout và Ordering Domain/Application

- Triển khai Ordering Domain entities:
  - `Order`
  - `OrderItem`
  - `OrderStatusHistory`
- Triển khai enum/value:
  - `OrderStatus`
  - `PaymentMethod`
- Order tạo mặc định trạng thái `Pending`.
- OrderItem lưu snapshot: product name, SKU, unit price, quantity, requires installation.
- Order tính `Subtotal`, `ShippingFee`, `DiscountAmount`, `TotalAmount` từ snapshot, không tin client gửi tổng tiền.
- Chưa triển khai shipping fee và discount thực tế; hiện giữ `0` theo quyết định scope.
- Trừ tồn kho khi tạo order theo quyết định scope hiện tại.
- Thêm `ProductVariant.DecreaseStock(int)` để bảo vệ invariant tồn kho.
- Thêm Application DTO/service/validator:
  - `CheckoutRequest`
  - `CheckoutResponse`
  - `OrderDto`
  - `OrderItemDto`
  - `CheckoutRequestValidator`
  - `ICheckoutService`
  - `CheckoutService`
  - `ICheckoutStore`
- Checkout service xử lý empty cart, invalid contact, inactive category/product/variant, insufficient stock, snapshot và create order.

### Ordering persistence và Checkout API

- `AppDbContext` triển khai `ICheckoutStore`.
- Thêm DbSet:
  - `Orders`
  - `OrderItems`
  - `OrderStatusHistories`
- Thêm EF Core mappings cho schema `ordering` bằng `IEntityTypeConfiguration<T>`.
- Tạo migration `AddOrderingSchema`.
- Migration tạo bảng:
  - `ordering.orders`
  - `ordering.order_items`
  - `ordering.order_status_history`
- Mapping PostgreSQL-compatible:
  - Order money fields và OrderItem unit price dùng `numeric(18,2)`.
  - `Order.Status` và `PaymentMethod` lưu dạng string max length 50.
  - Unique index `ux_orders_order_code`.
  - Index lookup cho customer phone, status, order item FK và status history.
  - FK `order_items.order_id -> ordering.orders.id` dùng cascade delete.
  - FK `order_items.product_variant_id -> catalog.product_variants.id` dùng restrict delete.
  - FK `order_status_history.order_id -> ordering.orders.id` dùng cascade delete.
- Checkout chạy trong transaction.
- Khi checkout, variant được query bằng PostgreSQL `FOR UPDATE` trong transaction để giảm rủi ro oversell.
- Thêm API public:
  - `POST /api/checkout`
- Controller checkout mỏng, delegate sang `ICheckoutService` và trả response envelope thống nhất.

### Storefront Order Lookup

- Thêm API public:
  - `GET /api/orders/lookup`
- Tra cứu đơn hàng bằng `orderCode` và `phone`.
- Dùng DTO request/response và FluentValidation.
- Controller mỏng, delegate sang `IStorefrontOrderLookupService`.
- Business logic lookup nằm trong Application service.
- Normalize `orderCode` bằng trim + uppercase trước khi lookup.
- Normalize `phone` bằng trim trước khi lookup.
- Chỉ trả order khi cả order code và phone cùng khớp.
- Không expose entity trực tiếp ra API; response dùng `OrderDto` và `OrderItemDto`.
- Order item response dùng snapshot đã lưu trong `ordering.order_items`, không đọc lại dữ liệu product hiện tại.

### Admin Order Management

- Thêm API admin:
  - `GET /api/admin/orders`
  - `GET /api/admin/orders/{id}`
  - `PUT /api/admin/orders/{id}/status`
- Các endpoint được bảo vệ bằng `[Authorize]`.
- Controller mỏng, delegate sang `IAdminOrderService`.
- Dùng DTO request/response và FluentValidation.
- `GET /api/admin/orders` hỗ trợ pagination MVP và filter cơ bản theo `status`, `search`.
- `GET /api/admin/orders/{id}` trả chi tiết order, order items snapshot và status history.
- `PUT /api/admin/orders/{id}/status` dùng `Order.ChangeStatus(...)` để enforce transition rules trong Domain.
- Mỗi lần đổi trạng thái ghi thêm `OrderStatusHistory`.
- `changedBy` lấy từ JWT claim admin hiện tại.
- Invalid transition trả `409 Conflict`.

### API foundation

- Thêm global exception middleware.
- Unexpected exception trả response `500` an toàn, không expose stack trace, SQL error hoặc internal exception message.
- Chuẩn hóa response bằng `ApiResponse<T>`.
- Thêm mapper từ Application `Result` sang HTTP response.
- Chuẩn hóa model-state error response của `[ApiController]`.
- Thêm OpenAPI bằng `Microsoft.AspNetCore.OpenApi`.
- Chỉ bật `/openapi/v1.json` trong Development.

### API integration tests

- Thêm project `WorkspaceEcommerce.Api.IntegrationTests`.
- Thêm project vào `WorkspaceEcommerce.slnx`.
- Dùng `Microsoft.AspNetCore.Mvc.Testing` với `WebApplicationFactory<Program>`.
- Dùng `Testcontainers.PostgreSql` để khởi động PostgreSQL thật cho integration tests.
- Fixture integration test:
  - Start PostgreSQL container `postgres:17-alpine`.
  - Override runtime config bằng environment variables test-only trước khi API host khởi động.
  - Apply EF Core migrations bằng `AppDbContext.Database.MigrateAsync()`.
  - Restore environment variables cũ khi dispose.
  - Dispose API factory và PostgreSQL container sau test.
- Thêm smoke test hạ tầng `GET /api/categories` để xác nhận API host + migrated PostgreSQL container hoạt động.
- Thêm reset database helper dùng PostgreSQL `TRUNCATE ... CASCADE` giữa các integration tests.
- Thêm seed helper dùng `AppDbContext` và Domain entities.
- Thêm integration tests cho:
  - Auth login hợp lệ.
  - Admin authorization khi thiếu bearer token.
  - Storefront Catalog: categories, products listing và product detail.
  - Cart add item.
  - Checkout tạo order, trừ stock và xóa cart.
  - Storefront Order Lookup đúng phone và sai phone.
  - Admin Order list, detail và update status.

### Configuration validation

- Validate `ConnectionStrings:DefaultConnection` sớm khi app start qua Infrastructure DI.
- Validate `AdminAuth` và `Jwt` sớm khi app start.
- Chặn config thiếu, sai format hoặc còn placeholder.
- Signing key JWT phải đủ tối thiểu 32 bytes cho HS256.
- `appsettings.json` và `appsettings.Development.json` chỉ giữ placeholder, không chứa secret thật.
- Runtime local cần override bằng user-secrets, environment variable hoặc local config không commit.

### Database migration local

- Đã thêm `docker-compose.yml` cho PostgreSQL local.
- Đã thêm `.env.example`; file `.env` local không commit vì chứa password dev.
- Đã chạy PostgreSQL container `workspace-ecommerce-postgres`.
- Đã apply migration `InitialCatalogSchema`, `AddCartSchema` và `AddOrderingSchema` vào PostgreSQL local.
- Đã verify trực tiếp schema/table/index/precision/delete behavior cho `catalog`, `cart` và `ordering`.
- Đã seed dữ liệu smoke-test tối thiểu cho Cart API.
- Đã seed/dùng dữ liệu Cart hiện có để smoke-test `POST /api/checkout` trên PostgreSQL local.
- Đã verify trực tiếp PostgreSQL sau checkout:
  - order được tạo trong `ordering.orders`.
  - order items snapshot đúng trong `ordering.order_items`.
  - status history có record khởi tạo trong `ordering.order_status_history`.
  - stock variant bị trừ đúng.
  - cart đã checkout được remove theo implementation hiện tại.

### Tests

- Application tests cho Admin Category validator/service.
- Application tests cho Admin Auth validator/service.
- Application tests cho Admin Product validator/service.
- Application tests cho Storefront Catalog read service.
- Application tests cho Storefront Cart validator/service.
- Application tests cho Checkout validator/service.
- Application tests cho Storefront Order Lookup validator/service.
- Application tests cho Admin Order validator/service.
- Domain tests cho Catalog, Cart và Ordering invariants.
- Infrastructure tests cho configuration validation và JWT token generation.
- Infrastructure tests cho EF Core Catalog/Cart/Ordering mapping.
- API integration tests dùng PostgreSQL thật qua Testcontainers.
- Persistence mapping tests không dùng EF InMemory cho behavior cần đúng với PostgreSQL metadata.

## Xác minh gần nhất

Đã chạy sau task API integration endpoint coverage:

```powershell
dotnet build WorkspaceEcommerce.slnx
```

Kết quả:

- Build succeeded.
- Warnings: 0.
- Errors: 0.

Đã chạy:

```powershell
dotnet test WorkspaceEcommerce.slnx --no-build
```

Kết quả:

- `WorkspaceEcommerce.Application.Tests`: 113 passed.
- `WorkspaceEcommerce.Api.IntegrationTests`: 6 passed.
- `WorkspaceEcommerce.Infrastructure.Tests`: 54 passed.
- Tổng test suite: 173 passed.
- Failed: 0.
- Skipped: 0.

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

## Rủi ro và khoảng trống

- Vì config dùng placeholder, app sẽ fail sớm nếu chưa override `DefaultConnection`, `AdminAuth` và `Jwt` bằng secret/config local hợp lệ.
- API integration tests đã cover luồng chính Auth/Admin authorization, Catalog, Cart, Checkout, Order Lookup và Admin Order; vẫn chưa cover đầy đủ mọi edge case API.
- Chưa có Banner Management và Dashboard.
- Chưa có Dockerfile/backend container; hiện mới có PostgreSQL container.
- Dữ liệu smoke-test local đã được insert vào PostgreSQL dev; nếu cần DB sạch cho demo thì cần seed strategy chính thức hoặc cleanup script.

## Nhiệm vụ tiếp theo đề xuất

### Ưu tiên 1 - API/integration quality

1. Bổ sung API integration tests cho edge cases còn thiếu: validation errors, duplicate conflicts, inactive catalog/cart/checkout failure, invalid order status transition.
2. Thêm Dockerfile cho backend API và tài liệu chạy API + PostgreSQL bằng Docker Compose.

### Ưu tiên 2 - Phần MVP sau Ordering

3. Triển khai Banner Management.
4. Triển khai Dashboard cơ bản.
5. Chuẩn hóa seed data demo cho Catalog/Cart/Checkout/Order.

## Lệnh nên chạy trước task tiếp theo

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test WorkspaceEcommerce.slnx
git status --short
```
