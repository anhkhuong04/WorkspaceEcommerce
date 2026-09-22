# Báo cáo review mã nguồn WorkspaceEcommerce

**Vai trò review:** Senior .NET Software Architect / Code Reviewer  
**Ngày review:** 2026-09-22  
**Phạm vi:** trạng thái hiện tại của toàn bộ backend ASP.NET Core trong `WorkspaceEcommerce.slnx`, gồm Domain, Application, Infrastructure, API, cấu hình, migrations, CI và ba dự án test.  
**Nguyên tắc đánh giá:** kết luận dựa trên mã nguồn thực tế; phân biệt rõ **Bug**, **Risk** và **Recommendation**. Không thay đổi mã nguồn trong quá trình review.

## 1. Executive Summary

Hệ thống có nền tảng tốt và đã vượt mức một CRUD API thông thường: dependency giữa bốn layer đi đúng chiều, Domain có hành vi và invariant thực, controller mỏng, cấu hình production phần lớn fail-fast, các luồng nhạy cảm như thanh toán, refresh token, email outbox, upload media và shipment outbox đều có nhiều biện pháp phòng vệ. Test suite cũng lớn: 525 test method được khai báo; trong lần review này 317 Application test và 203 Infrastructure test case đều pass.

Tuy nhiên, phiên bản hiện tại **chưa nên được coi là production-ready** trước khi xử lý ba nhóm P0:

1. Endpoint kết quả thanh toán cho phép truy cập ẩn danh chỉ bằng `orderCode`, vì `phone` là tùy chọn. Đây là lỗi kiểm soát truy cập/tiết lộ thông tin giao dịch.
2. Shipment webhook đọc `Order` và `OrderShipment` trước khi mở transaction, không khóa hàng và không có concurrency token. Hai webhook đồng thời có thể làm trạng thái shipment/order lùi lại hoặc khiến duplicate event trả lỗi 500 thay vì idempotent.
3. dependency graph của integration test đang khóa `SSH.NET 2025.1.0`, bị cảnh báo lỗ hổng High `GHSA-q939-rpr3-3284`; CI có bước chặn dependency vulnerability nên nhánh hiện tại có khả năng bị chặn phát hành.

Ngoài ra có các rủi ro đáng ưu tiên: callback VNPay fail-open khi thiếu/sai định dạng amount, rate limiter chạy trước authentication và chỉ lưu trong RAM từng replica, N+1 query ở cart/checkout, cleanup job tải toàn bộ dữ liệu hết hạn vào memory, cấu hình email không nhất quán giữa môi trường, và chính sách giữ nguyên giá snapshot trong cart chưa có thời hạn.

### Kết quả kiểm chứng

| Hạng mục | Kết quả |
|---|---|
| Compile trong `dotnet test -c Release --no-restore` | Tất cả project compile thành công |
| Application tests | **317/317 pass** |
| Infrastructure tests | **203/203 pass** |
| API integration tests | Không chạy được vì Docker engine tại máy review không hoạt động; lỗi xảy ra khi Testcontainers khởi tạo, không phải assertion của ứng dụng |
| NuGet audit trong lần build | Có `NU1903`: `SSH.NET 2025.1.0`, severity High |
| Tổng test method trong source | 525: Application 303, Infrastructure 135, API Integration 87; số test case thực thi lớn hơn do `[Theory]` |

## 2. Critical Issues

Không phát hiện vấn đề mức **Critical** theo nghĩa có thể trực tiếp chiếm quyền hệ thống/RCE hoặc làm mất dữ liệu chắc chắn. Có ba vấn đề **High** cần coi là release blocker và các vấn đề **Medium** nên xử lý ngay sau đó.

### F-01 — Endpoint payment result thiếu kiểm soát truy cập

