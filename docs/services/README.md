# Services — dịch vụ bên ngoài

Tài liệu này mô tả các hợp đồng tích hợp mà WorkspaceEcommerce đang dùng. MiniLogistics và các provider không nằm trong repository; backend tích hợp qua ports và Infrastructure adapters. Application services và workers được mô tả tại [Backend](../backend/README.md).

## Bản đồ tích hợp

| Dịch vụ | Mục đích | Điểm đọc implementation | Cấu hình |
| --- | --- | --- | --- |
| VNPay | Payment redirect, return và IPN | [VNPayPaymentService](../../src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs), [PaymentService](../../src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs) | `Payment:VNPay:*`, `Storefront:BaseUrl` |
| MiniLogistics | Shipping quote, create/cancel shipment, tracking/webhook | [MiniLogisticsClient](../../src/WorkspaceEcommerce.Infrastructure/Shipment/MiniLogisticsClient.cs) | `MiniLogistics:*` |
| SMTP | Email xác thực, reset mật khẩu và thông báo | [Notifications](../../src/WorkspaceEcommerce.Infrastructure/Notifications) | `EmailDelivery:*` |
| Google | Xác thực Google ID token | [GoogleIdTokenValidator](../../src/WorkspaceEcommerce.Infrastructure/Authentication/GoogleIdTokenValidator.cs) | `GoogleAuth:*`, `VITE_GOOGLE_CLIENT_ID` |
| S3-compatible storage / MinIO | Object bytes của media | [Media](../../src/WorkspaceEcommerce.Infrastructure/Media) | `MediaStorage:*` |
| Application Insights | Telemetry phía API | [Program.cs](../../src/WorkspaceEcommerce.Api/Program.cs), [redaction processor](../../src/WorkspaceEcommerce.Api/Observability/SensitiveTelemetryRedactionProcessor.cs) | `APPLICATIONINSIGHTS_CONNECTION_STRING` / `ApplicationInsights:ConnectionString` |

[Configuration matrix](runbooks/configuration-matrix.md) quản lý các nhóm cấu hình, chính sách môi trường, owner và rotation. Provider portal quản lý credentials và đặc tả phía provider; các phần dưới đây mô tả adapter hiện tại của repository.

## VNPay

Checkout lưu pending payment attempt rồi trả redirect URL. Browser chuyển người dùng đến VNPay; thông tin thẻ/ngân hàng không đi qua WorkspaceEcommerce.

| Chiều vào API | Contract |
| --- | --- |
| `GET /api/payments/vnpay/return` | Xử lý callback rồi redirect về storefront payment-result |
| `GET` hoặc `POST /api/payments/vnpay/ipn` | Xử lý server callback, trả acknowledgement theo protocol VNPay |
| `GET /api/payments/result` | Tra kết quả bằng order code và `X-Payment-Result-Token` do backend cấp |

Return/IPN cùng kiểm tra HMAC, các trường bắt buộc, transaction reference và số tiền dương khớp payment attempt đã lưu. Thanh toán thành công yêu cầu response code và transaction status cùng `00`. Khi chốt kết quả, backend khóa transaction/order, xử lý idempotently và enqueue shipment-create trong cùng transaction nếu thanh toán thành công.

Frontend query parameter chỉ phục vụ điều hướng; payment state được đọc từ backend. Production return/IPN phải là public HTTPS đúng đăng ký provider. Không log secret hay raw callback material. Xem [PaymentIntegrationTests](../../tests/WorkspaceEcommerce.Api.IntegrationTests/Payments/PaymentIntegrationTests.cs).

## MiniLogistics

`MiniLogistics:BaseUrl` là partner API root. Các path dưới đây tương đối với root này:

| Operation | Request | Cách thực thi |
| --- | --- | --- |
| Quote | `POST shipping/quote` | Đồng bộ trong preview/checkout |
| Create | `POST shipments` | Command outbox; `Idempotency-Key` là order code |
| Tracking | `GET shipments/{trackingCode}` | Read hoặc explicit refresh |
| Cancel | `POST shipments/{trackingCode}/cancel` | Command outbox; key `<order-code>:cancel` |

