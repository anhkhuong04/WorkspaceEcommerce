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
3. dependency graph của integration test đang khóa `SSH.NET 2025.1.0`, bị hai cảnh báo High `GHSA-q939-rpr3-3284` và `GHSA-mggc-4xg6-vcxf`; CI có bước chặn dependency vulnerability nên nhánh hiện tại có khả năng bị chặn phát hành.

Ngoài ra có các rủi ro đáng ưu tiên: callback VNPay fail-open khi thiếu/sai định dạng amount, rate limiter chạy trước authentication và chỉ lưu trong RAM từng replica, N+1 query ở cart/checkout, cleanup job tải toàn bộ dữ liệu hết hạn vào memory, cấu hình email không nhất quán giữa môi trường, và chính sách giữ nguyên giá snapshot trong cart chưa có thời hạn.

### Kết quả kiểm chứng

| Hạng mục | Kết quả |
|---|---|
| Compile trong `dotnet test -c Release --no-restore` | Tất cả project compile thành công |
| Application tests | **317/317 pass** |
| Infrastructure tests | **203/203 pass** |
| API integration tests | Không chạy được vì Docker engine tại máy review không hoạt động; lỗi xảy ra khi Testcontainers khởi tạo, không phải assertion của ứng dụng |
| NuGet audit trong lần build | Có `NU1903`: `SSH.NET 2025.1.0`, hai advisory severity High |
| Tổng test method trong source | 525: Application 303, Infrastructure 135, API Integration 87; số test case thực thi lớn hơn do `[Theory]` |

## 2. Remediation Tasks for Critical Issues

Không phát hiện vấn đề mức **Critical** theo nghĩa có thể trực tiếp chiếm quyền hệ thống/RCE hoặc làm mất dữ liệu chắc chắn. Ba vấn đề **High** dưới đây là release blocker; các vấn đề **Medium** là backlog bắt buộc sau P0.

Các nhận định đã được đối chiếu lại với source và test tại commit `9e8dc6b` ngày 2026-09-23. Mỗi task chỉ được đánh dấu **Done** khi toàn bộ acceptance criteria đạt và có evidence từ các lệnh verification trên cùng commit sạch. Việc code đã được merge nhưng thiếu test/evidence không được xem là hoàn thành.

| Task | Finding | Priority | Status | Completion proof |
|---|---|---|---|---|
| TASK-01 | F-01 Payment result authorization | P0 | Done 2026-09-23 | 10 Application tests, 2 token tests, 7 PostgreSQL/API integration tests, 14 frontend tests; build/lint/typecheck pass |
| TASK-02 | F-02 Shipment webhook concurrency | P0 | Done 2026-09-23 | 14 unit tests, 7 PostgreSQL webhook tests, concurrent duplicate test passed 10/10 runs |
| TASK-03 | F-03 Vulnerable SSH.NET dependency | P0 | Done 2026-09-23 | SSH.NET 2026.0.0; locked restore stable; audit gate clean; 617/617 backend tests pass in Release |
| TASK-04 | F-04 VNPay callback validation | P1 | Done 2026-09-23 | Signed malformed matrix and 635/635 backend tests prove fail-closed behavior |
| TASK-05 | F-05 Rate limiting | P1 | Repository done 2026-09-23; Platform evidence open | 3 middleware/partition tests and 638/638 backend tests pass; two-replica edge evidence still required |
| TASK-06 | F-06 Cart/checkout query count | P1 | Open | PostgreSQL command-count regression tests within fixed budgets |
| TASK-07 | F-07 Email configuration | P1 | Open | Environment matrix tests reject unsafe non-Development settings |
| TASK-08 | F-08 Cleanup batching | P1 | Open | Large-data integration test proves bounded batches and correct FK order |
| TASK-09 | F-09 Warranty activation concurrency | P1 | Open; required before enabling warranty admin | Concurrent activation test produces one activation/audit/email set |
| TASK-10 | F-10 Cart price policy | P1 decision gate | Blocked on Product decision | Approved policy + executable tests for the selected behavior |

### TASK-01 — Bảo vệ payment result theo ownership/possession proof (F-01)