- **Loại:** Bug bảo mật — Broken Object Level Authorization / information disclosure
- **Severity:** **High**
- **Vị trí:**
  - `src/WorkspaceEcommerce.Api/Controllers/PaymentsController.cs:47-62`
  - `src/WorkspaceEcommerce.Api/Controllers/PaymentsController.cs:95-115`
  - `src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs:62-101`
  - `src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs:244-277`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderFactory.cs:55-66`
- **Code path:** `GET /api/payments/result?orderCode=...&phone=...`; `phone` là nullable. Service chỉ thêm điều kiện `CustomerPhone` khi caller thực sự gửi phone.
- **Vấn đề:** người biết `orderCode` có thể lấy `OrderId`, payment status, shipment/tracking ID, transaction ID, `TxnRef`, gateway transaction number và gateway response. `orderCode` còn được đưa vào URL redirect sau callback và chỉ có 8 ký tự hex ngẫu nhiên sau ngày (xấp xỉ 32-bit entropy), nên không được dùng như một bằng chứng sở hữu.
- **Tác động:** lộ thông tin giao dịch và logistics; URL có thể xuất hiện trong browser history, application log, analytics hoặc được người dùng chia sẻ.
- **Cách sửa đề xuất:**
  1. Với khách đã đăng nhập, truy vấn phải luôn ràng buộc `order.CustomerId == currentCustomer.CustomerId`.
  2. Với guest, phát hành một receipt/payment-result token ngẫu nhiên đủ mạnh hoặc token ký HMAC, ngắn hạn, scope theo order và purpose; không coi `orderCode` là secret.
  3. Giải pháp tạm thời: bắt buộc `phone`, nhưng đây vẫn là shared secret yếu và PII nằm trong query string.
  4. Thu hẹp DTO public, không trả identifiers của payment gateway nếu UI không thực sự cần.
  5. Thêm test phủ: thiếu credential, phone sai, token sai/hết hạn, customer khác và enumeration response đồng nhất.

### F-02 — Webhook vận chuyển có race condition và idempotency chưa atomic

- **Loại:** Bug đồng thời / data consistency
- **Severity:** **High**
- **Vị trí:**
  - `src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentWebhookService.cs:36-69`
  - `src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentWebhookService.cs:89-167`
  - `src/WorkspaceEcommerce.Domain/Modules/Shipments/OrderShipment.cs:73-97`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/Configurations/Shipments/OrderShipmentConfiguration.cs:15-33`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/Configurations/Ordering/OrderConfiguration.cs:130-178`
- **Code path:** service kiểm tra inbox duplicate và đọc `Order`/`OrderShipment` ở ngoài transaction; transaction chỉ bắt đầu sau đó. Cả `Order` và `OrderShipment` đều không có concurrency token.
- **Vấn đề:**
  - Hai request cùng `EventId` có thể cùng vượt qua pre-check; một request đụng unique/PK khi insert inbox và bị chuyển thành lỗi generic thay vì success-idempotent.
  - Hai event khác nhau đến đồng thời có thể cùng đọc `LastEventAtUtc` cũ. Event mới commit trước, event cũ commit sau và ghi đè trạng thái mới. Check `eventAtUtc < LastEventAtUtc` trong Domain chỉ bảo vệ khi xử lý tuần tự, không bảo vệ hai `DbContext` đồng thời.
  - `Order` cũng có thể bị ghi trạng thái `Shipping` sau khi request khác đã ghi `Completed` vì không có row lock/concurrency token.
- **Tác động:** trạng thái fulfillment sai, timeline/inbox không nhất quán, loyalty có thể được kích hoạt trong một lịch sử chuyển trạng thái khó dự đoán.
- **Cách sửa đề xuất:**
  1. Bắt đầu transaction trước mọi read.
  2. Claim event inbox atomically bằng `INSERT ... ON CONFLICT DO NOTHING`; nếu không insert được thì trả duplicate success.
  3. Khóa `Order` và `OrderShipment` bằng `SELECT ... FOR UPDATE` theo thứ tự khóa cố định, hoặc dùng conditional update `WHERE last_event_at_utc <= @eventAt` kết hợp concurrency token/retry.
  4. Không dùng entity đã load trước transaction để mutation.
  5. Thêm integration test thật với hai `DbContext`/hai request `Task.WhenAll`, gồm duplicate event và cặp event cũ/mới có commit order đảo ngược.

### F-03 — Dependency test có lỗ hổng High và có thể làm CI fail

- **Loại:** Supply-chain risk / release blocker
- **Severity:** **High** cho pipeline; **Low hơn đối với runtime production** vì dependency nằm trong integration-test project và exploit yêu cầu dùng SCP recursive download với server độc hại.
- **Vị trí:**
  - `tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj:15`
  - `tests/WorkspaceEcommerce.Api.IntegrationTests/packages.lock.json:853-860`
  - `tests/WorkspaceEcommerce.Api.IntegrationTests/packages.lock.json:940-949`
  - `.github/workflows/ci.yml:72-78`
- **Bằng chứng:** `Testcontainers.PostgreSql 4.12.0` kéo transitive `SSH.NET 2025.1.0`; `dotnet test` phát `NU1903`. GitHub Advisory xác nhận các version `<= 2025.1.0` bị ảnh hưởng và `2026.0.0` đã vá: [GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284).
- **Vấn đề:** CI gọi `dotnet list ... --vulnerable --include-transitive` rồi enforce báo cáo không có vulnerability. Vì vậy dependency hiện tại vừa tạo supply-chain exposure trong môi trường build/test, vừa có thể chặn merge/release.
- **Cách sửa đề xuất:** pin trực tiếp `SSH.NET >= 2026.0.0` trong integration-test project hoặc nâng Testcontainers lên version có dependency graph đã vá; cập nhật lock file, chạy toàn bộ integration test và bước audit. Không suppress `NU1903` nếu chưa có risk acceptance có thời hạn.

### F-04 — Callback VNPay chấp nhận amount thiếu hoặc không parse được

- **Loại:** Risk về payment integrity
- **Severity:** **Medium**, tác động tiềm năng High nhưng khả năng khai thác trực tiếp thấp do callback vẫn phải có chữ ký hợp lệ
- **Vị trí:**
  - `src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs:74-103`
  - `src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs:158-165`
  - `src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs:125-147`
  - `src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs:106-117`
- **Code path:** parse lỗi hoặc thiếu `vnp_Amount` trả `null`; Application chỉ reject khi amount khác `null` **và** không bằng expected amount. `GetPaymentOutcome` cũng coi `transactionStatus` rỗng là success nếu `responseCode == "00"`.
- **Vấn đề:** validation đang fail-open đối với field quan trọng của giao dịch. Attacker ngoài không thể tự ký payload, nhưng payload malformed do provider/proxy/configuration error vẫn có thể đánh dấu đơn đã thanh toán.
- **Cách sửa đề xuất:** chữ ký hợp lệ là điều kiện cần, không phải đủ. Bắt buộc `TxnRef`, `Amount`, `ResponseCode`, `TransactionStatus` và các field theo contract; amount phải parse được, dương và bằng chính xác expected amount/currency. Payload thiếu field phải trả mã VNPay phù hợp và không mutation. Bổ sung test missing/malformed/negative/overflow amount và missing transaction status.

### F-05 — Rate limiter không đạt ý định bảo vệ trong cluster và middleware order sai với customer claim

- **Loại:** Risk bảo mật/vận hành
- **Severity:** **Medium**
- **Vị trí:**
  - `src/WorkspaceEcommerce.Api/Program.cs:128-131`
  - `src/WorkspaceEcommerce.Api/Extensions/RateLimiterExtensions.cs:11-26`
  - `src/WorkspaceEcommerce.Api/Extensions/RateLimiterExtensions.cs:81-91`
  - `src/WorkspaceEcommerce.Api/Extensions/RateLimiterExtensions.cs:106-116`
  - `src/WorkspaceEcommerce.Api/Extensions/RateLimiterExtensions.cs:119-130`
  - `src/WorkspaceEcommerce.Api/Extensions/RateLimiterExtensions.cs:162-166`
- **Vấn đề:**
  - `UseRateLimiter()` chạy trước `UseAuthentication()`, nên nhánh warranty activation đọc `customer_id` khi `HttpContext.User` chưa được authenticate; key customer thực tế sẽ là `anonymous`.
  - limiter là in-memory, mỗi replica có quota riêng; scale-out làm quota nhân lên và restart xóa state.
  - auth chủ yếu partition theo IP: botnet vượt được giới hạn; NAT có thể khóa nhầm nhiều user hợp lệ.
  - checkout, payment và webhook dùng chung bucket `transaction` theo IP; burst webhook hợp lệ có thể bị 429.
- **Cách sửa đề xuất:** đặt authentication trước limiter đối với partition cần claim; dùng gateway/WAF hoặc distributed store cho quota security-critical; kết hợp normalized account/challenge/customer ID với IP; tách quota webhook theo provider credential/signature và có retry semantics; bổ sung test middleware thật cho claim partition, `Retry-After` và 429.

### F-06 — Cart/checkout có N+1 query và VNPay dựng snapshot hai lần

- **Loại:** Performance bug/risk
- **Severity:** **Medium**
- **Vị trí:**
  - `src/WorkspaceEcommerce.Application/Modules/Cart/StorefrontCartService.cs:240-277`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutCartBuilder.cs:20-61`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutService.cs:121-146`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderPlacer.cs:35-47`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/AppDbContext.cs:481-531`
- **Vấn đề:** cart DTO thực hiện tới ba query cho mỗi item (variant, product, primary image). Checkout thực hiện variant/product/category cho từng item. Với VNPay, bộ snapshot này được dựng một lần để quote shipping trước transaction và thêm một lần trong transaction, tạo khoảng `6N` query cùng dữ liệu liên quan.
- **Tác động:** latency tăng tuyến tính theo số item, tăng connection pressure và kéo dài transaction trong checkout.
- **Cách sửa đề xuất:** thêm query batch/projection cho toàn bộ variant IDs, product/category/image trong 1-3 round trip; khóa variants theo danh sách đã sort trong một query (`FindProductVariantsForUpdateAsync` đã tồn tại nhưng chưa được dùng ở flow này); đo command count trong integration test. Quote ngoài transaction có thể dùng snapshot chỉ để gọi provider, nhưng trong transaction vẫn phải revalidate stock/price.

### F-07 — Email configuration có thể silently discard mail ngoài Development và cho phép SMTP không TLS

- **Loại:** Bug cấu hình / security risk
- **Severity:** **Medium**
- **Vị trí:**
  - `src/WorkspaceEcommerce.Infrastructure/Configuration/EmailDeliveryConfigurationValidator.cs:14-26`
  - `src/WorkspaceEcommerce.Infrastructure/Configuration/EmailDeliveryConfigurationValidator.cs:48-78`
  - `src/WorkspaceEcommerce.Infrastructure/Notifications/CustomerEmailDeliveryServices.cs:9-18`
  - `src/WorkspaceEcommerce.Infrastructure/Notifications/CustomerEmailDeliveryServices.cs:21-39`
- **Vấn đề:** message nói provider `Log` chỉ hợp lệ trong Development, nhưng code chỉ cấm khi environment name đúng bằng `Production`; Staging/QA vẫn chấp nhận Log và worker coi email đã gửi dù chỉ log subject. Với SMTP, validator không yêu cầu `EnableSsl` ngoài Development.
- **Tác động:** verification/reset email biến mất ở staging hoặc môi trường tên khác; link/token tài khoản có thể truyền qua SMTP không mã hóa nếu cấu hình sai.
- **Cách sửa đề xuất:** dùng semantic `environment.IsDevelopment()` thay vì so chuỗi Production; ngoài Development bắt buộc provider gửi thật và TLS, trừ một risk-acceptance flag rõ ràng; validate cặp username/password; thêm configuration tests cho Development, Staging, QA và Production.

### F-08 — Cleanup worker materialize toàn bộ dữ liệu hết hạn

- **Loại:** Reliability/performance risk
- **Severity:** **Medium**
- **Vị trí:** `src/WorkspaceEcommerce.Infrastructure/Notifications/CustomerAccountCleanupWorker.cs:37-90`
- **Vấn đề:** worker tải toàn bộ token, refresh family, 2FA challenge/recovery code, login history và delivered email quá hạn vào memory, tracking tất cả entity rồi delete trong một `SaveChanges`. Advisory lock ngăn nhiều replica chạy đồng thời nhưng không giới hạn kích thước batch.
- **Tác động:** memory spike, transaction/WAL lớn, lock lâu và timeout khi dữ liệu tăng.
- **Cách sửa đề xuất:** dùng `ExecuteDeleteAsync` theo batch có giới hạn hoặc keyset pagination, commit từng batch, giới hạn thời gian mỗi vòng, phát metric số row/duration/error. Thứ tự xóa phải tôn trọng FK, đặc biệt refresh token trước family.

### F-09 — Admin warranty activation không có transaction/lock tương đương customer flow

- **Loại:** Risk đồng thời; hiện bị giảm mức độ vì warranty mặc định tắt
- **Severity:** **Medium** trước khi bật feature
- **Vị trí:**
  - `src/WorkspaceEcommerce.Application/Modules/Warranties/AdminWarrantyService.cs:784-839`
  - `src/WorkspaceEcommerce.Application/Modules/Warranties/CustomerWarrantyService.cs:61-153`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/Configurations/Warranties/WarrantyEntitlementConfiguration.cs:13-45`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/Configurations/Warranties/WarrantyCoverageSnapshotConfiguration.cs:11-22`
- **Vấn đề:** customer flow mở transaction và khóa serialized unit/order; admin flow load entitlement/unit/order/plan bình thường rồi mutation/save, không row lock và không concurrency token. Hai admin request có thể cùng thấy Pending và cùng tạo coverage snapshot/audit/email. Coverage snapshot không có unique constraint theo entitlement/component để chặn bản sao.
- **Cách sửa đề xuất:** dùng chung một transactional activation coordinator cho admin/customer; khóa unit, entitlement/order theo thứ tự thống nhất; thêm unique constraint phù hợp cho snapshot; bắt và map concurrency conflict. Phải có concurrent integration test trước khi bật `Warranty:AdminEnabled`.

### F-10 — Giá snapshot trong cart được giữ vô thời hạn

- **Loại:** Business risk, không kết luận là bug vì test hiện tại chủ động kỳ vọng hành vi này
- **Severity:** **Medium** nếu nghiệp vụ không cam kết price lock
- **Vị trí:**
  - `src/WorkspaceEcommerce.Domain/Modules/Cart/Cart.cs:38-53`
  - `src/WorkspaceEcommerce.Domain/Modules/Cart/CartItem.cs:21-39`
  - `src/WorkspaceEcommerce.Application/Modules/Cart/StorefrontCartService.cs:45-84`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutCartBuilder.cs:40-57`
  - `tests/WorkspaceEcommerce.Application.Tests/Modules/Ordering/CheckoutServiceTests.cs:22-53`
