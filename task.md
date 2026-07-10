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

---

# Task - Customer Loyalty Program

Ngay cap nhat: 2026-07-09

## Muc tieu

Trien khai tinh nang Khach hang than thiet cho khach hang da dang nhap:

1. Tu dong tich diem khi don hang cua customer chuyen sang `Completed`.
2. Tu dong nang hang thanh vien dua tren tong diem da tich luy.
3. Cho customer xem so du diem, hang hien tai, quyen loi hang va lich su giao dich diem.
4. Cho customer doi diem lay voucher giam gia customer-scoped de dung trong checkout.
5. Dam bao idempotency cho earn theo order va concurrency cho redeem.

## Quyet dinh MVP

- Guest order khong tich diem trong version dau. Chi order co `CustomerId` moi duoc earn.
- Diem earn tinh theo gia tri hang hoa da thanh toan, khong tinh shipping: `floor((Subtotal - DiscountAmount) * ExchangeRate / Loyalty:MoneyPerPoint)`.
- Default `Loyalty:MoneyPerPoint = 10000`.
- `CurrentPoints` la so du kha dung; `TotalPointsEarned` chi tang, khong giam khi redeem.
- Tier benefit trong version dau la display-only. Chua auto-apply tier discount/free shipping vao checkout.
- Redeem diem tao coupon/voucher trong schema `promotions`, gan voi customer va source loyalty.
- Loyalty voucher la fixed-amount coupon, gioi han 1 lan dung, het han sau 30 ngay.
- Idempotency earn enforce bang unique partial index cho transaction type `Earn` theo `OrderId`.
- Khong them domain event/outbox full trong version dau. `AdminOrderService` se goi application service khi status chuyen sang `Completed`.
- Neu order da `Completed` bi `Returned` sau do, version dau khong tu dong tru diem; ghi nhan limitation.

## Phase 0: Spec Alignment

- [x] 0.1 Cap nhat `docs/loyalty-program-spec.md` theo cac quyet dinh MVP o tren.
- [x] 0.2 Ghi ro guest order khong earn points.
- [x] 0.3 Ghi ro formula tinh diem, currency/exchange-rate, subtotal/discount/shipping handling.
- [x] 0.4 Ghi ro tier benefits display-only trong v1.
- [x] 0.5 Ghi ro loyalty voucher duoc build tren coupon hien co, can customer-scoped fields.
- [x] 0.6 Sua idempotency index thanh partial unique index chi ap dung cho Earn transaction.

## Phase 1: Domain Model

- [x] 1.1 Tao module domain `Loyalty` gom `CustomerLoyaltyAccount`, `LoyaltyTransaction`, `LoyaltyTier`.
- [x] 1.2 Tao enum `LoyaltyTierType` va `LoyaltyTransactionType`.
- [x] 1.3 Implement domain methods:
  - `EarnPoints(points, orderId, description)`.
  - `RedeemPoints(points, voucherId, description)`.
  - `TryEvaluateTierUpgrade(tierDefinitions)`.
- [x] 1.4 Validate domain rules: points > 0, khong am balance, order id bat buoc voi Earn, voucher id bat buoc voi Redeem.
- [x] 1.5 Them domain tests cho earn, redeem, tier upgrade, duplicate guards trong aggregate neu can.

## Phase 2: Persistence & Migration

- [x] 2.1 Them DbSet/IQueryable loyalty vao `IAppDbContext` va `AppDbContext`.
- [x] 2.2 Tao EF configurations cho `CustomerLoyaltyAccount`, `LoyaltyTransaction`, `LoyaltyTier`.
- [x] 2.3 Dat loyalty tables trong schema `loyalty`.
- [x] 2.4 Cau hinh relationship:
  - `CustomerLoyaltyAccount.CustomerId` unique FK den customer.
  - `LoyaltyTransaction.CustomerLoyaltyAccountId` FK den account.
  - `LoyaltyTransaction.OrderId` optional FK den order.
- [x] 2.5 Cau hinh optimistic concurrency cho account, uu tien PostgreSQL `xmin` neu phu hop voi repo.
- [x] 2.6 Them unique partial index cho Earn transaction: unique `order_id` where `type = Earn` and `order_id is not null`.
- [x] 2.7 Seed default tiers: Bronze, Silver, Gold, Platinum.
- [x] 2.8 Tao EF migration `AddLoyaltySchema`.
- [x] 2.9 Viet infrastructure configuration tests cho loyalty model.

## Phase 3: Coupon/Voucher Extension

- [x] 3.1 Mo rong `Coupon` de ho tro loyalty voucher:
  - `CustomerId` nullable.
  - `Source` hoac `CouponSource` enum (`Admin`, `Loyalty`).
  - optional `CreatedByLoyaltyTransactionId` neu can audit nguoc.
- [x] 3.2 Cap nhat `CouponConfiguration`, migration va DTO lien quan.
- [x] 3.3 Cap nhat checkout coupon evaluation de customer-scoped coupon chi dung duoc boi dung customer.
- [x] 3.4 Dinh nghia loyalty voucher code format, vi du `LOYAL-{shortId}`.
- [x] 3.5 Dinh nghia redeem conversion: `discountAmount = points * Loyalty:VoucherAmountPerPoint`.
- [x] 3.6 Default `Loyalty:VoucherAmountPerPoint = 1000`.
- [x] 3.7 Viet tests cho customer-scoped coupon availability va checkout validation.