- **Loại:** Bug bảo mật — Broken Object Level Authorization / information disclosure
- **Severity:** **High**
- **Goal:** Chỉ chủ sở hữu đã xác thực hoặc guest có possession proof hợp lệ mới xem được kết quả thanh toán; response public không lộ gateway identifiers không cần thiết.
- **Scope:** API/Application payment-result contract, callback redirect, storefront/API client tương ứng và regression tests. Không thay đổi contract order lookup/receipt ngoài phần dùng chung đã được chứng minh là cần thiết.
- **Vị trí:**
  - `src/WorkspaceEcommerce.Api/Controllers/PaymentsController.cs:47-62`
  - `src/WorkspaceEcommerce.Api/Controllers/PaymentsController.cs:95-115`
  - `src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs:62-101`
  - `src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs:244-277`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderFactory.cs:55-66`
- **Code path:** `GET /api/payments/result?orderCode=...&phone=...`; `phone` là nullable. Service chỉ thêm điều kiện `CustomerPhone` khi caller thực sự gửi phone.
- **Vấn đề:** người biết `orderCode` có thể lấy `OrderId`, payment status, shipment/tracking ID, transaction ID, `TxnRef`, gateway transaction number và gateway response. `orderCode` còn được đưa vào URL redirect sau callback và chỉ có 8 ký tự hex ngẫu nhiên sau ngày (xấp xỉ 32-bit entropy), nên không được dùng như một bằng chứng sở hữu.
- **Tác động:** lộ thông tin giao dịch và logistics; URL có thể xuất hiện trong browser history, application log, analytics hoặc được người dùng chia sẻ.
- **Implementation checklist:**
  1. Với khách đã đăng nhập, truy vấn phải luôn ràng buộc `order.CustomerId == currentCustomer.CustomerId`.
  2. Với guest, phát hành một receipt/payment-result token ngẫu nhiên đủ mạnh hoặc token ký HMAC, ngắn hạn, scope theo order và purpose; không coi `orderCode` là secret.
  3. Giải pháp tạm thời: bắt buộc `phone`, nhưng đây vẫn là shared secret yếu và PII nằm trong query string.
  4. Thu hẹp DTO public, không trả identifiers của payment gateway nếu UI không thực sự cần.
  5. Thêm test phủ: thiếu credential, phone sai, token sai/hết hạn, customer khác và enumeration response đồng nhất.

- **Acceptance criteria:**
  - Request chỉ có `orderCode` không thể đọc dữ liệu payment; credential sai/hết hạn và order không tồn tại có response chống enumeration đồng nhất.
  - Customer A không đọc được order của customer B; customer đúng owner vẫn đọc được.
  - Guest hoàn tất VNPay có thể xem kết quả bằng token có entropy/TTL/purpose/order scope rõ ràng; token không chứa PII thô và không dùng lại cho order khác.
  - DTO anonymous không chứa `PaymentTransaction.Id`, `TxnRef`, gateway transaction number/response nếu UI không cần.
  - Callback redirect và storefront hoạt động với contract mới; không log token.
- **Verification:**
  - Bổ sung/đổi test trong `PaymentServiceTests`, `PaymentIntegrationTests` và storefront `PaymentResultPage` tests cho cả positive/negative cases.
  - Chạy `dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~PaymentServiceTests"`.
  - Chạy `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PaymentIntegrationTests"` với Docker.
  - Trong `frontend/`, chạy `corepack pnpm test && corepack pnpm typecheck && corepack pnpm build`.

### TASK-02 — Atomic shipment webhook và monotonic state (F-02)

- **Completion evidence (2026-09-23):** transaction now starts before authoritative reads; order then shipment rows are locked in a fixed order; inbox ownership uses `INSERT ... ON CONFLICT DO NOTHING`; PostgreSQL tests prove concurrent duplicate delivery creates one inbox/timeline/loyalty earn and an older event committed after a newer event cannot regress state. The concurrent duplicate test passed 10 consecutive runs.