- **Vấn đề:** khi item đã tồn tại, tăng quantity không cập nhật `UnitPriceSnapshot`; checkout dùng snapshot thay vì `variant.Price`. Cart không có expiration/price-lock deadline. Test đang chứng minh variant giá 150 nhưng order dùng snapshot 120, nên đây là policy hiện hữu chứ không phải tình cờ.
- **Tác động:** người dùng có thể giữ giá cũ lâu dài sau khi catalog tăng giá; giảm doanh thu hoặc tạo tranh chấp về giá.
- **Cách sửa đề xuất:** product owner phải xác nhận policy. Thông thường checkout nên reprice bằng giá variant đã khóa và trả danh sách thay đổi để người dùng xác nhận; nếu muốn giữ giá, cần `PriceLockedUntil`, audit nguồn giá và thời hạn cart/reservation rõ ràng.

## 3. Architecture Review

### 3.1 Dependency direction và Clean Architecture

Dependency project-level đang đúng:

```text
Api ───────────────> Application ─────────> Domain
 │                         ▲
 └──────> Infrastructure ──┘
                └────────────────────────> Domain
```

- Domain không reference project/package khác ngoài BCL: tốt.
- Application chỉ reference Domain ở cấp project.
- Infrastructure implement abstraction của Application và chứa EF Core, PostgreSQL, S3, VNPay, SMTP, JWT, Google, QuestPDF, ImageMagick.
- API là composition root, reference Application + Infrastructure; không thấy Infrastructure reference ngược API.

