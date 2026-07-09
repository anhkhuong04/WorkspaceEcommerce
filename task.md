# Task - News/Blogs Feature

Ngay cap nhat: 2026-06-29

## Muc tieu

Hoan thien tinh nang dang bai News/Blogs lien quan den san pham:

1. Admin: Tao, sua, xoa, dang/go bai viet; xem va xoa comment.
2. Storefront: Xem danh sach bai viet da dang, xem chi tiet bai viet kem san pham lien quan, va gui binh luan.

## Quyet dinh MVP

- Toan bo table cua blogs se nam trong schema `content`.
- Bai viet can slug doc nhat de lam URL dep.
- San pham lien quan duoc chon trong Admin va hien thi duoi dang card o cuoi bai viet.
- Binh luan se tu dong duoc duyet (`IsApproved = true` mac dinh) de de demo, nhung Admin co the xoa bat ky comment nao.
- Ho tro ca guest comment va authenticated customer comment.

## Tích Hợp MiniLogistics Shipment - Tasks

## Phase 1: Domain & Infrastructure Backend

- [x] 1.1 Thêm WeightKg, LengthCm, WidthCm, HeightCm vào `ProductVariant`
- [x] 1.2 Cập nhật `ProductVariantConfiguration` EF
- [x] 1.3 Thêm TrackingCode, ShipmentId vào `Order` entity + method SetShippingFee, UpdateShipmentInfo
- [x] 1.4 Cập nhật `OrderConfiguration` EF
- [x] 1.5 Tạo EF Migration `AddShipmentFields`

## Phase 2: Application Layer

- [x] 2.1 Tạo `IMiniLogisticsClient` interface + DTOs
- [x] 2.2 Tạo `MiniLogisticsClient` implementation
- [x] 2.3 Tạo `MiniLogisticsOptions` config class
- [x] 2.4 Đăng ký DI trong `Infrastructure/DependencyInjection.cs`
- [x] 2.5 Cập nhật `appsettings.json` / `appsettings.Development.json`
- [x] 2.6 Cập nhật `docker-compose.yml` env vars
- [x] 2.7 Tạo `ShippingQuoteRequest/Response` DTOs
- [x] 2.8 Cập nhật `ICheckoutService` + `CheckoutService` (thêm GetShippingQuoteAsync, tích hợp shipment vào checkout)

## Phase 3: API Controllers

- [x] 3.1 Thêm endpoint `POST /api/checkout/shipping-quote` vào `CheckoutController`
- [x] 3.2 Tạo `MiniLogisticsWebhookController` (webhook endpoint)

## Phase 4: Frontend

- [x] 4.1 Thêm `ShippingQuoteRequest/Response` types vào `api-types`
- [x] 4.2 Thêm `getShippingQuote` method vào `api-client`
- [x] 4.3 Cập nhật `CheckoutPage.tsx` (auto-call shipping quote, hiện phí ship)

## Phase 5: Seed Data & Migration

- [x] 5.1 Cập nhật `DemoDataSeeder` thêm weight/dimensions cho variants
- [x] 5.2 Build + migrate + test (Fix typecheck frontend, unit tests, integration tests - All passed!)

## Cac Phase Trien Khai (News/Blogs)

- [x] **P1: Domain & Persistence**
  - [x] Tao entity `BlogPost.cs`, `BlogPostRelatedProduct.cs`, `BlogComment.cs` trong Domain.
  - [x] Viet configurations map sang schema `content`.
  - [x] Cap nhat `AppDbContext` va `IAppDbContext`.
  - [x] Tao migration EF Core `AddBlogSchema` va run migration.
  - [x] Viet model configuration tests.

- [x] **P2: Application Module**
  - [x] Tao folder `Modules/Blogs` trong Application.
  - [x] DTO/request/validators cho Admin va Storefront.
  - [x] `AdminBlogService` (CRUD posts, fetch/delete comments).
  - [x] `StorefrontBlogService` (list posts, get post detail, submit comment).

- [x] **P3: Web API Controllers**
  - [x] Controller `Admin/BlogsController.cs` (CRUD posts, status toggle, manage comments).
  - [x] Controller `BlogsController.cs` (Storefront listing, detail, comment submission).

- [x] **P4: Frontend Shared Types & Client**
  - [x] Cap nhat `api-types` voi blogs/comments types.
  - [x] Cap nhat `api-client` voi blogs admin/storefront methods.

- [x] **P5: Admin UI**
  - [x] Them nav item va route `/blogs` trong Admin portal.
  - [x] `BlogListPage.tsx` (danh sach posts, status toggle, actions).
  - [x] `BlogEditorPage.tsx` (edit details, markdown/text content editor, related products picker).
  - [x] Tab comment moderation trong editor.

- [x] **P6: Storefront UI**
  - [x] Link route `/news` vao header item "News".
  - [x] `BlogListPage.tsx` (danh sach posts grid/cards).
  - [x] `BlogDetailPage.tsx` (noi dung post, list related products, comment section).

- [x] **P7: Verification & Testing**
  - [x] Viet unit/domain tests cho blogs/comments.
  - [x] Viet API integration tests cho admin/storefront blog flows.
  - [x] Chay typecheck va build tren ca storefront va admin.