- **Loại:** Bug đồng thời / data consistency
- **Severity:** **High**
- **Goal:** Mỗi provider event được claim atomically và mọi concurrent event chỉ có thể giữ nguyên hoặc tiến trạng thái shipment/order, không tạo duplicate side effect.
- **Scope:** Shipment inbox claim, transaction/locking strategy, order/shipment mutation, loyalty trigger, PostgreSQL integration tests và metrics conflict/duplicate. Không thay đổi provider signature contract.
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
- **Implementation checklist:**
  1. Bắt đầu transaction trước mọi read.
  2. Claim event inbox atomically bằng `INSERT ... ON CONFLICT DO NOTHING`; nếu không insert được thì trả duplicate success.
  3. Khóa `Order` và `OrderShipment` bằng `SELECT ... FOR UPDATE` theo thứ tự khóa cố định, hoặc dùng conditional update `WHERE last_event_at_utc <= @eventAt` kết hợp concurrency token/retry.
  4. Không dùng entity đã load trước transaction để mutation.
  5. Thêm integration test thật với hai `DbContext`/hai request `Task.WhenAll`, gồm duplicate event và cặp event cũ/mới có commit order đảo ngược.

- **Acceptance criteria:**
  - Hai request đồng thời cùng `EventId` đều trả semantics thành công/idempotent; database chỉ có một inbox/timeline/side-effect tương ứng và không có 500 do unique violation.
  - Event cũ commit sau event mới không làm giảm `LastEventAtUtc`, shipment status hoặc order status.
  - Lock order được cố định và document trong code để tránh deadlock; toàn bộ read dùng để quyết định mutation nằm trong transaction.
  - Loyalty completion và mọi durable side effect chỉ xảy ra một lần.
- **Verification:**
  - Thêm test PostgreSQL dùng hai scope/`DbContext` độc lập và synchronization barrier, không dùng fake để chứng minh race.
  - Chạy `dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShipmentWebhookIntegrationTests"` với Docker, lặp tối thiểu 10 lần trong CI hoặc harness ổn định tương đương.
  - Chạy toàn bộ Application và API integration tests để kiểm tra order, shipment và loyalty regression.

### TASK-03 — Loại bỏ dependency SSH.NET có lỗ hổng High (F-03)

- **Completion evidence (2026-09-23):** the integration-test project pins patched `SSH.NET 2026.0.0`; two locked restores left the lock file unchanged; the CI audit report contained zero vulnerable entries; all 321 Application, 205 Infrastructure, and 91 PostgreSQL/API integration tests passed in Release. The audit assertion script was also corrected to handle an empty findings set under PowerShell StrictMode.

- **Loại:** Supply-chain risk / release blocker
- **Severity:** **High** cho pipeline; **Low hơn đối với runtime production** vì dependency nằm trong integration-test project và exploit yêu cầu dùng SCP recursive download với server độc hại.
- **Goal:** Locked dependency graph không còn phiên bản SSH.NET bị ảnh hưởng và CI vulnerability gate chạy xanh mà không suppress advisory.
- **Scope:** Integration-test package references, `packages.lock.json`, restore/test và NuGet audit. Không nâng package ngoài dependency chain này nếu không bắt buộc.
- **Vị trí:**
  - `tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj:15`
  - `tests/WorkspaceEcommerce.Api.IntegrationTests/packages.lock.json:853-860`
  - `tests/WorkspaceEcommerce.Api.IntegrationTests/packages.lock.json:940-949`
  - `.github/workflows/ci.yml:72-78`