Điểm chưa “clean” hoàn toàn là Application reference trực tiếp `Microsoft.EntityFrameworkCore` tại `WorkspaceEcommerce.Application.csproj:13-17`. `IAppDbContext` expose rất nhiều `IQueryable<T>` (`IAppDbContext.cs:15-143`) và `QueryableAsyncExtensions.cs:1-85` gọi `AsNoTracking`, `ToListAsync`, `IAsyncQueryProvider`. Đây không phải circular dependency, nhưng làm Application phụ thuộc semantics của EF/LINQ provider và khiến việc thay persistence engine khó hơn tên abstraction gợi ý.

**Đánh giá:** kiến trúc project tốt; boundary persistence ở mức trung bình. Không nên rewrite toàn bộ sang repository-per-entity. Nên tách dần theo use case/hot path thành query interfaces trả projection và command interfaces thể hiện lock/idempotency rõ ràng.

### 3.2 Domain layer

Điểm tốt:

- Entity dùng private setter, collection backing field và method hành vi; không phải anemic domain thuần túy.
- `Order`, `Coupon`, `ProductVariant`, loyalty, warranty và shipment đều có invariant/transition trong Domain.
- `DomainException`, `Guard` và currency validation tập trung hóa validation cơ bản.
- Snapshot trong order/coupon/warranty giúp bảo toàn lịch sử nghiệp vụ.