Provider gọi `POST /api/webhooks/minilogistics` với `X-MiniLogistics-Timestamp` trong configured tolerance và `X-MiniLogistics-Signature = sha256=<hex HMAC-SHA256>` của `<timestamp>.<raw-body>`. API xác thực trước khi xử lý JSON. Events được hỗ trợ: `shipment.created`, `shipment.status_changed`, `webhook.test` (probe không đổi business state).

Inbox loại bỏ xử lý trùng theo event ID và timeline giữ lịch sử; provider event cũ không làm lùi shipment state. Pickup/transit tiến order qua trạng thái hợp lệ, Delivered → Completed, failed/returned delivery → FailedDelivery; provider cancellation chỉ áp dụng khi aggregate cho phép. Status không được hỗ trợ bị từ chối.

Adapter có timeout, bounded retry và failure gate trong process; command worker có durable retry/dead letter. Lease/transaction semantics thuộc [Data and consistency](../backend/architecture/data-and-consistency.md); inspect/replay theo [Outbox operations](runbooks/background-outbox-operations.md). Contract fixtures và tests tại [Infrastructure shipment tests](../../tests/WorkspaceEcommerce.Infrastructure.Tests/Shipment).

## SMTP và email outbox

Use case enqueue Data Protection-protected payload vào PostgreSQL; email worker lease và gửi SMTP sau commit. Delivery là at-least-once: crash sau khi SMTP nhận nhưng trước completion commit có thể gửi trùng. Account links vì vậy có expiry và chỉ dùng một lần.

Development có thể dùng `Log`, chỉ ghi metadata và không in action links. Ngoài Development phải dùng SMTP/TLS và credentials hợp lệ khi bật authentication. Data Protection key ring cần bền vững để worker đọc payload sau restart. Thiết kế nằm trong [ADR 006](../backend/adr/006-durable-external-side-effects.md).

## Google

Storefront gửi Google ID token; backend kiểm tra token theo danh sách audience do server cấu hình. `VITE_GOOGLE_CLIENT_ID` là cấu hình công khai, không đủ để tự bật backend Google login. Sau xác thực, account vẫn đi qua luồng customer session/2FA của hệ thống theo [ADR 003](../backend/adr/003-customer-account-lifecycle.md).

## Media storage

Development hỗ trợ Local hoặc S3-compatible storage; MinIO là Compose profile `storage` tùy chọn. Ngoài Development dùng object store và stable public base URL. PostgreSQL giữ lifecycle/reference metadata; upload validate kích thước/dimensions, strip metadata và encode WebP trước khi lưu.

Chỉ có malware scanner `NoOp`; đây không phải khả năng quét malware. Ngoài Development, startup và mỗi upload cần risk acceptance có owner/reference/expiry hợp lệ. Quy trình và thiết kế tại [Media scanning policy](runbooks/media-malware-scanning.md) và [ADR 004](../backend/adr/004-durable-media-storage.md).

## Telemetry và realtime

API gửi telemetry qua Application Insights với sensitive-data redaction; yêu cầu production và alerting nằm tại [Observability runbook](runbooks/observability-and-alerting.md).

SignalR là endpoint nội bộ `/hubs/notifications`, không phải provider bên ngoài. Customer được xác thực và join group theo customer ID; frontend nhận order updates. Registration hiện chỉ trong process, chưa có shared backplane. Nhiều API replicas cần backplane và cross-replica smoke test; sticky sessions không tự broadcast giữa replicas.

## Kiểm tra và triển khai

Provider tests dùng synthetic callbacks, fake HTTP hoặc fixtures; PostgreSQL integration tests chứng minh enqueue/locks/deduplication. Lệnh tại [Testing](../backend/development/testing.md). Compose cần VNPay/MiniLogistics sandbox credentials ngay cả khi không thực hiện flow đó; hướng dẫn local tại [Development guide](../backend/development/guide.md). Domain/ingress, CORS/cookies, callback và key-ring checks khi triển khai nằm tại [Production release](../backend/runbooks/production-release.md).