- **Bằng chứng:** NuGet audit ngày 2026-09-23 xác nhận `Testcontainers.PostgreSql 4.12.0` kéo transitive `SSH.NET 2025.1.0` và phát hai `NU1903`: [GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284), [GHSA-mggc-4xg6-vcxf](https://github.com/advisories/GHSA-mggc-4xg6-vcxf).
- **Vấn đề:** CI gọi `dotnet list ... --vulnerable --include-transitive` rồi enforce báo cáo không có vulnerability. Vì vậy dependency hiện tại vừa tạo supply-chain exposure trong môi trường build/test, vừa có thể chặn merge/release.
- **Implementation checklist:** pin trực tiếp một phiên bản `SSH.NET` đã vá cả hai advisory hoặc nâng Testcontainers lên version có dependency graph đã vá; cập nhật lock file, chạy toàn bộ integration test và bước audit. Version được chọn phải được NuGet audit hiện tại xác nhận; không suppress `NU1903` nếu chưa có risk acceptance có thời hạn.
- **Acceptance criteria:**
  - `packages.lock.json` không resolve phiên bản SSH.NET bị ảnh hưởng; locked restore không tạo diff.
  - NuGet audit không còn `GHSA-q939-rpr3-3284`, `GHSA-mggc-4xg6-vcxf` và không còn High/Critical finding nào khác.
  - Toàn bộ backend tests, đặc biệt Testcontainers PostgreSQL, vẫn pass.
- **Verification:**
  - Chạy `dotnet restore WorkspaceEcommerce.slnx --locked-mode` hai lần và xác nhận `git diff --exit-code -- '**/packages.lock.json'`.
  - Chạy `dotnet test WorkspaceEcommerce.slnx --no-restore --configuration Release` với Docker.
  - Chạy đúng CI gate: xuất `dotnet list WorkspaceEcommerce.slnx package --vulnerable --include-transitive --format json`, sau đó chạy `./scripts/assert-no-nuget-vulnerabilities.ps1` với report vừa tạo.

### TASK-04 — VNPay callback fail-closed với required fields (F-04)

- **Completion evidence (2026-09-23):** signature validity and payload validity are separate; required VNPay v2.1 callback fields are checked before any database read/mutation; amount must be numeric, positive, within provider length, and equal the stored amount; success requires both response/status `00`. Signed missing/malformed/negative/overflow callbacks return IPN `99` or a neutral failed browser redirect without changing payment/order/outbox state. All 322 Application, 216 Infrastructure, and 97 PostgreSQL/API integration tests passed in Release.

- **Loại:** Risk về payment integrity
- **Severity:** **Medium**, tác động tiềm năng High nhưng khả năng khai thác trực tiếp thấp do callback vẫn phải có chữ ký hợp lệ
- **Goal:** Callback chỉ được phép mutation khi chữ ký hợp lệ và mọi field bắt buộc đều hiện diện, parse hợp lệ, đúng transaction/amount/currency/status.
- **Scope:** VNPay verification/result model, Application callback guard, IPN/return response mapping và focused tests. Không thay đổi signing algorithm/provider protocol ngoài contract VNPay.
- **Vị trí:**
  - `src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs:74-103`
  - `src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs:158-165`
  - `src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs:125-147`
  - `src/WorkspaceEcommerce.Infrastructure/Payments/VNPayPaymentService.cs:106-117`
- **Code path:** parse lỗi hoặc thiếu `vnp_Amount` trả `null`; Application chỉ reject khi amount khác `null` **và** không bằng expected amount. `GetPaymentOutcome` cũng coi `transactionStatus` rỗng là success nếu `responseCode == "00"`.
- **Vấn đề:** validation đang fail-open đối với field quan trọng của giao dịch. Attacker ngoài không thể tự ký payload, nhưng payload malformed do provider/proxy/configuration error vẫn có thể đánh dấu đơn đã thanh toán.
- **Implementation checklist:** chữ ký hợp lệ là điều kiện cần, không phải đủ. Bắt buộc `TxnRef`, `Amount`, `ResponseCode`, `TransactionStatus` và các field theo contract; amount phải parse được, dương và bằng chính xác expected amount/currency. Payload thiếu field phải trả mã VNPay phù hợp và không mutation. Bổ sung test missing/malformed/negative/overflow amount và missing transaction status.
- **Acceptance criteria:**
  - Signed payload thiếu/sai format/âm/overflow `vnp_Amount`, thiếu status hoặc required field không thay đổi payment/order/shipment.
  - Success chỉ xảy ra khi cả `vnp_ResponseCode == "00"` và `vnp_TransactionStatus == "00"`; status rỗng không còn được coi là success.
  - IPN trả đúng response code contract cho invalid/missing data; return flow không tiết lộ raw provider data.
  - Duplicate callback hợp lệ vẫn idempotent.
- **Verification:**
  - Mở rộng `VNPayPaymentServiceTests`, `PaymentServiceTests`, `PaymentIntegrationTests` cho ma trận malformed fields và database non-mutation.
  - Chạy ba test class trên; sau đó chạy toàn bộ backend suite.

### TASK-05 — Rate limiting đúng identity và scale-out (F-05)

- **Repository completion evidence (2026-09-23):** authentication now precedes rate limiting; authenticated customer/client keys are opaque and canonical; auth, 2FA, checkout, payment, provider webhook, warranty, catalog, and default policies use independent configurable buckets; every rejection emits `Retry-After`. Three API tests prove customer partitioning, policy isolation/limit boundaries, anonymous IP partitioning, and header behavior; all 322 Application, 216 Infrastructure, and 100 PostgreSQL/API integration tests passed in Release.
- **Remaining Platform gate:** the application limiter is intentionally per process. TASK-05 is not fully Done until Platform/SRE attaches a two-replica staging result proving a shared edge/distributed quota survives replica restart and preserves the separate provider-webhook bucket.

- **Loại:** Risk bảo mật/vận hành
- **Severity:** **Medium**
- **Goal:** Security-critical quota dùng đúng authenticated identity và có hiệu lực trên toàn deployment; webhook không tranh bucket với checkout/payment.
- **Scope:** Middleware order, partition keys/policies, response headers, deployment edge/distributed limiter configuration và tests. Không tự thêm distributed framework khi gateway/WAF hiện hữu đã đáp ứng được yêu cầu.
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
- **Implementation checklist:** đặt authentication trước limiter đối với partition cần claim; dùng gateway/WAF hoặc distributed store cho quota security-critical; kết hợp normalized account/challenge/customer ID với IP; tách quota webhook theo provider credential/signature và có retry semantics; bổ sung test middleware thật cho claim partition, `Retry-After` và 429.
- **Acceptance criteria:**
  - Authenticated warranty requests partition theo customer claim sau authentication; anonymous requests không dùng chung một global `anonymous` bucket.
  - Auth, checkout/payment và provider webhook có policy/key riêng; `429` có `Retry-After` và không làm provider retry sai semantics.
  - Hai replicas chia sẻ cùng quota tại edge/distributed store, hoặc có evidence kiến trúc chứng minh edge là enforcement authority duy nhất.
  - Key không chứa raw email/phone/token/signature và trusted proxy handling vẫn đúng.
- **Verification:**
  - Thêm API middleware tests cho authenticated/anonymous partition, policy separation, limit boundary và `Retry-After`.
  - Chạy focused API integration tests và full backend suite.
  - Platform chạy two-replica test trên staging: tổng accepted requests không vượt quota toàn cục; restart một replica không reset quota. Đính kèm cấu hình đã redacted và metric/log evidence.

### TASK-06 — Đặt query budget cho cart/checkout (F-06)

- **Loại:** Performance bug/risk
- **Severity:** **Medium**
- **Goal:** Số SQL round trip của cart và checkout bị chặn bởi một budget cố định, không tăng tuyến tính theo số cart item, trong khi stock/price vẫn được revalidate dưới lock.
- **Scope:** Cart DTO query, checkout snapshot query/locking, VNPay quote/place flow và PostgreSQL command-count tests. Không cache dữ liệu authoritative qua transaction boundary.
- **Vị trí:**
  - `src/WorkspaceEcommerce.Application/Modules/Cart/StorefrontCartService.cs:240-277`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutCartBuilder.cs:20-61`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutService.cs:121-146`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderPlacer.cs:35-47`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/AppDbContext.cs:481-531`
- **Vấn đề:** cart DTO thực hiện tới ba query cho mỗi item (variant, product, primary image). Checkout thực hiện variant/product/category cho từng item. Với VNPay, bộ snapshot này được dựng một lần để quote shipping trước transaction và thêm một lần trong transaction, tạo khoảng `6N` query cùng dữ liệu liên quan.
- **Tác động:** latency tăng tuyến tính theo số item, tăng connection pressure và kéo dài transaction trong checkout.
- **Implementation checklist:** thêm query batch/projection cho toàn bộ variant IDs, product/category/image trong 1-3 round trip; khóa variants theo danh sách đã sort trong một query (`FindProductVariantsForUpdateAsync` đã tồn tại nhưng chưa được dùng ở flow này); đo command count trong integration test. Quote ngoài transaction có thể dùng snapshot chỉ để gọi provider, nhưng trong transaction vẫn phải revalidate stock/price.
- **Acceptance criteria:**
  - Cart projection tải catalog/image bằng batch/projection; không có query bên trong vòng lặp item.
  - Checkout khóa variant theo danh sách ID đã sort trong một batch, revalidate active/stock/price trong transaction và giữ nguyên order snapshots.
  - Command-count test với 1 và 20 items chứng minh phần catalog của cart/checkout tăng không quá một command; test ghi rõ budget tổng để regression có tín hiệu rõ.
  - COD, bank transfer và VNPay cho cùng totals/snapshots như trước; VNPay không gọi provider mutation ngoài durable path.
- **Verification:**
  - Thêm PostgreSQL integration test dùng command interceptor/counter cho cart và từng payment method.
  - Chạy `CartCheckoutAndOrderLookupIntegrationTests`, focused Application checkout/cart tests, rồi full backend suite.
  - Ghi command count 1-item/20-item trước và sau trong PR/task evidence.

### TASK-07 — Fail-fast email configuration ngoài Development (F-07)

- **Loại:** Bug cấu hình / security risk
- **Severity:** **Medium**
- **Goal:** Mọi environment ngoài Development phải fail startup nếu email có thể bị discard hoặc truyền credential/link qua SMTP không TLS.
- **Scope:** Email configuration validator, provider registration, configuration tests và runbook/config matrix. Không thay SMTP library/provider nếu validator đủ đáp ứng.
- **Vị trí:**
  - `src/WorkspaceEcommerce.Infrastructure/Configuration/EmailDeliveryConfigurationValidator.cs:14-26`
  - `src/WorkspaceEcommerce.Infrastructure/Configuration/EmailDeliveryConfigurationValidator.cs:48-78`
  - `src/WorkspaceEcommerce.Infrastructure/Notifications/CustomerEmailDeliveryServices.cs:9-18`
  - `src/WorkspaceEcommerce.Infrastructure/Notifications/CustomerEmailDeliveryServices.cs:21-39`
- **Vấn đề:** message nói provider `Log` chỉ hợp lệ trong Development, nhưng code chỉ cấm khi environment name đúng bằng `Production`; Staging/QA vẫn chấp nhận Log và worker coi email đã gửi dù chỉ log subject. Với SMTP, validator không yêu cầu `EnableSsl` ngoài Development.
- **Tác động:** verification/reset email biến mất ở staging hoặc môi trường tên khác; link/token tài khoản có thể truyền qua SMTP không mã hóa nếu cấu hình sai.
- **Implementation checklist:** dùng semantic `environment.IsDevelopment()` thay vì so chuỗi Production; ngoài Development bắt buộc provider gửi thật và TLS, trừ một risk-acceptance flag rõ ràng; validate cặp username/password; thêm configuration tests cho Development, Staging, QA và Production.
- **Acceptance criteria:**
  - `Log` provider chỉ hợp lệ trong Development; Staging/QA/Production đều fail startup.
  - SMTP ngoài Development bắt buộc TLS và cặp username/password nhất quán; placeholder/partial credentials bị từ chối.
  - Error message nêu đúng key cấu hình nhưng không in secret.
  - Configuration matrix/runbook phản ánh chính xác rule được test.
- **Verification:**
  - Mở rộng `EmailDeliveryConfigurationValidatorTests` thành ma trận Development/Staging/QA/Production × Log/SMTP/TLS/credentials.
  - Chạy focused Infrastructure tests và khởi động API bằng cấu hình invalid trong test để chứng minh fail-fast.

### TASK-08 — Cleanup dữ liệu theo bounded batches (F-08)

- **Loại:** Reliability/performance risk
- **Severity:** **Medium**
- **Goal:** Mỗi cleanup cycle có giới hạn rõ về row count/thời gian/transaction và có thể tiếp tục an toàn ở cycle sau mà không materialize toàn bộ backlog.
- **Scope:** Account cleanup query/delete loop, options validation, metrics và PostgreSQL tests. Giữ advisory lock hiện hữu và retention semantics.
- **Vị trí:** `src/WorkspaceEcommerce.Infrastructure/Notifications/CustomerAccountCleanupWorker.cs:37-90`
- **Vấn đề:** worker tải toàn bộ token, refresh family, 2FA challenge/recovery code, login history và delivered email quá hạn vào memory, tracking tất cả entity rồi delete trong một `SaveChanges`. Advisory lock ngăn nhiều replica chạy đồng thời nhưng không giới hạn kích thước batch.
- **Tác động:** memory spike, transaction/WAL lớn, lock lâu và timeout khi dữ liệu tăng.
- **Implementation checklist:** dùng `ExecuteDeleteAsync` theo batch có giới hạn hoặc keyset pagination, commit từng batch, giới hạn thời gian mỗi vòng, phát metric số row/duration/error. Thứ tự xóa phải tôn trọng FK, đặc biệt refresh token trước family.
- **Acceptance criteria:**
  - Batch size và cycle time budget được cấu hình/validate; không query nào materialize toàn bộ expired rows.
  - Mỗi transaction xóa tối đa batch size đã cấu hình; cancellation dừng giữa các batch mà dữ liệu đã commit vẫn nhất quán.
  - FK order đúng, dữ liệu chưa hết hạn không bị xóa, backlog lớn được drain qua nhiều batch/cycle.
  - Metrics có deleted count, duration, remaining/backlog hoặc equivalent signal, và failure count; không chứa PII/token.
- **Verification:**
  - Thêm PostgreSQL integration test seed nhiều hơn ít nhất ba lần batch size, kiểm tra giới hạn từng batch, retention boundary, FK và cancellation/resume.
  - Chạy focused cleanup/configuration tests và full Infrastructure/API integration suite.

### TASK-09 — Đồng nhất concurrency boundary cho warranty activation (F-09)

- **Loại:** Risk đồng thời; hiện bị giảm mức độ vì warranty mặc định tắt
- **Severity:** **Medium** trước khi bật feature
- **Goal:** Admin và customer activation dùng cùng invariant/lock order; hai request đồng thời chỉ tạo một activation cùng một bộ snapshot/audit/email.
- **Scope:** Activation coordinator hoặc shared transactional routine, EF lock methods/constraints, migration nếu cần, và concurrent PostgreSQL tests. Không mở rộng sang refactor toàn bộ `AdminWarrantyService`.
- **Vị trí:**
  - `src/WorkspaceEcommerce.Application/Modules/Warranties/AdminWarrantyService.cs:784-839`
  - `src/WorkspaceEcommerce.Application/Modules/Warranties/CustomerWarrantyService.cs:61-153`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/Configurations/Warranties/WarrantyEntitlementConfiguration.cs:13-45`
  - `src/WorkspaceEcommerce.Infrastructure/Persistence/Configurations/Warranties/WarrantyCoverageSnapshotConfiguration.cs:11-22`
- **Vấn đề:** customer flow mở transaction và khóa serialized unit/order; admin flow load entitlement/unit/order/plan bình thường rồi mutation/save, không row lock và không concurrency token. Hai admin request có thể cùng thấy Pending và cùng tạo coverage snapshot/audit/email. Coverage snapshot không có unique constraint theo entitlement/component để chặn bản sao.
- **Implementation checklist:** dùng chung một transactional activation coordinator cho admin/customer; khóa unit, entitlement/order theo thứ tự thống nhất; thêm unique constraint phù hợp cho snapshot; bắt và map concurrency conflict. Phải có concurrent integration test trước khi bật `Warranty:AdminEnabled`.
- **Acceptance criteria:**
  - Admin activation bắt đầu transaction trước read quyết định và khóa unit/entitlement/order theo cùng thứ tự với customer flow.
  - Hai activation đồng thời cho cùng entitlement cho kết quả một success + một idempotent/conflict có kiểm soát; không có duplicate coverage snapshot, audit hoặc email outbox.
  - Unique constraint bảo vệ snapshot identity phù hợp và migration nâng cấp dữ liệu hiện hữu an toàn.
  - Feature vẫn disabled mặc định cho đến khi concurrent integration test pass.
- **Verification:**
  - Thêm PostgreSQL test dùng hai scope/`DbContext` và synchronization barrier cho admin-admin và admin-customer race.
  - Chạy `WarrantyIntegrationTests`, Application warranty tests, migration pending-model check và migration verification script nếu schema thay đổi.

### TASK-10 — Chốt và mã hóa cart price policy (F-10)

- **Loại:** Business risk, không kết luận là bug vì test hiện tại chủ động kỳ vọng hành vi này
- **Severity:** **Medium** nếu nghiệp vụ không cam kết price lock
- **Goal:** Có một policy được Product phê duyệt và test hóa cho thời điểm giá được chốt, thời hạn hiệu lực, UX khi giá đổi và audit source; không còn price lock vô thời hạn do ngầm định kỹ thuật.
- **Scope:** Trước hết là decision record. Chỉ sau khi được duyệt mới thay Domain/Application/API/frontend/migration cần thiết. Không tự chọn repricing hay price lock thay Product.
- **Vị trí:**
  - `src/WorkspaceEcommerce.Domain/Modules/Cart/Cart.cs:38-53`
  - `src/WorkspaceEcommerce.Domain/Modules/Cart/CartItem.cs:21-39`
  - `src/WorkspaceEcommerce.Application/Modules/Cart/StorefrontCartService.cs:45-84`
  - `src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutCartBuilder.cs:40-57`
  - `tests/WorkspaceEcommerce.Application.Tests/Modules/Ordering/CheckoutServiceTests.cs:22-53`
- **Vấn đề:** khi item đã tồn tại, tăng quantity không cập nhật `UnitPriceSnapshot`; checkout dùng snapshot thay vì `variant.Price`. Cart không có expiration/price-lock deadline. Test đang chứng minh variant giá 150 nhưng order dùng snapshot 120, nên đây là policy hiện hữu chứ không phải tình cờ.
- **Tác động:** người dùng có thể giữ giá cũ lâu dài sau khi catalog tăng giá; giảm doanh thu hoặc tạo tranh chấp về giá.
- **Decision checklist:** Product owner chọn và ký một trong hai contract: (A) checkout reprice theo catalog hiện tại và yêu cầu user xác nhận thay đổi, hoặc (B) price lock có `PriceLockedUntil`, nguồn giá và expiry/reservation rõ ràng. Decision phải nêu coupon/shipping/tax interaction, guest/customer parity và hành vi khi lock hết hạn.
- **Implementation checklist sau decision:** cập nhật ADR/domain rules; triển khai đúng một contract end-to-end; thêm migration nếu lưu deadline/source; cập nhật storefront UX/i18n và tests. Không bắt đầu implementation khi decision chưa được phê duyệt.
- **Acceptance criteria:**
  - ADR hoặc product rule được owner phê duyệt, nêu ví dụ giá tăng/giảm, add quantity, reopen cart, concurrent checkout và expired lock.
  - Domain/Application không còn hành vi vô thời hạn ngoài policy; totals server-authoritative và được revalidate trong transaction.
  - API trả đủ dữ liệu để UI hiển thị/confirm price change hoặc lock expiry; UI không tự quyết định giá.
  - Tests cố định behavior cho cả tăng và giảm giá, boundary expiry và race với catalog update.
- **Verification:**
  - Review ADR/product sign-off là gate đầu tiên.
  - Chạy focused cart/checkout Domain + Application tests, PostgreSQL checkout integration tests và storefront tests/typecheck/build.
  - Nếu có migration, chạy pending-model check và `./scripts/verify-prh-009-migrations.ps1`.

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

## 8. Execution Order

### P0 — Phải hoàn thành trước release

1. **TASK-01:** chốt payment-result contract trước vì thay đổi API/redirect/storefront và là security boundary public.
2. **TASK-02:** triển khai độc lập với TASK-01; merge chỉ khi concurrent PostgreSQL tests chứng minh atomic idempotency và monotonic state.
3. **TASK-03:** có thể thực hiện song song; phải hoàn tất trước khi dùng CI/release evidence của TASK-01 và TASK-02.

P0 hoàn thành khi cả ba task có evidence trên cùng release candidate sạch; không đóng release gate chỉ bằng unit test hoặc code review.

### P1 — Sprint kế tiếp

1. **TASK-04:** payment integrity; thực hiện ngay sau P0.
2. **TASK-07:** configuration fail-fast, phạm vi nhỏ và giảm rủi ro môi trường.
3. **TASK-05:** hoàn thành phần code/test trong repo, sau đó Platform cung cấp evidence multi-replica/edge.
4. **TASK-06:** tối ưu query sau khi payment/checkout correctness đã được khóa bằng regression tests.
5. **TASK-08:** bounded cleanup và operational metrics.
6. **TASK-09:** bắt buộc trước khi bật `Warranty:AdminEnabled`.
7. **TASK-10:** Product decision có thể chạy song song; implementation chỉ bắt đầu sau sign-off.

Backlog liên quan nhưng không phải task đóng các finding trên: chuyển `ClientIpAddress` khỏi request contract sang trusted server context; bổ sung DB check constraints cho amount, quantity, stock và financial date ranges trong một schema-change task riêng.

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

1. TASK-01 — Payment result authorization/tokenization.
2. TASK-02 — Shipment webhook concurrency + atomic idempotency.
3. TASK-03 — Vá dependency SSH.NET và khôi phục CI audit xanh.
4. TASK-04 — VNPay callback required-field/amount validation fail-closed.
5. TASK-05 rồi TASK-06 — Rate limiter middleware/distributed strategy, sau đó batch hóa cart/checkout.

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