Điểm cần cải thiện:

- Có 61 chỗ gọi trực tiếp `DateTimeOffset.UtcNow`/`DateTime.UtcNow` trong Domain/Application/Infrastructure. Một số service đã dùng `TimeProvider`, nhưng việc dùng lẫn lộn làm test thời gian kém deterministic và một use case có thể sinh nhiều timestamp lệch nhau.
- Invariant liên aggregate vẫn nằm ở Application, điều này hợp lý, nhưng các flow quan trọng chưa luôn có concurrency policy thống nhất; ví dụ customer warranty có lock còn admin warranty không có.
- Domain guard bảo vệ khi đi qua code C#, nhưng database chưa có check constraint cho các invariant quan trọng như amount/quantity/date range.

### 3.3 Application layer

Điểm tốt:

- Use case được tổ chức theo module; controller không chứa nghiệp vụ.
- FluentValidation được dùng đều; `CancellationToken` được truyền xuyên qua phần lớn async path.
- Result pattern phân biệt Validation/Unauthorized/NotFound/Conflict/Failure và giảm exception-driven control flow.
- Các flow payment và customer warranty dùng transaction + row lock đúng hướng; checkout khóa product variant trước khi giảm stock.
- Outbox cho shipment/email đóng các crash window quan trọng.

Điểm cần cải thiện:

- `IAppDbContext` là God interface cho nhiều bounded context; test fake và service đều biết quá nhiều persistence detail.
- Các class quá lớn: `AdminWarrantyService` 959 dòng, `AdminOrderService` 851 dòng, `OrderShipmentService` 693 dòng. Chúng gom query, command, mapping, audit, notification và orchestration; chi phí review/thay đổi cao.
- `CheckoutService`/`CheckoutOrderPlacer` tự `new` helper nội bộ thay vì dependency rõ ràng. Các helper thuần logic có thể giữ internal/static, nhưng orchestration/policy nên inject qua interface hoặc gom thành domain/application service có test độc lập.
- LINQ-to-Objects fallback trong `QueryableAsyncExtensions` giúp fake test tiện, nhưng có thể làm unit test pass trong khi query thật không translate hoặc có semantics khác PostgreSQL.

### 3.4 Web API layer

Điểm tốt:

- Controller nhìn chung mỏng, trả qua `ToApiResponse`, nhận cancellation token.
- Invalid model state và exception đều trả envelope không lộ stack trace; `GlobalExceptionHandlingMiddleware.cs:17-38` log trace ID và trả message generic.
- OpenAPI chỉ map trong Development; production bật HSTS/HTTPS redirection.
- Health check tách live/ready; forwarded header, CORS, security header, rate limiter, authentication và authorization đều được cấu hình tập trung.
- Endpoint admin/customer dùng policy/identity separation; integration tests có authorization matrix.

Điểm cần cải thiện:

- F-01 và F-05 là hai lỗi boundary quan trọng.
- `CheckoutRequest.ClientIpAddress` là input từ client (`CheckoutRequest.cs:29`) nhưng được gửi thẳng vào VNPay (`CheckoutService.cs:155-164`) và validator không kiểm tra. IP dùng cho audit/risk signal phải lấy từ `HttpContext.Connection.RemoteIpAddress` sau trusted forwarded headers, như auth controller đang làm, không lấy từ request body. Đây là issue **Medium** về integrity của audit/anti-fraud data.
- API dùng custom envelope nhất quán nhưng không theo RFC 7807 `ProblemDetails`; không phải bug, nhưng cần document contract rõ và versioning strategy trước khi có external client.

### 3.5 Infrastructure layer

Điểm tốt:

- Mapping EF tách theo entity; schema, max length, precision, FK và index được khai báo khá đầy đủ.
- Raw SQL row lock dùng `FromSqlInterpolated`, không nối chuỗi user input.
- PostgreSQL advisory lock, `FOR UPDATE SKIP LOCKED`, lease token và retry/dead-letter trong outbox cho thấy thiết kế multi-replica có chủ ý.
- Upload media giới hạn size/dimension/type, canonicalize ảnh, giới hạn object path; receipt image resolver chặn arbitrary origin/path traversal và giới hạn byte.
- Configuration validator fail-fast cho nhiều provider và production setting.

Điểm cần cải thiện:

- Concurrency strategy không đồng nhất giữa module như F-02/F-09.
- Cleanup batch F-08 và receipt generation đồng bộ có thể tạo pressure.
- `AppDbContext` 639 dòng implement nhiều store/interface; nên tách query object/adapter theo module, giữ DbContext làm unit-of-work nội bộ.

## 4. Code Quality

### Điểm mạnh

- Naming nhìn chung rõ, namespace/module nhất quán.
- Nullable reference types và implicit usings bật ở mọi project.
- Validation, mapping và configuration phần lớn được tách thành class riêng.
- Không thấy `async void`; cancellation token được dùng có hệ thống.
- Comment ở các flow payment/outbox giải thích đúng “why”, không chỉ lặp lại code.
- Global error handling tránh lộ thông tin nội bộ.

### Code smells và technical debt

1. **God interface/God service:** `IAppDbContext` và ba service 693-959 dòng làm tăng blast radius. Tách theo use case, không nhất thiết áp dụng MediatR/CQRS toàn hệ thống.
2. **Clock không thống nhất:** 61 static UTC calls; chuẩn hóa `TimeProvider` từ use case xuống domain method bằng tham số `now`.
3. **Persistence leakage:** Application chứa EF package và query extension. Tách trước ở các hot path/concurrency-sensitive path.
4. **Repeated mapping/query:** cart, order, warranty có mapping methods tự query thêm entity; dễ sinh N+1.
5. **Exception mapping chưa đầy đủ:** database unique/concurrency error không phải nơi nào cũng được map thành Conflict/idempotent result; webhook duplicate là ví dụ.
6. **API contract coupling với Domain enum:** chấp nhận được ở nội bộ, nhưng khi API public/versioned nên dùng contract enum/translation để tránh domain rename thành breaking change.
7. **Order code generation:** pre-check rồi insert vẫn có TOCTOU nhỏ; unique index là bảo vệ cuối nhưng collision hiện sẽ thành 500. Xác suất thấp, nên retry khi bắt unique violation thay vì chỉ pre-check.

## 5. Database & Performance

### Database design

Điểm tốt:

- Có unique index cho order code, tracking code, SKU, slug, coupon code, transaction reference, media object key, warranty identifier fingerprint và nhiều idempotency key.
- Các query list phổ biến có composite index theo customer/status/time.
- Precision tiền tệ được cấu hình; FK delete behavior chủ yếu explicit.
- Migration được kiểm tra pending model change, clean migration và upgrade migration trong CI.

Rủi ro:

- Không tìm thấy `HasCheckConstraint` hoặc `CHECK` trong persistence/migrations. Dữ liệu đi qua Domain được bảo vệ, nhưng raw SQL/import/bug mapping vẫn có thể tạo amount âm, quantity không hợp lệ hoặc date range sai. Nên thêm check constraint có chọn lọc cho invariant tài chính/tồn kho quan trọng.
- `Order`/`OrderShipment` không có concurrency token trong khi bị cập nhật từ admin, callback, worker và webhook.
- Các pre-check unique (`Any`/`Exists`) không thay thế việc bắt PostgreSQL unique violation.

### Hotspots hiệu năng

1. F-06: cart/checkout N+1 và snapshot hai lần.
2. F-08: cleanup unbounded.
3. `OrderReceiptImageResolver.cs:27-73` tải tối đa 20 ảnh **tuần tự**, mỗi request có timeout 5 giây. Worst-case lý thuyết gần 100 giây trước khi render; `QuestPdfOrderReceiptRenderer.cs:25-43` render CPU đồng bộ ngay trên request thread.
4. Receipt nên đọc object store theo key thay vì HTTP self-call, dùng bounded parallelism + total deadline, cache PDF hoặc chuyển generation sang background nếu traffic đáng kể.
5. Cần bổ sung telemetry: EF command count/duration cho cart/checkout, cleanup rows/duration, PDF generation duration/size, outbox lag/dead-letter count.

