# Backend

Backend là modular monolith trên .NET/ASP.NET Core, EF Core và PostgreSQL. Một API và các hosted workers xử lý nghiệp vụ; storefront/admin giao tiếp qua HTTP và SignalR. Hướng dẫn chạy nằm tại [Development guide](development/guide.md).

## Kiến trúc và trách nhiệm

```text
Api ----------> Application ----------> Domain
 |                   ^
 +--> Infrastructure-+----------------> Domain
```

| Project trong `src/` | Trách nhiệm |
| --- | --- |
| `WorkspaceEcommerce.Domain` | Entity, invariant và lifecycle; không phụ thuộc HTTP, EF hay provider |
| `WorkspaceEcommerce.Application` | Use case, DTO, FluentValidation, result, ports và điều phối transaction |
| `WorkspaceEcommerce.Infrastructure` | EF mapping/migration, PostgreSQL locks, auth implementations, provider adapters, media/PDF và workers |
| `WorkspaceEcommerce.Api` | Composition root, controllers, middleware, auth/CORS/rate limit, health và SignalR |

Application chủ động dùng EF query qua focused stores và `IAppDbContext`; đây là quyết định trong [ADR 001](adr/001-direct-dbcontext.md). Không thêm generic repository, MediatR/CQRS hay event bus để thay thế cấu trúc hiện tại. Controller chuyển đổi HTTP và gọi use case; quy tắc nghiệp vụ thuộc Application/Domain.

## Module và dữ liệu chính

Module nằm dưới `Domain/Modules` và `Application/Modules`; implementation persistence/provider nằm trong Infrastructure.

| Module | Phạm vi và quan hệ chính |
| --- | --- |
| Catalog, Reviews | Category → Product → Variant; giá và tồn kho trên variant; ảnh, specification và review trên product |
| Content, Blogs | Banner, bài viết, liên kết sản phẩm và bình luận |
| Cart, Ordering, Coupons | Giỏ theo session/customer; order giữ item/customer/address/coupon/money snapshots và status history |
| Payments, Shipments | VNPay attempts; shipment theo order, tracking/timeline, webhook inbox và command outbox |
| Customers | Account, address, profile, login history, TOTP, account-action tokens và refresh-token families |
| Loyalty | Account balance, point ledger, tier và voucher dưới dạng customer-scoped coupon |
| Media | Asset metadata/lifecycle và tham chiếu đến object bytes |
| Warranties | Plan/coverage, serialized unit, entitlement theo order item/customer, snapshot và audit |
| Admin, Operations | Dashboard, quản trị và kiểm tra/replay durable outbox |

[Data and consistency](architecture/data-and-consistency.md) sở hữu schema, thứ tự lock, transaction và dữ liệu lịch sử. [Business rules](product/business-rules.md) sở hữu invariant và bảng chuyển trạng thái.

## Quyền truy cập

| Actor | Phạm vi |
| --- | --- |
| Guest | Catalog/content, giỏ theo session, checkout API và tra cứu order/tracking/receipt bằng order code + phone |
| Customer | Profile/address, đơn của mình, cancel/return, loyalty, session/2FA, notification và bảo hành được bật |
| Admin | Catalog/content/coupon/order/review/media/warranty, dashboard và recovery/outbox operations |
| Provider | Payment callback hoặc signed shipment webhook theo contract riêng |

Role và ownership là hai bước kiểm tra riêng. API cho phép guest checkout và customer chưa xác thực email; **storefront hiện bảo vệ trang checkout bằng đăng nhập**. Route guard chỉ kiểm soát UI, không thay thế authorization phía server. Chi tiết HTTP tại [API conventions](api/conventions.md).

## Luồng chính và điểm đọc source

### Request thông thường

[Program.cs](../../src/WorkspaceEcommerce.Api/Program.cs) đăng ký middleware, authentication, rate limiting, health, SignalR và DI. Controller gọi Application service, validator kiểm tra input, entity giữ invariant, persistence ghi dữ liệu rồi API trả envelope. DI nghiệp vụ nằm tại [Application/DependencyInjection.cs](../../src/WorkspaceEcommerce.Application/DependencyInjection.cs); implementation và workers tại [Infrastructure/DependencyInjection.cs](../../src/WorkspaceEcommerce.Infrastructure/DependencyInjection.cs).

### Checkout → payment → shipment

1. [CheckoutService](../../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutService.cs) kiểm tra giỏ và điều phối quote. VNPay lấy quote trước placement; COD/chuyển khoản cập nhật phí sau commit.
2. [CheckoutCartBuilder](../../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutCartBuilder.cs) đọc theo tập ID, kiểm tra catalog/tồn kho và tạo snapshots. Giá item lấy từ `UnitPriceSnapshot` của giỏ.
3. [CheckoutOrderPlacer](../../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderPlacer.cs) chạy transaction: khóa variant/coupon, tạo order, giữ coupon usage, trừ tồn kho và xóa giỏ. COD enqueue shipment-create trong transaction; VNPay tạo payment attempt.
4. [PaymentService](../../src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs) xử lý callback VNPay, chốt kết quả idempotently và enqueue shipment khi thanh toán thành công.
5. [ShipmentCommandWorker](../../src/WorkspaceEcommerce.Infrastructure/Shipment/ShipmentCommandWorker.cs) gọi provider để tạo/hủy shipment từ outbox. Webhook/refresh cập nhật shipment và tiến trạng thái order hợp lệ; đơn hoàn tất kích hoạt cộng điểm loyalty.

Hợp đồng tích hợp nằm tại [Services](../services/README.md). Chuyển khoản chưa có luồng xác nhận thanh toán; lỗi quote sau commit có thể để lại phí vận chuyển ban đầu bằng 0. Các giới hạn này được quản lý tại [Known limitations](development/known-limitations.md).

### Account, bảo hành và tác vụ nền

- Account: [Customers/Authentication](../../src/WorkspaceEcommerce.Application/Modules/Customers/Authentication) xử lý login, account lifecycle và refresh rotation; email được enqueue, không gửi SMTP trong request transaction. [ADR 002](adr/002-customer-totp-authentication.md) và [ADR 003](adr/003-customer-account-lifecycle.md) giải thích thiết kế identity.
- Bảo hành: [WarrantyActivationCoordinator](../../src/WorkspaceEcommerce.Application/Modules/Warranties/WarrantyActivationCoordinator.cs) dùng chung cho admin/customer, khóa unit → entitlement → order, snapshot coverage và enqueue email trong transaction. Feature flags mặc định tắt; identifier dùng masked value và HMAC theo [ADR 005](adr/005-warranty-activation-and-identifier-protection.md).
- Workers: shipment/email outbox delivery, outbox metrics, account retention cleanup và media cleanup. Account cleanup dùng bounded batches, time budget và PostgreSQL advisory lock. Recovery theo [Outbox runbook](../services/runbooks/background-outbox-operations.md).

## Khi sửa Backend

Theo chiều controller → use case/validator → entity → persistence/provider → tests. Giữ contract, ownership, transaction, snapshots và idempotency; không sửa migration đã áp dụng. Nếu đổi API, cập nhật `frontend/packages/api-types` và các consumer liên quan.

[Testing and quality](development/testing.md) quy định lệnh và coverage; concurrency, EF translation, migrations và database semantics cần API/PostgreSQL integration tests. Biên nhận PDF và kiểu tiền có tham chiếu riêng tại [Order receipts](order-receipts.md) và [Currency and money](currency-and-money.md).