## Phase 4: Application Services

- [x] 4.1 Tao `Modules/Loyalty` trong Application.
- [x] 4.2 Tao request/response DTOs:
  - `LoyaltyAccountDto`.
  - `LoyaltyTransactionDto`.
  - `LoyaltyTierDto`.
  - `RedeemLoyaltyPointsRequest`.
  - `RedeemLoyaltyPointsResponse`.
- [x] 4.3 Tao validators cho transactions paging va redeem request.
- [x] 4.4 Tao `ILoyaltyService` voi:
  - `GetMyLoyaltyAsync`.
  - `GetMyTransactionsAsync`.
  - `GetTiersAsync`.
  - `RedeemPointsAsync`.
  - `EarnForCompletedOrderAsync`.
- [x] 4.5 Implement `EarnForCompletedOrderAsync`:
  - Skip neu order khong co `CustomerId`.
  - Skip thanh cong neu transaction Earn cho order da ton tai.
  - Tao account neu customer chua co account.
  - Tinh points theo config.
  - Ghi transaction Earn va update tier.
- [x] 4.6 Implement `RedeemPointsAsync` trong transaction:
  - Load account cua current customer.
  - Validate balance.
  - Tao loyalty coupon customer-scoped.
  - Ghi transaction Redeem voi voucher id/coupon id.
  - Save voi concurrency handling.
- [x] 4.7 Mapping Result status: validation, unauthorized, not found, conflict.
- [x] 4.8 Dang ky DI cho loyalty services va options.

## Phase 5: Order Integration

- [x] 5.1 Mo rong `AdminOrderService` de nhan `ILoyaltyService`.
- [x] 5.2 Khi `UpdateStatusAsync` chuyen status thanh `Completed`, goi `EarnForCompletedOrderAsync` sau khi order status/history save thanh cong.
- [x] 5.3 Dam bao loyalty earn failure duplicate khong lam fail update order.
- [x] 5.4 Neu loyalty earn gap loi he thong that su, tra conflict/failure hay log-only can quyet dinh theo implementation; uu tien khong lam mat trang thai order da save.
- [x] 5.5 Viet tests cho update order status to `Completed` trigger earn.
- [x] 5.6 Viet tests duplicate earn khong cong diem lan 2.

## Phase 6: API

- [x] 6.1 Tao `LoyaltyController` cho storefront/customer endpoints.
- [x] 6.2 Implement `GET /api/loyalty/me` yeu cau customer auth.
- [x] 6.3 Implement `GET /api/loyalty/me/transactions?page=1&pageSize=20` yeu cau customer auth.
- [x] 6.4 Implement `POST /api/loyalty/me/redeem` yeu cau customer auth.
- [x] 6.5 Implement `GET /api/loyalty/tiers` public.
- [x] 6.6 Dung response wrapper hien co va authorization policy/role hien co.
- [x] 6.7 Viet API integration tests cho endpoints loyalty.

## Phase 7: Frontend Shared Types & Client

- [x] 7.1 Them loyalty types vao `frontend/packages/api-types`.
- [x] 7.2 Them loyalty methods vao `frontend/packages/api-client`.
- [x] 7.3 Cap nhat storefront API service neu app dang dung wrapper rieng.
- [x] 7.4 Chay typecheck shared packages.

## Phase 8: Storefront UI

- [x] 8.1 Them loyalty section/page trong account area.
- [x] 8.2 Hien thi current points, total earned, current tier, next tier progress.
- [x] 8.3 Hien thi tier benefits table/list tu `GET /api/loyalty/tiers`.
- [x] 8.4 Hien thi transaction history co pagination co ban.
- [x] 8.5 Them redeem form:
  - Nhap so diem.
  - Preview discount amount.
  - Submit redeem.
  - Hien voucher code sau khi tao.
- [x] 8.6 Dam bao UI chi hien cho customer da dang nhap.
- [x] 8.7 Khong build admin UI quan ly tier trong v1.

## Phase 9: Verification

- [x] 9.1 Chay unit tests Application/Domain lien quan loyalty.
- [x] 9.2 Chay Infrastructure tests cho EF configuration/migration model.
- [x] 9.3 Chay API integration tests lien quan order completed, loyalty, coupon checkout.
- [x] 9.4 Chay backend build/test solution.
- [x] 9.5 Chay frontend typecheck/build cho storefront va shared packages.
- [x] 9.6 Test manual flow:
  - Customer login.
  - Checkout order.
  - Admin chuyen order den `Completed`.
  - Customer thay diem tang.
  - Customer redeem voucher.
  - Customer dung voucher loyalty trong checkout tiep theo.
  - Verified bang API integration test `LoyaltyManualFlow_CheckoutCompleteEarnRedeemAndUseVoucher`.
  - Note: full API integration suite hien con 5 loi ngoai pham vi loyalty/order/coupon checkout o dashboard/catalog/customer tests.