## 6. Security

### Điểm đang làm tốt

- JWT validate issuer, audience, signing key và lifetime; token query cho SignalR được giới hạn theo hub path.
- Production configuration fail-fast cho host/origin/HTTPS/key ring; Data Protection có hỗ trợ persistent key ring.
- CORS allow-list cụ thể, không wildcard với credentials; forwarded headers chỉ tin proxy cấu hình.
- Password/refresh token dùng hash, random token, rotation, family revocation và concurrency protection; 2FA secret/email outbox payload được bảo vệ.
- Webhook MiniLogistics dùng HMAC, constant-time comparison/timestamp tolerance và request size limit.
- SQL row lock dùng parameterized interpolation; không thấy raw SQL ghép input.
- Upload ảnh xác minh signature/content/dimension, re-encode và hạn chế path; receipt resolver có same-origin/path allow-list.
- CI có secret scan/dependency audit/SBOM/Trivy; telemetry có redaction processor.

### Vấn đề cần xử lý

| ID | Vấn đề | Severity |
|---|---|---|
| F-01 | Payment result ẩn danh chỉ cần orderCode | High |
| F-03 | SSH.NET vulnerable trong test supply chain | High pipeline / test scope |
| F-04 | VNPay amount/transaction status validation fail-open | Medium |
| F-05 | Rate limiter sai middleware order, per-process/IP-only | Medium |
| F-07 | Log email ngoài Development và SMTP không bắt buộc TLS | Medium |
| API-IP | Client tự khai IP gửi VNPay | Medium |
| Guest lookup | `orderCode + phone` trả đầy đủ tên/email/address/note; phone là shared secret và lookup/tracking chỉ nhận default IP quota 120/phút | Medium risk |

Đối với guest order lookup/receipt/tracking (`OrdersController.cs:16-52`, `StorefrontOrderLookupService.cs:25-73`), nên dùng opaque signed lookup token; nếu vẫn giữ phone, response phải tối thiểu hóa/mask PII và có quota riêng thấp hơn cho toàn bộ lookup, không chỉ receipt.

## 7. Testing

### Hiện trạng tốt

- 525 test method trải trên Domain/Application, Infrastructure và API Integration.
- Application tests dùng fake nhanh; Infrastructure tests phủ configuration, persistence mapping, provider, media, auth; API Integration dùng Testcontainers PostgreSQL nên có khả năng bắt behavior thật của provider.
- Có test concurrent checkout last-stock, concurrent refresh rotation, email lease, auth boundary, migration và idempotency cơ bản.
- CI build/test với locked restore; kiểm migration; audit dependency; SBOM; image scan; frontend/browser smoke.

### Kết quả lần review

```text
WorkspaceEcommerce.Application.Tests:    317 passed, 0 failed
WorkspaceEcommerce.Infrastructure.Tests: 203 passed, 0 failed
WorkspaceEcommerce.Api.IntegrationTests: blocked before execution because Docker endpoint
                                        npipe://./pipe/docker_engine was unavailable
```

Việc integration test không chạy được tại máy review là giới hạn môi trường. Tuy nhiên, báo cáo không coi 87 integration test method là “pass”.

### Test gaps ưu tiên

1. Hai shipment webhook đồng thời: duplicate `EventId`; event cũ/mới commit đảo thứ tự.
2. Payment result authorization: không phone/token, phone sai, customer khác, token hết hạn.
3. VNPay callback thiếu/sai/âm/overflow amount và thiếu transaction status.
4. Rate limiter qua full middleware pipeline để chứng minh claim partition và behavior multi-key.
5. Cart/checkout SQL command-count test để chặn N+1 regression.
6. Admin warranty activation đồng thời.
7. Cleanup với dataset lớn và batch boundary/FK order.
8. Email configuration matrix cho Development/Staging/QA/Production và TLS.
9. Test policy khi catalog price đổi sau lúc add-to-cart.

`coverlet.collector` đã có trong test project nhưng CI không thấy bước collect/publish coverage hoặc quality threshold. Không nên chạy theo coverage 100%; nên đặt baseline thực tế và gate riêng cho payment/auth/shipment/domain transition.

Fake `IQueryable` chạy LINQ-to-Objects không thể thay thế provider test cho query phức tạp, translation, isolation và concurrency. Những use case đó phải ưu tiên integration test PostgreSQL.

## 8. Refactoring Recommendations

### P0 — Phải hoàn thành trước release

1. **Đóng F-01:** thay orderCode-only access bằng authenticated ownership hoặc short-lived opaque/signed result token; giảm DTO public; thêm negative authorization tests.
2. **Đóng F-02:** atomic inbox claim + transaction-before-read + row lock/conditional update cho order/shipment; thêm concurrent PostgreSQL tests.
3. **Đóng F-03:** nâng/pin SSH.NET bản vá, cập nhật lock file và chạy lại audit/CI.

### P1 — Sprint kế tiếp

1. Siết callback VNPay bắt buộc amount/status/required fields, fail-closed.
2. Chuyển `ClientIpAddress` khỏi request contract; lấy IP từ trusted server context.
3. Sửa middleware order và thiết kế distributed/account-aware rate limiting; tách webhook quota.
4. Batch query cart/checkout, dùng batch row lock và command-count integration test.
5. Sửa email environment semantics, yêu cầu TLS ngoài Development.
6. Batch cleanup bằng set-based delete/keyset loop.
7. Dùng chung transactional warranty activation coordinator trước khi bật feature.
8. Chốt và mã hóa business policy về cart price snapshot/expiration.
9. Thêm DB check constraint cho amount, quantity, stock và các date range có giá trị tài chính.

### P2 — Cải thiện maintainability có kiểm soát

1. Tách `AdminWarrantyService`, `AdminOrderService`, `OrderShipmentService` theo command/query/use case; giữ transaction boundary rõ.
2. Thu hẹp `IAppDbContext` thành interface theo module/use case; trả projection thay vì expose toàn bộ `IQueryable` ở hot path.
3. Chuẩn hóa `TimeProvider`; Domain method nhận `now` từ caller, không tự đọc clock tĩnh.
4. Tối ưu receipt: object-store read, bounded parallelism, total timeout và cache/background rendering.
5. Chuẩn hóa API error contract/versioning và document compatibility policy.
6. Thêm observability SLO/metric cho payment callback, shipment lag, webhook duplicates/conflicts, cleanup, query count và PDF rendering.
7. Thêm coverage report/gate tập trung vào critical business paths, không dùng một con số coverage chung thay cho test chất lượng.

## 9. Final Assessment

### Điểm số

| Nhóm | Điểm / 10 | Nhận xét ngắn |
|---|---:|---|
| Architecture | **7.5** | Project dependency đúng; persistence abstraction còn rò EF/IQueryable |
| Domain | **8.0** | Rich domain và invariant tốt; clock/concurrency policy chưa đồng nhất |
| Application | **6.5** | Use case rõ, transaction tốt ở nhiều flow; God interface/service và N+1 |
| Web API | **7.0** | Controller mỏng, error/config tốt; payment access và middleware order đáng kể |
| Infrastructure/Persistence | **7.0** | PostgreSQL/outbox/locking mạnh; một số flow thiếu lock, cleanup unbounded |
| Code Quality | **7.0** | Naming/validation/cancellation tốt; class lớn và repeated mapping/query |
| Security | **6.5** | Nhiều hardening tốt nhưng có BOLA payment, vulnerable dependency và limiter/email gaps |
| Performance | **6.0** | Index khá tốt; cart/checkout N+1, cleanup/PDF cần tối ưu |
| Testing | **7.5** | Test suite rộng và 520 case đã pass; concurrency/security gaps, chưa có coverage gate |
| Maintainability/Extensibility | **6.5** | Provider abstraction tốt; IAppDbContext và service size làm tăng chi phí thay đổi |
| **Tổng thể** | **7.0** | Nền tảng tốt, cần đóng P0 trước production release |

### Top 5 vấn đề cần xử lý theo thứ tự

1. Payment result authorization/tokenization (F-01).
2. Shipment webhook concurrency + atomic idempotency (F-02).
3. Vá dependency SSH.NET và khôi phục CI audit xanh (F-03).
4. VNPay callback required-field/amount validation fail-closed (F-04).
5. Rate limiter middleware/distributed strategy, sau đó batch hóa cart/checkout (F-05/F-06).

### Những phần tốt không nên thay đổi chỉ vì “refactor”

- Giữ dependency direction hiện tại và Domain không phụ thuộc framework.
- Giữ controller mỏng + Result mapping + global generic error response.
- Giữ payment/checkout/customer-warranty row-lock pattern, nhưng áp dụng nhất quán hơn.
- Giữ email/shipment outbox với lease, retry, dead-letter và advisory lock.
- Giữ security boundary của media processing và receipt same-origin resolver.
- Giữ production fail-fast configuration, persistent Data Protection key ring, trusted proxies/CORS allow-list.
- Giữ PostgreSQL integration tests và migration verification trong CI.

### Kết luận

Codebase có kiến trúc và mức engineering maturity khá tốt, đặc biệt ở Domain modeling, transaction/outbox, cấu hình production và test breadth. Không cần “đại tu” hoặc đưa thêm framework chỉ để đạt hình thức Clean Architecture. Hướng hiệu quả nhất là xử lý ba P0, củng cố concurrency/idempotency tại database boundary, rồi giảm query round trip và thu hẹp persistence interface theo từng hot path. Sau khi các P0 có integration test chứng minh và dependency audit xanh, hệ thống sẽ tiến gần đáng kể đến mức sẵn sàng production.
