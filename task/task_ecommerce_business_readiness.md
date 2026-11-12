# Ecommerce Business Readiness Implementation Plan

> **For agentic workers:** Khi được yêu cầu triển khai, dùng superpowers:executing-plans hoặc superpowers:subagent-driven-development theo phương thức người dùng chọn. Thực hiện theo task và checkbox; tài liệu này chỉ lập kế hoạch, không phải yêu cầu triển khai ngay.

**Goal:** Khép kín nghiệp vụ một shop thực tế: không mất dấu tiền, không sai tồn kho, hủy/giao lại/trả hàng có thể xử lý và đối soát qua admin.

**Architecture:** Giữ .NET/PostgreSQL modular monolith, direct EF và các focused store hiện có. Bổ sung các nghiệp vụ cụ thể trong Ordering, Payments, Catalog, Shipments, Loyalty và Warranties; provider mutations tiếp tục qua outbox/inbox, không tạo framework điều phối mới.

**Tech Stack:** .NET SDK 10.0.202, EF Core/PostgreSQL, React 19/TypeScript/Vite, TanStack Query, React Hook Form/Zod, pnpm 10.24.0, xUnit/Testcontainers và Vitest/Playwright hiện có.

**Spec:** Phần 2–4 của chính tài liệu này là thiết kế đề xuất và quy tắc đích; source/tests/accepted ADRs là baseline. Các chính sách mới bên dưới chưa phải hành vi đang chạy và chưa phải ADR đã được phê duyệt.

**Ngày lập:** 2026-11-05, Asia/Bangkok. **Trạng thái:** Planned; chưa task triển khai nào được đánh dấu hoàn thành.

## 1. Phạm vi và cách dùng

- Phạm vi: một shop, một kho, một shipment/phương thức giao trên một đơn; COD, chuyển khoản thủ công, VNPay; WorkspaceEC và MiniLogistics.
- Mỗi task gồm backend, contracts/UI cần thiết để chủ shop dùng được, regression và hướng dẫn vận hành tương ứng. Chia PR nhỏ theo deliverable; không gom toàn bộ phase thành một PR.
- Độ ưu tiên là mức chặn sẵn sàng bán hàng, không phải khẳng định đang có incident production: **P0** = ngăn sai hàng/tiền hoặc mất khả năng phục hồi; **P1** = hoàn thiện vận hành trước mở bán rộng; **P2** = nâng chất lượng quản trị sau khi luồng cốt lõi an toàn.
- Không đặt deadline theo ngày khi chưa có velocity và môi trường kiểm thử. Dùng phụ thuộc và exit gate để quyết định thứ tự.
- Giữ nguyên task/task_shipment.md, task/task-warranty.md và task/task.md. Kế hoạch này bổ sung nghiệp vụ, không tự đổi trạng thái các kế hoạch cũ hoặc thay thế production release gate.
- Không yêu cầu microservice, generic repository, MediatR/CQRS, event bus, đa kho, marketplace, kế toán tổng hợp hoặc tự tích hợp thêm nhà cung cấp.

### Global Constraints

- Đọc .agents/README.md và các hướng dẫn được route trước mỗi task; bảo toàn working tree đang có thay đổi.
- VND nguyên, backend decimal, exchange rate 1; giá/tổng tiền/phí/discount/stock/payment do server quyết định.
- Giữ UnitPriceSnapshot của giỏ theo chính sách hiện tại; không tự đổi thành repricing hoặc áp thời hạn giá giỏ.
- Không viết lại order item/giá/địa chỉ/coupon/rate snapshots đã chốt, không đổi nghĩa provider ShipmentId hoặc giả ID external cho local.
- Không sửa migration lịch sử; schema mới dùng migration additive, có clean/upgrade verification và rollout tương thích worker/client.
- Giữ route/enum values/provider signatures và authorization/ownership hiện có, trừ thay đổi được task mô tả tường minh. Thuật ngữ/tên file mới là thiết kế đề xuất.
- Không gọi HTTP provider trong transaction giữ khóa; sau HTTP phải khóa và đọc lại dữ liệu authoritative.
- Actor lấy từ claims, timestamp từ TimeProvider, requestId/idempotency phải được bảo vệ bằng DB constraint/transaction.
- Không log/chụp vào artifact secrets, full bank details của khách, token, chữ ký callback hoặc Serial/IMEI thô; bảo toàn HMAC/mask của warranty.
- PostgreSQL race/translation/constraint không được chứng minh bằng fake hoặc EF InMemory.

### Review Focus

| Điều kiện dễ bỏ sót | Hành vi bắt buộc | Task sở hữu |
| --- | --- | --- |
| Provider nhận tiền trước hủy nhưng IPN đến sau; callback lặp/sai amount | Giữ đơn hủy, ghi nhận tiền thật, tạo đúng một ngoại lệ; callback không hợp lệ không ghi tiền | EC-11, EC-23, EC-24 |
| Admin mở form cũ, import cạnh checkout, VNPay đọc sớm trước khóa | Không hồi sinh tồn hoặc bán vượt tồn; conflict rõ ràng, không mất audit | EC-12, EC-13, EC-14 |
| Refresh chậm cạnh hủy/webhook; provider bỏ qua sự kiện giao lại | Không hồi sinh đơn đã hủy, trạng thái theo sự kiện hợp lệ mới nhất, hoàn tất đúng một lần | EC-15, EC-16 |
| COD đã thu, sau đó trả/hủy, rồi mới nộp về shop | Vẫn ghi nhận nộp tiền; không tính thu/nộp hai lần thành doanh thu | EC-18, EC-22, EC-40 |
| Trả một phần, hai request cùng item, điểm đã tiêu, serial đã kích hoạt | Không nhận/hoàn quá số lượng/tiền; không nhập hàng hỏng vào hàng bán; không xóa lịch sử | EC-30, EC-31, EC-32, EC-33 |

## 2. Phân tích hiện trạng và bằng chứng

Các lỗi theo luồng source dưới đây cần regression trước khi sửa; các race chưa được tái hiện trên PostgreSQL ở phiên đánh giá trước.

| ID | Vấn đề và tác động nghiệp vụ | Bằng chứng hiện tại | Phân loại | Task |
| --- | --- | --- | --- | --- |
| F01 | Hủy local transaction trước IPN thành công có thể làm mất dấu khoản VNPay đã thu | [PaymentService](../src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs), nhánh IsTerminal; [AdminOrderService](../src/WorkspaceEcommerce.Application/Modules/Ordering/AdminOrderService.cs), CancelOrOpenRefundWorkflow | Lỗi theo code | EC-11 |
| F02 | Hủy vận đơn/provider Cancelled đổi Order trực tiếp, bỏ qua hoàn tồn và xử lý payment | [ShipmentOrderStatusTransitioner](../src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentOrderStatusTransitioner.cs), [OrderShipmentService](../src/WorkspaceEcommerce.Application/Modules/Shipments/OrderShipmentService.cs), [ShipmentWebhookService](../src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentWebhookService.cs) | Lỗi theo code | EC-10 |
| F03 | Sửa SKU với StockQuantity từ form cũ có thể ghi đè tồn vừa bán | [AdminProductVariantService](../src/WorkspaceEcommerce.Application/Modules/Catalog/Products/AdminProductVariantService.cs), UpdateVariantAsync | Lỗi stale update | EC-12 |
| F04 | Legacy VNPay đọc tracked variant trước báo phí rồi khóa nhưng không reload | [CheckoutService](../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutService.cs), [AppDbContext](../src/WorkspaceEcommerce.Infrastructure/Persistence/AppDbContext.cs) | Race theo source, chưa tái hiện DB | EC-13 |
| F05 | Import chuẩn bị variant trước transaction, giảm tồn không khóa/revalidate | [AdminOrderService](../src/WorkspaceEcommerce.Application/Modules/Ordering/AdminOrderService.cs), BuildImportPlanAsync/CommitImportAsync | Race theo source, chưa tái hiện DB | EC-14 |
| F06 | Refresh tracking áp dụng lên order/shipment đã đọc trước HTTP | [OrderShipmentService](../src/WorkspaceEcommerce.Application/Modules/Shipments/OrderShipmentService.cs), RefreshAsync/ApplyTrackingAsync | Race theo source, chưa tái hiện DB | EC-15 |
| F07 | MiniLogistics không tự tiến từ FailedDelivery sang Shipping/Completed khi giao lại | [ShipmentOrderStatusTransitioner](../src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentOrderStatusTransitioner.cs), GetNextStatus | Lỗi theo code | EC-16 |
| F08 | Tạo/sửa SKU qua admin không nhập được cân nặng bắt buộc cho WorkspaceEC | [CreateProductVariantRequest](../src/WorkspaceEcommerce.Application/Modules/Catalog/Products/CreateProductVariantRequest.cs), [WorkspaceShippingRateCalculator](../src/WorkspaceEcommerce.Application/Modules/Shipments/WorkspaceShippingRateCalculator.cs) | Thiếu luồng vận hành | EC-17 |
| F09 | COD remittance bị chặn sau Returned/Cancelled dù tiền đã thu | [OrderPaymentService](../src/WorkspaceEcommerce.Application/Modules/Payments/OrderPaymentService.cs), ConfirmAsync/SummaryAsync | Lỗi theo code | EC-18 |
| F10 | Coupon full update có thể ghi đè UsedCount tăng bởi checkout | [AdminCouponService](../src/WorkspaceEcommerce.Application/Modules/Coupons/AdminCouponService.cs), [AppDbContext](../src/WorkspaceEcommerce.Infrastructure/Persistence/AppDbContext.cs), Set.Update | Race theo source, chưa tái hiện DB | EC-19 |
| F11 | RefundPending chưa có chứng từ và bước xác nhận tiền đã hoàn | [PaymentStatus](../src/WorkspaceEcommerce.Domain/Modules/Ordering/PaymentStatus.cs), [OrderPaymentsController](../src/WorkspaceEcommerce.Api/Controllers/Admin/OrderPaymentsController.cs) | Khoảng trống nghiệp vụ | EC-21, EC-22 |
| F12 | Đơn bỏ/failed payment giữ tồn; thiếu expiry/retry an toàn và chính sách hoàn lượt coupon | [QuotedCheckoutService](../src/WorkspaceEcommerce.Application/Modules/Ordering/QuotedCheckoutService.cs), [PaymentService](../src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs) | Khoảng trống nghiệp vụ | EC-23, EC-24 |
| F13 | RequestReturn đổi thẳng Completed sang Returned; không kiểm hàng/refund/loyalty/warranty | [CustomerOrderService](../src/WorkspaceEcommerce.Application/Modules/Customers/Orders/CustomerOrderService.cs), RequestReturnAsync | Phạm vi hiện tại chưa đủ | EC-30 đến EC-33 |
| F14 | Kho chỉ có số tồn trên SKU, thiếu biến động/kiểm kê/nhận hàng hoàn có căn cứ | [ProductVariant](../src/WorkspaceEcommerce.Domain/Modules/Catalog/ProductVariant.cs) | Khoảng trống nghiệp vụ | EC-20, EC-41 |
| F15 | Completed revenue không phản ánh tiền thực thu, COD chờ nộp và tiền phải hoàn | [EfAdminDashboardQuery](../src/WorkspaceEcommerce.Infrastructure/Persistence/Queries/EfAdminDashboardQuery.cs) | Thiếu báo cáo, không gọi metric cũ là sai | EC-40 |
| F16 | Quyền lợi tier đang hiển thị nhưng chưa áp checkout; chưa đảo điểm khi trả | [LoyaltyService](../src/WorkspaceEcommerce.Application/Modules/Loyalty/LoyaltyService.cs), [AccountPages](../frontend/apps/storefront/src/pages/account/AccountPages.tsx) | Khoảng trống và thông tin chưa khớp | EC-32, EC-42 |
| F17 | Trang chuyển khoản thiếu ngân hàng/tài khoản/chủ tài khoản; chính sách và thông báo chưa đủ cho shop thật | [CheckoutSuccessPage](../frontend/apps/storefront/src/pages/checkout/CheckoutSuccessPage.tsx), [SupportPages](../frontend/apps/storefront/src/pages/support/SupportPages.tsx) | Thiếu vận hành/nội dung | EC-25, EC-43 |

### Những gì đã có và phải bảo toàn

- Catalog/SKU/biến thể/active state; cart guest/customer; VND; coupon và snapshots đơn.
- Checkout mới khóa quote/cart/variant/coupon, cố định phí khách và phục hồi accepted-request retry.
- VNPay kiểm tra chữ ký/payload/amount, khóa transaction và order; shipment outbox và webhook inbox.
- WorkspaceEC có bảng giá phiên bản, dispatch/failure/retry; manual transfer confirmation và COD collection/remittance đã tồn tại cho đơn có shipping selection.
- Account ownership, guest possession proof, receipt, loyalty ledger và feature flags warranty.
- Một số bullet trong known-limitations.md về chuyển khoản/phí ship đã chậm hơn source. Không tạo task xây lại chức năng đã có chỉ vì tài liệu cũ nói thiếu.

## 3. Quyết định nghiệp vụ đề xuất để triển khai

Người dùng đã giao quyền suy luận theo shop thực tế. Những lựa chọn dưới đây là đề xuất của người lập kế hoạch; không mô tả là owner đã duyệt ADR, không tự áp dụng lên đơn lịch sử.

| ID | Quyết định đích | Lý do và ranh giới |
| --- | --- | --- |
| D01 | Một shop/một kho; tồn bán được tách khỏi hàng đang giao và hàng chờ kiểm. Tiếp tục giảm lượng có thể bán khi nhận đơn, chưa cần hệ thống reservation mới song song. | Bảo toàn checkout hiện có; thêm dấu vết và lifecycle thay vì thay toàn bộ model kho. |
| D02 | Hủy trước bàn giao hoàn lượng giữ đúng một lần. Hủy sau bàn giao/giao thất bại không tự nhập lại hàng bán được; phải có nhận hàng hoàn và kiểm tra. Provider hủy vận đơn không mặc nhiên hủy giao dịch mua bán. | Không bán lại hàng vẫn ở carrier. Với Pending/Confirmed chưa bàn giao, áp dụng hủy commerce qua coordinator; sự kiện không phù hợp custody tạo ngoại lệ để admin xử lý. |
| D03 | Ghi nhận sự thật tiền thu độc lập với trạng thái Order. Khoản thu muộn/thu trùng tạo exception và nghĩa vụ hoàn/đối soát, không kích hoạt lại đơn đã hủy. | Chống mất dấu tiền và gửi hàng lại sau hủy. |
| D04 | COD collected và remitted là hai sự kiện; remittance vẫn cho phép sau hủy/trả khi có collection chưa nộp. Không tính remittance thêm một lần vào tiền bán hàng. | Tiền đang ở nhân viên/carrier vẫn phải quyết toán. Bản đầu giữ xác nhận thu đủ, thiếu/thừa tạo exception riêng. |
| D05 | Hoàn tiền thủ công có audit trước; chỉ xác nhận Succeeded sau bằng chứng tiền ra. Tiền hoàn thành công cộng tiền đang giữ chỗ cho các refund Pending không vượt tiền đã thu còn được phép hoàn. | Không hứa đã hoàn hoặc đã có queue provider khi mới đặt RefundPending. Chưa tự thêm protocol refund VNPay. |
| D06 | Deadline mới: VNPay 30 phút, chuyển khoản 24 giờ từ nhận đơn, cấu hình được và snapshot trên đơn/attempt. COD không auto-expire vì Unpaid. | Đây là hạn chấp nhận local, không khẳng định là TTL provider. Không reset deadline khi retry; không backfill deadline giả cho đơn cũ. |
| D07 | Hết hạn chỉ tự hủy đơn Pending/Confirmed chưa bàn giao và chưa có khoản thu được xác minh. Callback cạnh expiry được serialize; success sau expiry thành ngoại lệ cần hoàn. | Có thể giải phóng hàng mà không hồi sinh đơn. Mạng lỗi/không rõ kết quả không được giả là provider thất bại. |
| D08 | Coupon reservation được release một lần khi hủy trước bàn giao/hết hạn; sau khi đã giao không tự hoàn lượt coupon. Loyalty voucher được mở lại nếu còn hiệu lực khi release; không vừa mở voucher vừa trả điểm đã đổi. | Chốt rõ promotion semantics; bản hết hạn không được gia hạn ngầm. Redemption cũ giữ lịch sử, chỉ thêm trạng thái release/reason. |
| D09 | Yêu cầu trả trong 7 ngày kể từ CompletedAt lần đầu, tính thời điểm server, được admin duyệt/từ chối. Quá hạn chỉ admin-assisted với lý do. Không coi đây là kết luận về thời hạn luật định. | Khớp chính sách shop đang hiển thị; yêu cầu chưa phải hàng đã về. Snapshot policy version cho case mới. |
| D10 | Return theo item/quantity. Request/approval không đổi Completed; chỉ sau khi toàn bộ số lượng đã được nhận/chấp thuận và xử lý hàng mới đổi Order sang Returned. Trả một phần thể hiện bằng return summary additive. | Không thêm enum OrderStatus làm vỡ client cũ chỉ để biểu diễn từng bước return. |
| D11 | Hoàn giá hàng thực trả theo net item allocation. Lỗi shop/toàn bộ đơn: hoàn phí giao gốc một lần; đổi ý: không hoàn phí giao đã thực hiện. Hủy chưa gửi hoàn toàn bộ tiền đã thu hợp lệ của đơn. | Phí trả hàng do shop chịu nếu lỗi shop; khoản phải thu khách cần thỏa thuận riêng, không sửa TotalAmount cũ. Không thu hồi discount của món khách giữ lại. |
| D12 | Hàng trả Saleable mới tăng tồn bán; Damaged/Quarantine không tăng. Giao thất bại hoàn về dùng biên nhận hàng hoàn carrier riêng, không giả thành customer return từ Completed. | Đúng custody và không đưa hàng lỗi trở lại bán. |
| D13 | Loyalty chỉ đảo phần điểm đã earn tương ứng hàng được chấp thuận trả; đảo idempotent. Điểm đã tiêu tạo nợ điểm không âm riêng, được bù bằng earn sau; không ép balance âm hoặc xóa ledger. | Giữ invariant balance hiện có; tier lifetime không tự downgrade. Không thu tiền từ khách để bù điểm. |
| D14 | Warranty chỉ xử lý các serialized unit thực nhận/chấp thuận; giữ audit và coverage snapshots, không xóa entitlement và không tự tạo bảo hành mới cho hàng hoàn. | Theo ADR 005; flag tắt vẫn không bị bật tự động bởi return. |
| D15 | Exchange v1 = return/refund và một đơn mua mới liên kết tham chiếu; không sửa đơn gốc hoặc triển khai engine đổi hàng/bù giá. | Tránh mở rộng phạm vi, giữ lịch sử và số tiền có thể đối chiếu. |
| D16 | Các quyền lợi tier chưa có tác dụng được ẩn/ghi rõ chưa áp dụng; không tự thêm stacking discount/freeship. Thông tin ngân hàng nhận tiền của shop được cấu hình server và hiển thị đầy đủ. | Không quảng bá quyền lợi chưa thực thi; giảm rủi ro tự đổi giá. |
| D17 | Đơn legacy/import không có dữ liệu chắc chắn đi theo luồng explicit manual-review. Không suy tiền đã thu, ngày giao, cân nặng hoặc phí cũ từ catalog hiện tại. | Reconciliation có audit trước backfill; tính tương thích là yêu cầu của từng task. |

### Tiền và làm tròn khi trả một phần

- Lưu net amount allocation cho item/unit trước khi cho phép partial return. Discount sản phẩm chỉ phân bổ vào item đủ điều kiện; tổng allocation bằng đúng snapshot discount.
- Dùng VND nguyên, phần dư phân theo largest remainder và tie-break ổn định theo OrderItemId rồi unit ordinal. Không chia độc lập rồi làm tròn khiến tổng refund vượt thực trả.
- Giữ nguyên subtotal/discount/total lịch sử; allocation mới tham chiếu snapshot gốc và version thuật toán.
- Ví dụ test: goods 100.000đ + 200.000đ, discount 10.000đ => net 96.667đ + 193.333đ; tổng 290.000đ. Fixture tie-break phải có ID cố định.
- Hoàn gộp nhiều lần tính trên cumulative returned quantity; không cộng sai sai số làm tròn. Shipping refund có unique source để không hoàn lặp.
- Khoản thu trùng vượt TotalAmount được hoàn từ chính capture dư, không cộng thành quyền mua hàng/refund của các item.

### Các lựa chọn triển khai và lý do chọn

1. **Chọn sửa theo vertical slice trong kiến trúc hiện tại:** dễ chứng minh từng invariant, dùng lại transaction/outbox và bàn giao từng PR.
2. **Không chọn viết lại order/payment/kho đồng loạt:** làm tăng rủi ro migration, snapshots và regression trong lúc lỗi hàng/tiền chưa được chặn.
3. **Không chọn tự động hóa provider hoàn/đối soát ngay:** tài khoản, quyền API và protocol cần xác minh riêng; manual audited workflow đủ cho shop nhỏ và có thể chạy trước.

## 4. Lộ trình, phụ thuộc và gate

| Phase | Mục tiêu | Task | Ưu tiên | Điều kiện ra phase |
| --- | --- | --- | --- | --- |
| 0 | Baseline, tái hiện và chốt contract | EC-00, EC-01 | P0 | Bằng chứng reproducible, test DB dùng được, quyết định mới được ghi tường minh |
| 1 | Sửa tính nhất quán hàng/tiền/vận chuyển | EC-10 đến EC-19 | P0 | Các race/regression tương ứng đạt; không còn đường ghi vượt guard |
| 2 | Phục hồi và vận hành thu/hoàn cơ bản | EC-20 đến EC-25 | P0/P1 | Có biến động kho, hoàn thủ công, xử lý treo, khách chuyển khoản được |
| 3 | Trả hàng có kiểm soát và hậu mãi | EC-30 đến EC-33 | P1 | Return/refund/stock/loyalty/warranty được đối soát toàn vòng |
| 4 | Quản trị shop, báo cáo và lời hứa với khách | EC-40 đến EC-43 | P1/P2 | Số liệu và thao tác admin giải thích được hàng/tiền từng đơn |
| 5 | Diễn tập và mở bán có kiểm soát | EC-50, EC-51 | P0 release | UAT, migration và production gates hiện có có bằng chứng |

Luồng chính: EC-00 → EC-01 → Phase 1 → Phase 2 → Phase 3 → Phase 5. Phase 4 có thể tiến theo phụ thuộc riêng, nhưng các mục P1 của phase này phải xong trước mở bán rộng.

Các nhánh có thể làm độc lập sau EC-01: EC-12/13/14 về tồn, EC-15/16 về tracking, EC-17 về kiện hàng. Không cho hai task cùng sửa file lớn hoặc chạy migration test trên cùng DB đồng thời.

**Controlled pilot:** chỉ sau Phase 1 + Phase 2, có quy trình hỗ trợ trả hàng thủ công và không để nút RequestReturn cũ đổi thẳng trạng thái; giới hạn traffic theo khả năng chủ shop xử lý.

**Mở bán rộng:** Phase 3 và các task P1 của Phase 4 hoàn tất, không còn P0 mở, EC-50/51 đạt. Tắt chức năng chưa sẵn sàng thay vì hiển thị giả.

## 5. Các task triển khai

Mọi task đi theo vòng: viết regression có assertion cụ thể → chạy xác nhận FAIL đúng nguyên nhân → thay đổi nhỏ nhất → chạy kiểm tra tương ứng → review diff và cập nhật bằng chứng. Không commit kèm thay đổi chưa thuộc task; chỉ commit khi workflow thực thi cho phép.

Các mã V-APP/V-INFRA/V-API/V-FE/V-E2E/V-MIG ở phần 6 là lệnh dùng để nghiệm thu; tên class/test mới dưới đây là tên đề xuất cần tạo.

### Phase 0 — Chuẩn bị và khóa quyết định

### [ ] EC-00 — Tái hiện phát hiện và lưu baseline hiện tại

- **Ưu tiên:** P0. **Phụ thuộc:** không.
- **Files:** đọc source/tests ở bảng F01–F17; cập nhật evidence vào phần 7 tài liệu này. Không chỉnh source để làm test xanh.
- **Interfaces:** consumes source hiện tại; produces bảng Confirmed/Not reproduced/Blocked với command, commit và working-tree scope.
- [ ] Kiểm tra git status, SDK và Docker; chạy lại V-APP/V-INFRA/V-API khi môi trường sẵn sàng. Baseline phiên cũ không thay thế kết quả mới.
- [ ] Tạo regression DB có barrier/provider gate cho F04/F05/F06/F10 và fixture cho F01/F02/F09; mỗi test assert cả order, tiền, stock, shipment và số history cần thiết.
- [ ] Với phát hiện không tái hiện, ghi chứng cứ guard/caller đang bảo vệ rồi điều chỉnh task tương ứng; không sửa dựa trên giả thuyết đã bị bác bỏ.
- **Nghiệm thu:** test thất bại do hành vi đích, không do Docker/DI/fixture. Mọi Blocked được nêu rõ và không đánh dấu Done.

### [ ] EC-01 — Ghi đặc tả consistency, contract và cách nâng cấp

- **Ưu tiên:** P0. **Phụ thuộc:** EC-00.
- **Files:** tạo ADR số kế tiếp tại thời điểm triển khai; cập nhật docs/backend/product/business-rules.md, docs/backend/architecture/data-and-consistency.md, docs/backend/development/known-limitations.md và shipping docs khi có thay đổi liên quan.
- **Interfaces:** produces state matrix cancel/payment/custody/return và route/type additive cho các task sau; D01–D17 là input thiết kế.
- [ ] Chốt ma trận khóa theo từng use case. Payment/cancel dùng payment rows → order → shipment → variants có thứ tự; checkout giữ protocol quote/cart → variants → coupon hiện có. Luồng chỉ đọc summary không lấy thêm khóa ngược chiều. Return/warranty không lấy order trước unit nếu sẽ gọi activation path.
- [ ] Ghi rõ thứ tự khóa cho từng luồng mới và chứng minh không tạo chu trình với activation unit → entitlement → order hiện tại; không áp một thứ tự giả cho toàn hệ thống.
- [ ] Định nghĩa idempotency requestId + payload hash + unique constraint, expected revision và 400/401/403/404/409; actor/time do server cung cấp.
- [ ] Chốt policy version, rollout feature flags, hành vi legacy/import và compatibility của return endpoint cũ. Thiết kế route mới nằm dưới các family admin/customer hiện có.
- **Nghiệm thu:** bảng tình huống có kết quả hàng/tiền/trạng thái, không để TBD về các quyết định đã nêu. Chưa công bố ADR mới là Accepted thay owner nếu chưa có phê duyệt thực tế.

### Phase 1 — Sửa hàng, tiền và vận chuyển

### [ ] EC-10 — Một nghiệp vụ hủy có custody và side effects nhất quán

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01.
- **Files:** sửa AdminOrderService.cs, CustomerOrderService.cs, ShipmentWebhookService.cs, OrderShipmentService.cs, ShipmentOrderStatusTransitioner.cs trong Application; shipping read DTO/admin order panel và OrderShipment custody metadata nếu cần; tạo Modules/Ordering/OrderCancellationCoordinator.cs và integration test AdminOrders/OrderCancellationConsistencyTests.cs.
- **Interfaces:** tạo IOrderCancellationCoordinator.CancelLockedAsync(Order order, string reason, string actor, CancellationOrigin origin, CancellationToken ct) → Task<Result>. Caller mở transaction, khóa payment/order; coordinator không tự mở nested transaction. CancellationOrigin phân biệt Customer/Admin/Provider/Expiry.
- [ ] Regression ProviderCancellation_RestoresReservedStockAndOpensRefundOnce: đơn chưa bàn giao, Paid → Cancelled + RefundPending, stock +qty đúng một lần; duplicate webhook không tăng lần hai.
- [ ] Implement chung cho các entry point; provider Cancelled không đi qua ChangeStatus riêng rồi bỏ side effects. Không enqueue provider cancel từ chính callback đã xác nhận cancel.
- [ ] Test FailedDeliveryCancellation_DoesNotRestockBeforePhysicalReceipt: hàng đã bàn giao không tự trở lại sellable; bất đồng carrier/order mở exception.
- [ ] Căn cứ custody bằng local dispatch hoặc provider handover evidence đã lưu, không coi thiếu event là bằng chứng chưa bàn giao. Trường hợp unknown giữ hàng chưa được bán lại, giữ timeline và trả shipping summary RequiresReview/reason để admin xử lý; không xóa bằng chứng hoặc cho webhook retry vô hạn mà không có đường giải quyết.
- [ ] Chạy V-APP/V-API filter OrderCancellationConsistency và các test cancel/webhook cũ; kiểm tra reason/history/email và mọi rollback nguyên tử.
- **Nghiệm thu:** cùng tình huống cho cùng kết quả hàng/tiền dù gọi từ entry point nào; không hồi tồn khi hàng chưa về.

### [ ] EC-11 — Khoản VNPay đến muộn và thanh toán trùng không bị mất dấu

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01, phối hợp EC-10.
- **Files:** sửa PaymentService.cs, PaymentTransaction.cs và mappings; tạo Domain/Modules/Payments/PaymentCapture.cs, PaymentReconciliationCase.cs; create persistence configurations/migration; test Payments/LatePaymentIntegrationTests.cs.
- **Interfaces:** PaymentCapture là khoản thu đã được provider xác minh, khác trạng thái xử lý local attempt. Unique (provider, gatewayTransactionNo); callback lặp cùng payload không tạo capture/case mới; thiếu định danh bắt buộc tạo lỗi protocol không ghi nhận giả.
- [ ] Regression CancelledOrder_LateSuccessfulIpn_RecordsCaptureWithoutFulfillment: giữ Cancelled, lưu capture/case đúng một lần, không trừ stock hoặc tạo shipment; invalid signature/amount không được ghi tiền.
- [ ] Xét outcome callback hợp lệ trước khi trả sớm vì local attempt terminal. Thành công trái với outcome trước được giữ thành dữ kiện cần đối soát, không xóa evidence cũ.
- [ ] Test TwoAttemptsBothPaid_CreateOneOrderAndOneExcessPaymentCase; callback fail đến sau success không xóa capture. Persist dữ liệu tài chính và exception atomically.
- [ ] Chạy V-INFRA filter VNPay, V-API filter LatePaymentIntegrationTests và PaymentIntegrationTests; V-MIG cho uniqueness/upgrade.
- **Nghiệm thu:** mọi khoản thu hợp lệ có đường tới đối soát/refund; không sửa protocol IPN/return khi chưa kiểm chứng contract provider; response acknowledgement chỉ sau durable save.

### [ ] EC-12 — Sửa SKU không ghi đè tồn từ form cũ

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01.
- **Files:** AdminProductVariantService.cs, UpdateProductVariantRequest.cs, ProductVariant.cs/configuration; frontend api-types/admin.ts, VariantModal.tsx, variant hooks; test AdminProducts/VariantStockConcurrencyTests.cs.
- **Interfaces:** thêm Revision theo optimistic concurrency và ExpectedRevision additive. Tạo AdjustVariantStockRequest(RequestId, ExpectedRevision, Delta, Reason) và endpoint admin products/variants/{id}/stock-adjustments; không nhận actor từ body. Lưu bản ghi StockAdjustment tối thiểu để requestId/reason có durability; EC-20 tham chiếu bản ghi này, không ghi nhận thêm một lần tăng/giảm.
- [ ] Regression StaleVariantEdit_DoesNotRestoreSoldStock: mở form tồn 10, checkout còn 9, save form cũ → 409 hoặc update metadata không chạm tồn; không được trở lại 10.
- [ ] Tách metadata/pricing update khỏi stock adjustment. Trong thời gian tương thích, payload legacy muốn đổi stock phải bị từ chối rõ hoặc đi qua đường reviewed adjustment; không âm thầm bỏ một yêu cầu nhập kho của admin.
- [ ] UI dùng expected revision, hiển thị conflict yêu cầu reload, điều chỉnh tồn cần lý do. Bảo vệ requestId lặp và số tồn không âm ở DB/domain.
- [ ] Chạy V-API filter VariantStockConcurrencyTests, V-FE và V-E2E cho form conflict/adjustment. V-MIG nếu thêm revision.
- **Nghiệm thu:** sửa tên/giá không đổi tồn; tồn chỉ đổi qua thao tác có căn cứ, đối chiếu được.

### [ ] EC-13 — Legacy VNPay kiểm tra tồn mới sau khóa

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01.
- **Files:** CheckoutService.cs, CheckoutCartBuilder.cs, CheckoutOrderPlacer.cs, AppDbContext.cs; test CartCheckout/LegacyVNPayStockConcurrencyTests.cs.
- **Interfaces:** giữ ICheckoutStore và public checkout contract; preview read không tracking hoặc refresh tất cả variant sau khóa theo thứ tự ổn định.
- [ ] Test TwoVnPayCheckouts_LastUnit_OnlyOneOrderAccepted với hai DbContext và provider quote barrier; assert một accepted, một conflict, một order và tồn 0.
- [ ] Sửa điểm đọc sớm để không làm locked revalidation dùng entity cũ; giữ UnitPriceSnapshot và không kéo HTTP vào transaction.
- [ ] Test catalog deactivated/weight changed trong lúc báo phí được từ chối hoặc xử lý theo snapshot policy rõ ràng, không dùng dữ liệu stale mặc nhiên.
- [ ] Chạy V-API filter LegacyVNPayStockConcurrencyTests và CartCheckoutQueryBudgetIntegrationTests; V-APP checkout tests.
- **Nghiệm thu:** chứng minh bằng PostgreSQL interleaving; không chỉ thêm Assert vào fake có chung object.

### [ ] EC-14 — Import đơn kiểm tra và trừ tồn trong transaction

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01.
- **Files:** AdminOrderService.cs, OrderImportFileParser.cs khi cần, tests/AdminOrders/OrderImportConcurrencyTests.cs.
- **Interfaces:** preview chỉ là dự kiến; commit khóa/reload toàn bộ SKU cần dùng rồi revalidate. Giữ LegacyImport, không tạo WorkspaceEC selection hoặc sửa giá/địa chỉ đơn cũ.
- [ ] Test ImportVsCheckout_LastUnit_NoOversell và ImportMultipleRows_SameSku_UsesAggregateDemand; thất bại không tạo một phần file.
- [ ] Implement thứ tự khóa stable và bounded query; không dùng tracked variant của plan ngoài transaction để quyết định stock.
- [ ] Test preview hợp lệ nhưng commit thiếu tồn → conflict với lỗi có thể xử lý; file trùng SKU hợp lệ vẫn gom nhu cầu đúng.
- [ ] Chạy V-API filter OrderImportConcurrencyTests và AdminOrderIntegrationTests; V-APP import tests.
- **Nghiệm thu:** all-or-nothing, tồn đủ tại thời điểm commit; không làm import trở thành checkout shipping mới.

### [ ] EC-15 — Refresh provider không ghi đè hủy/webhook mới

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01, EC-10.
- **Files:** OrderShipmentService.cs, OrderShipment.cs/configuration và focused shipping store; test Shipments/ShipmentRefreshConcurrencyTests.cs.
- **Interfaces:** dùng/extend reload-lock persistence có sẵn; apply provider response chỉ nhận order/shipment authoritative trong transaction sau HTTP.
- [ ] Test RefreshPaused_CancelCommits_DoesNotResurrectOrder và RefreshPaused_NewerDeliveredWebhook_DoesNotRegressShipment; assert stock/history/loyalty không bị lặp.
- [ ] Reload dưới khóa; áp event timestamp, terminal guards và concurrency revision nhất quán cho mọi provider writer. Không dùng timestamp lúc refresh để đánh bại sự kiện provider mới hơn.
- [ ] Test response không có timeline xử lý theo chính sách timestamp rõ ràng; duplicate/equal timestamps không ghi history mâu thuẫn.
- [ ] Chạy V-API filter ShipmentRefreshConcurrencyTests và ShipmentWebhookIntegrationTests; V-APP shipment tests.
- **Nghiệm thu:** HTTP nằm ngoài khóa, state update và evidence nằm trong transaction; không hồi sinh Cancelled/Returned.

### [ ] EC-16 — Giao lại MiniLogistics và hoàn tất COD đúng điều kiện

- **Ưu tiên:** P0. **Phụ thuộc:** EC-15.
- **Files:** ShipmentOrderStatusTransitioner.cs, ShipmentWebhookService.cs, FulfillmentCoordinator.cs; test Shipments/ShipmentRedeliveryIntegrationTests.cs.
- **Interfaces:** tái dùng Order.ChangeStatus; FailedDelivery → Shipping → Completed theo sự kiện hợp lệ; không đổi enum values.
- [ ] Test DeliveryFailed_ThenDeliveringThenDelivered_CompletesOnce; test provider nhảy thẳng FailedDelivery → Delivered vẫn xử lý đủ history hợp lệ.
- [ ] Giữ guard COD: Delivered chưa thu đủ vẫn chưa Completed; collect sau đó mới hoàn tất. Test collect trước/sau Delivered và duplicate webhook.
- [ ] Chạy V-APP/V-API filter ShipmentRedeliveryIntegrationTests và ShippingOrderPaymentTests; kiểm tra loyalty một lần.
- **Nghiệm thu:** shipment/order không lệch sau giao lại; giữ WorkspaceEC retry đang có.

### [ ] EC-17 — Quản trị cân nặng/kích thước SKU xuyên suốt

- **Ưu tiên:** P0 nếu mở WorkspaceEC; P1 nếu phương thức này tắt. **Phụ thuộc:** EC-01, phối hợp EC-12.
- **Files:** create/update variant requests/validators/DTO/service, VariantModal.tsx, productTypes.ts, api-types/admin.ts; test AdminProducts/VariantParcelReadinessTests.cs.
- **Interfaces:** WeightKg, LengthCm, WidthCm, HeightCm nullable cho legacy; WeightKg phải >0 để WorkspaceEC available. Field không gửi không được xóa dữ liệu đang có trong rollout tương thích.
- [ ] Test AdminCreateVariant_SetActualWeight_WorkspaceQuoteSucceeds; missing/nonpositive/overflow rejected đúng nơi, không thay bằng cân giả.
- [ ] Bổ sung form đơn vị rõ, validation backend, read DTO và hành động sửa từ danh sách weight readiness; không bắt kích thước ngoài phạm vi calculator v1.
- [ ] Chạy V-API filter VariantParcelReadinessTests, V-FE, V-E2E tạo SKU/quote. Test sửa cân nặng không đổi snapshot đơn đã đặt.
- **Nghiệm thu:** sản phẩm mới có thể bán/giao từ UI bình thường, không cần seed hoặc SQL sửa tay.

### [ ] EC-18 — Cho phép nộp COD đã thu sau hủy/trả

- **Ưu tiên:** P0. **Phụ thuộc:** EC-01.
- **Files:** OrderPaymentService.cs, OrderPaymentModels.cs, OrderPaymentPanel.tsx; test Shipments/CodRemittanceLifecycleTests.cs.
- **Interfaces:** giữ ConfirmAsync và receipt kinds hiện có; remittance dựa vào collection còn chưa nộp, độc lập open/closed Order. Không mở lại khả năng thu tiền mua hàng mới cho đơn đã hủy.
- [ ] Test CollectedThenReturned_RemittanceStillAllowed và CollectedThenCancelled_RemittanceStillAllowed; đúng một receipt, giữ trạng thái đơn/refund.
- [ ] Tách CanRemitCod khỏi guard chung open-order; reference trùng ở đơn khác/conflicting payload phải conflict.
- [ ] Chạy V-API filter CodRemittanceLifecycleTests và ShippingOrderPaymentTests; V-FE/V-E2E panel sau return/cancel.
- **Nghiệm thu:** collection/remittance/refund là ba sự kiện phân biệt, báo cáo không đếm tiền hai lần.

### [ ] EC-19 — Coupon admin update không ghi đè bộ đếm checkout

- **Ưu tiên:** P0 cho coupon đang bật. **Phụ thuộc:** EC-01.
- **Files:** AdminCouponService.cs, coupon mappings và focused locking method; test AdminCoupons/CouponUsageConcurrencyTests.cs.
- **Interfaces:** metadata/status update không ghi UsedCount cũ; khóa hoặc concurrency token phối hợp với checkout, giữ usage limit invariant.
- [ ] Test AdminEditVsCheckout_UsedCountNotLost và LowerLimitVsRedemption_NeverExceedsLimit; independent scopes có barrier.
- [ ] Sửa update payload/persistence đúng field hoặc reload có khóa; không sửa bằng recount mỗi request.
- [ ] Chạy V-API filter CouponUsageConcurrencyTests và AdminCouponIntegrationTests; V-APP coupon tests.
- **Nghiệm thu:** bật/tắt/sửa coupon không mở lại lượt đã dùng hoặc làm mất redemption.

### Phase 2 — Phục hồi và vận hành thu/hoàn cơ bản

### [ ] EC-20 — Sổ biến động kho và nhận hàng hoàn carrier

- **Ưu tiên:** P1, prerequisite cho expiry/return đầy đủ. **Phụ thuộc:** EC-10, EC-12, EC-13, EC-14.
- **Files:** tạo Catalog/Stock/StockMovement.cs trong Domain và use case/DTO trong Application, configurations/migration; sửa điểm ghi tồn trong checkout/cancel/import/adjustment; admin stock history và carrier-return receipt.
- **Interfaces:** AppendStockMovementLockedAsync(variantId, deltaSellable, sourceType, sourceId, actor, reason, occurredAt, ct) → Task; unique source action per variant. Caller sở hữu transaction/lock. Đây là sổ stock, không phải generic event bus.
- [ ] Test CheckoutCancelAndReplay_HaveBalancedMovements và CarrierReturn_DamagedDoesNotIncreaseSellable; receipt reference/requestId lặp không cộng kho lần hai.
- [ ] Tạo baseline OpeningBalance tại cutover có timestamp và số thực tế hiện tại; không dựng lịch sử giả từ đơn cũ hoặc tính lại stock bằng cách trừ toàn bộ đơn lịch sử.
- [ ] Nhận hàng hoàn carrier có quantity, Saleable/Damaged/Quarantine và audit; chỉ Saleable tăng sellable; không tạo customer ReturnRequest giả.
- [ ] Chạy V-API filter StockMovementIntegrationTests, V-MIG clean/upgrade và V-FE/V-E2E stock history/receive.
- **Nghiệm thu:** stock hiện tại giải thích được bằng opening + movements sau cutover, mọi writer tham gia cùng transaction.

### [ ] EC-21 — Refund có số tiền, trạng thái và bằng chứng hoàn thủ công

- **Ưu tiên:** P0 trước pilot. **Phụ thuộc:** EC-11, EC-18.
- **Files:** tạo Domain/Modules/Payments/OrderRefund.cs; Application/Modules/Payments/OrderRefundService.cs và RefundModels.cs; controller admin OrderRefundsController.cs; configurations/migration; tests/Payments/RefundIntegrationTests.cs.
- **Interfaces:** IOrderRefundService.RequestAsync(Guid orderId, RequestRefundRequest request, string actor, CancellationToken ct) → Task<Result<RefundDto>>; ConfirmAsync(Guid refundId, ConfirmRefundRequest request, string actor, CancellationToken ct) → Task<Result<RefundDto>>. RequestRefundRequest có RequestId, Amount, Currency, Reason, SourcePaymentId và ReturnId nullable; ConfirmRefundRequest có RequestId, ExpectedRevision, Reference, ExecutedAtUtc, Reason và evidence metadata, không nhận secret ngân hàng. Tiền được phép hoàn gồm capture VNPay và receipt chuyển khoản/COD đã xác minh; COD remittance không tạo thêm hạn mức hoàn.
- [ ] Test TwoConcurrentRefundRequests_DoNotOverReserveCapturedMoney; retry cùng request trả cùng kết quả, khác payload conflict; zero/âm/quá thực thu bị chặn.
- [ ] Model Requested → Processing → Succeeded/Failed; Failed chỉ retry theo thao tác có audit. Outstanding gồm refund đang giữ chỗ; Succeeded không sửa/xóa, nhập nhầm đi bằng correction có review.
- [ ] PaymentStatus chỉ append Refunded/PartiallyRefunded khi cần và cập nhật mọi mapping/formatter; financial summary vẫn là nguồn chi tiết, không ép một enum mô tả mọi exception.
- [ ] Test CancelPaidOrder_ManualRefundCompletesWithReference và duplicate confirm; giữ nguyên Order.TotalAmount, không enqueue shipment.
- [ ] Chạy V-API filter RefundIntegrationTests, V-INFRA mappings và V-MIG. Provider refund tự động không nằm trong task.
- **Nghiệm thu:** shop trả lời được đã hoàn bao nhiêu, còn nợ bao nhiêu, lúc nào, ai xác nhận, chứng từ nào.

### [ ] EC-22 — Admin xử lý ngoại lệ tiền và hoàn tiền

- **Ưu tiên:** P0 trước pilot. **Phụ thuộc:** EC-11, EC-18, EC-21.
- **Files:** tạo pages/payments/PaymentExceptionsPage.tsx và RefundPanel.tsx trong admin; admin router/navigation, API adapter, api-types/payments.ts; read API trong Payments có paging.
- **Interfaces:** GET admin payment exceptions, POST resolve có expected revision/reason; dùng refund service EC-21, không cho frontend gán Paid/Refunded trực tiếp.
- [ ] UI/contract tests: late paid cancelled order hiện exception; chỉ đóng case khi có refund succeeded hoặc quyết định hợp lệ có lý do, không bấm Closed để xóa nghĩa vụ còn thiếu.
- [ ] Hiển thị tiền thu, COD chờ nộp, tiền hoàn/đang hoàn, outstanding theo từng đơn/capture; amount partial/excess không sửa committed total.
- [ ] Điều chỉnh email hủy: chỉ nói đang chờ xử lý hoàn khi mới RefundPending; thông báo đã hoàn chỉ sau Succeeded; enqueue durable trong transaction.
- [ ] Trước pilot mà EC-30 chưa bật: khóa luồng legacy self-service return đổi thẳng Returned bằng guard phía backend, trả conflict có hướng dẫn liên hệ hỗ trợ và ẩn nút mutation trên UI; admin vẫn xử lý case hỗ trợ có audit. Test LegacyReturnDisabled_DoesNotChangeOrder. EC-30 thay guard bằng adapter mới khi đủ điều kiện.
- [ ] Chạy V-API filter PaymentExceptionIntegrationTests, V-FE và V-E2E late payment → refund → resolved.
- **Nghiệm thu:** không phải sửa DB để xử lý các khoản lệch; có filter overdue và audit ai làm gì.

### [ ] EC-23 — Deadline thanh toán, hết hạn đơn và release coupon

- **Ưu tiên:** P1. **Phụ thuộc:** EC-10, EC-11, EC-19, EC-20, EC-21.
- **Files:** Order/PaymentTransaction additions, CheckoutOrderFactory/placer/QuotedCheckoutService; tạo UnpaidOrderExpiryService/Worker và focused persistence claim; CouponRedemption.cs; migration/config validator/runbook.
- **Interfaces:** PaymentDueAtUtc snapshot nullable; ExpireDueOrdersAsync(int batchSize, CancellationToken ct) → Task<int>. Dùng claim/lease hoặc SKIP LOCKED phù hợp worker hiện có; local attempt hết hạn không phủ nhận provider capture muộn.
- [ ] Test AdvanceClockPastDeadline_ExpiresUnpaidOrderOnce: stock và coupon reservation release một lần; COD Unpaid không tự hủy; legacy thiếu deadline không tự backfill/hủy.
- [ ] Thêm config VNPay 30 phút/manual transfer 24 giờ, validate positive bounded duration; không reset khi retry. Quote TTL 10 phút vẫn là một loại hạn khác.
- [ ] Test SuccessCallbackVsExpiry_ExactlyOneCommercialOutcome và LateSuccessAfterExpiry_OpensRefundCase; giữ đúng locks và không gọi provider trong transaction.
- [ ] Thêm trạng thái release trên redemption; cancel trước bàn giao và expiry dùng chung rule D08. Loyalty voucher còn hạn được dùng lại, không phát thêm điểm/voucher.
- [ ] Chạy V-API filter UnpaidOrderExpiryIntegrationTests, V-APP TimeProvider tests, V-INFRA worker/config tests và V-MIG.
- **Nghiệm thu:** hàng không bị giữ vô hạn; uncertainty của provider hiện trong exception, không giả success/failure; có batch/replay/monitoring.

### [ ] EC-24 — Thanh toán lại trên cùng đơn trước deadline

- **Ưu tiên:** P1. **Phụ thuộc:** EC-11, EC-23.
- **Files:** PaymentService/payment DTOs/controller, PaymentTransaction configuration, QuotedCheckoutService.ResponseAsync và nhánh accepted-quote replay; storefront PaymentResultPage/OrderLookupPage/account order; api-types/payments.ts.
- **Interfaces:** RetryPaymentAsync(Guid orderId, RetryPaymentRequest request, CancellationToken ct) → Task<Result<PaymentRetryDto>>. Ownership hoặc guest proof theo pattern hiện có; không public chỉ bằng orderId/code.
- [ ] Test RetryFailedAttempt_CreatesOneNewAttemptWithoutNewOrderOrStockDecrease; chỉ retry khi failure/cancel của attempt đã rõ và đơn còn hạn, còn eligible.
- [ ] Không tạo attempt mới khi attempt trước Pending/unknown; trả status và hướng xử lý. Không tự đổi payment method/giá/shipping selection; accepted-checkout replay vẫn trả cùng đơn.
- [ ] Test ConcurrentRetries_CreateOneActiveAttempt; old attempt paid cạnh new attempt success tạo excess capture/case, chỉ một fulfillment. Không gia hạn local deadline vô hạn.
- [ ] Sửa accepted-quote replay đang dùng SingleOrDefault theo OrderId để hỗ trợ nhiều attempts: chọn active/latest attempt theo thứ tự rõ ràng, giữ lịch sử đầy đủ. Thêm DB guard một active attempt/order và test CheckoutReplayAfterRetry_ReturnsSameOrderWithoutSingleRowException.
- [ ] Chạy V-API filter PaymentRetryIntegrationTests, V-INFRA URL/signing tests hiện có, V-FE/V-E2E fail → retry.
- **Nghiệm thu:** khách không cần tạo đơn thứ hai khi lần thanh toán trước đã thất bại rõ; không tạo thu trùng không kiểm soát.

### [ ] EC-25 — Hướng dẫn chuyển khoản thực sự dùng được

- **Ưu tiên:** P1. **Phụ thuộc:** EC-01; tích hợp deadline khi EC-23 có.
- **Files:** cấu hình public receiving-bank DTO/validator trong Payments; CheckoutSuccessPage.tsx, CheckoutPage.tsx, manualTransfer.ts, order details và admin payment panel.
- **Interfaces:** public bank info do server cung cấp: bank display name, account number, account holder, reference format, PaymentDueAtUtc; không nhận tài khoản thụ hưởng từ client. QR chỉ bổ sung khi có chuẩn/nguồn được xác minh, không bắt buộc v1.
- [ ] Test ManualTransferCheckout_ReturnsConfiguredReceivingDetailsAndExactAmount; thiếu config phải không quảng bá phương thức như đã sẵn sàng, không lấy tài khoản ví dụ làm production.
- [ ] Hiển thị ngân hàng/chủ tài khoản/số tài khoản/nội dung/số tiền/hạn; copy và trạng thái chờ xác minh. Không cho khách tự bấm đã chuyển để MarkPaid.
- [ ] Chạy V-APP/V-INFRA config, V-API public DTO ownership, V-FE/V-E2E hướng dẫn chuyển khoản.
- **Nghiệm thu:** khách hoàn thành được chuyển khoản chỉ với thông tin hệ thống cung cấp; tài liệu vận hành nêu cách xử lý thiếu/thừa/sai reference.

### Phase 3 — Trả hàng và hậu mãi

### [ ] EC-30 — ReturnRequest, item quantities và quyền yêu cầu/duyệt

- **Ưu tiên:** P1 trước mở bán rộng. **Phụ thuộc:** EC-01, EC-21, EC-22.
- **Files:** tạo Domain/Modules/Ordering/Returns/ReturnRequest.cs, ReturnItem.cs; Application/Modules/Ordering/Returns/OrderReturnService.cs, ReturnModels.cs; controllers admin/customer; sửa CustomerOrderService.RequestReturnAsync và frontend account/admin order panels; migration.
- **Interfaces:** IOrderReturnService.RequestAsync(Guid orderId, CreateReturnRequest request, CancellationToken ct) → Task<Result<ReturnDto>>; ReviewAsync(Guid returnId, ReviewReturnRequest request, string actor, CancellationToken ct) → Task<Result<ReturnDto>>. CreateReturnRequest có RequestId, Items gồm OrderItemId/Quantity, ReasonCode/Reason; ReviewReturnRequest có RequestId, ExpectedRevision, Decision và Reason. Actor server-owned; không tin số tiền hoặc ngày mua do client gửi.
- [ ] Test CustomerRequest_WithinWindow_CreatesRequestedCaseWithoutChangingOrder; quá 7 ngày chỉ admin exception có reason; customer khác nhận 404.
- [ ] State Requested → Approved/Rejected/Withdrawn; Approved → Received → Resolved. Cộng số lượng các case pending/approved/received để ngăn hai request cùng trả vượt số đã mua.
- [ ] Thêm return summary additive trong order DTO. Legacy RequestReturn endpoint thành adapter tạo request toàn bộ phần còn đủ điều kiện, trả current order + summary; rollout frontend cùng lúc, khóa đường đổi thẳng Returned. Duplicate legacy call trả cùng open case hoặc conflict rõ.
- [ ] Chạy V-API filter ReturnRequestIntegrationTests, V-MIG và V-FE/V-E2E request/review/ownership.
- **Nghiệm thu:** bấm yêu cầu không có nghĩa hàng đã về, đã nhập kho hoặc đã hoàn tiền; khách/admin theo dõi được case.

### [ ] EC-31 — Nhận/kiểm hàng trả và tính tiền hoàn một phần

- **Ưu tiên:** P1. **Phụ thuộc:** EC-20, EC-21, EC-30.
- **Files:** OrderReturnService và DTOs, order refund allocation model/configuration, admin ReturnInspectionPanel.tsx; integration Returns/ReturnInspectionIntegrationTests.cs.
- **Interfaces:** ReceiveAsync(Guid returnId, ReceiveReturnRequest request, string actor, CancellationToken ct) → Task<Result<ReturnDto>>. Request có nhận thực tế theo item/unit, disposition Saleable/Damaged/Quarantine, expected revision/requestId; tiền refund tính server.
- [ ] Test PartialReturn_AllocatesDiscountInWholeVndAndNeverOverRefunds với ví dụ phần 3; nhận nhiều đợt và concurrent receive không vượt quantity/refund entitlement.
- [ ] Ghi stock movement Saleable trong cùng transaction; thiếu/hỏng/quarantine chưa tăng sellable. Refund Requested là output riêng, không gọi là hoàn xong.
- [ ] Test SellerFaultFullReturn_RefundsOriginalShippingOnce và ChangeOfMind_DoesNotRefundConsumedShipping; kiểm nhận sai serial và chênh số lượng có exception.
- [ ] Order Returned chỉ khi toàn bộ quantity được chấp thuận/nhận và disposition đã xử lý; partial giữ Completed + summary. Case Resolved chỉ khi nghĩa vụ stock/refund/hậu mãi hoàn tất hoặc kết quả không cần refund có căn cứ.
- [ ] Chạy V-API filter ReturnInspectionIntegrationTests, RefundIntegrationTests, V-MIG và V-FE/V-E2E.
- **Nghiệm thu:** từng món hàng và từng đồng hoàn có căn cứ, không đổi order totals hoặc dùng giá catalog mới.

### [ ] EC-32 — Đảo điểm earn khi trả hàng và xử lý điểm đã tiêu

- **Ưu tiên:** P1 nếu loyalty bật. **Phụ thuộc:** EC-31.
- **Files:** LoyaltyService.cs/ILoyaltyService.cs, CustomerLoyaltyAccount.cs, LoyaltyTransaction.cs/configuration, return coordinator và account UI.
- **Interfaces:** ReverseEarnForReturnAsync(Guid orderId, Guid returnId, decimal cumulativeReturnedNetGoods, CancellationToken ct) → Task<Result>; unique return/reversal source. Thêm ReversalDebtPoints không âm; ledger mới ghi rõ reversal/debt offset.
- [ ] Test ReturnAfterPointsRedeemed_CreatesDebtWithoutNegativeSpendableBalance; earn sau ưu tiên bù debt, không cho redeem phần chưa thực có.
- [ ] Snapshot MoneyPerPoint/rule tại earn mới; tính cumulative earn còn được giữ rồi đảo delta, không đảo quá original Earn. Legacy thiếu rule dùng ledger earn thực + allocation tỷ lệ có version, không dựng điểm từ config hiện tại.
- [ ] Test DuplicatePartialReturns_ReverseExactlyOnce; không hoàn điểm đã đổi voucher đồng thời mở lại voucher; giữ lifetime-tier không tự downgrade.
- [ ] Chạy V-APP/V-API filter LoyaltyReturnIntegrationTests và LoyaltyIntegrationTests; V-MIG, V-FE số dư/nợ điểm.
- **Nghiệm thu:** khách không giữ điểm earn của hàng đã hoàn, không xóa hoặc sửa ledger lịch sử.

### [ ] EC-33 — Warranty reconciliation trên unit thực trả

- **Ưu tiên:** P1 nếu bất kỳ warranty feature liên quan bật; nếu tắt ghi rõ Disabled, không giả Done. **Phụ thuộc:** EC-31, đọc ADR 005.
- **Files:** WarrantyEntitlement.cs, SerializedProductUnit.cs, WarrantyAuditAction.cs, Application/Warranties coordinator/service và return orchestration.
- **Interfaces:** ReconcileReturnedUnitsAsync(Guid returnId, IReadOnlyCollection<Guid> unitIds, string actor, CancellationToken ct) → Task<Result>. Chỉ nhận unit thuộc đúng order item/quantity, không nhận raw identifier để lưu.
- [ ] Test ReturnOneOfTwoSerializedUnits_ChangesOnlyReceivedUnit; giữ coverage/audit cũ, không void unit khách vẫn giữ.
- [ ] Tuân theo unit → entitlement → order khi cần cùng khóa với activation. Nếu dùng tác vụ deferred phải durable/idempotent, return case không Resolved khi hậu mãi còn treo; không dựa vào fire-and-forget.
- [ ] Test ActivationVsReturn_NoActiveWarrantyOnAcceptedReturnedUnit và duplicate reconciliation; guard flag/ownership/HMAC không thay đổi.
- [ ] Chạy V-API filter WarrantyReturnIntegrationTests và WarrantyIntegrationTests; V-INFRA mapping/privacy tests.
- **Nghiệm thu:** dữ liệu serial đã trả không còn quyền lợi sai, nhưng toàn bộ dấu vết mua/kích hoạt/hoàn vẫn tồn tại.

### Phase 4 — Quản trị, báo cáo và thông tin với khách

### [ ] EC-40 — Báo cáo tiền thu, tiền nộp, hoàn và khoản treo

- **Ưu tiên:** P1 cho tiền/ngoại lệ; P2 cho biểu đồ nâng cao. **Phụ thuộc:** EC-18, EC-21, EC-22, EC-31.
- **Files:** focused query trong Infrastructure/Persistence/Queries, admin dashboard DTO/service/controller, DashboardSections.tsx và trang payments; có paging/export có giới hạn.
- **Interfaces:** summary theo khoảng thời gian/ngày giao dịch và currency, tách Captured/Collected, COD awaiting remittance, Refunded, RefundPending và exceptions. Không đổi nghĩa metric CompletedRevenue cũ.
- [ ] Test Collect100_Remit100_Refund20_ReportDoesNotShow200Revenue: receipt bán hàng 100, internal remittance 100, refund 20 và net receipts 80; COD custody còn chờ được hiển thị riêng.
- [ ] Dùng sự kiện tài chính thay current OrderStatus để tính tiền; một đơn Returned chưa hoàn không được biến mất khỏi nghĩa vụ tiền.
- [ ] UTC storage, kỳ báo cáo theo Asia/Bangkok; test biên 23:59/00:00 và CSV totals khớp UI. Unknown provider fee vẫn unknown, không giả 0/lợi nhuận.
- [ ] Chạy V-API filter FinancialReconciliationIntegrationTests và AdminDashboardIntegrationTests; V-FE/V-E2E filters/export.
- **Nghiệm thu:** chủ shop đối chiếu được statement/carrier settlement và danh sách lệch; không gọi net receipts là lợi nhuận.

### [ ] EC-41 — Nhập kho, kiểm kê và điều chỉnh có audit

- **Ưu tiên:** P1 cho receiving/adjustment; P2 cho phân tích tồn. **Phụ thuộc:** EC-12, EC-20.
- **Files:** Catalog/Stock use cases/controller, admin pages/inventory, stock movement queries và api-types/admin.ts.
- **Interfaces:** ReceiveStockRequest(RequestId, VariantId, Quantity, Reference, Reason, ExpectedRevision); CountStockRequest có counted sellable/damaged và snapshot expected revision; server tính adjustment delta.
- [ ] Test StockCountVsCheckout_StaleCountRejected và ReceiveSameDocumentTwice_NoDoubleStock; chỉ nhận số nguyên không âm, điều chỉnh không vượt số có thể giảm.
- [ ] Tách nhập hàng, hàng lỗi/mất, kiểm kê và hàng hoàn; có lý do/chứng từ/actor/time. Chưa cần supplier directory/purchase-order engine hoặc nhiều kho.
- [ ] Chạy V-API filter InventoryOperationsIntegrationTests; V-FE/V-E2E receiving/count/history.
- **Nghiệm thu:** sửa tồn không còn là thay một con số không giải thích được.

### [ ] EC-42 — Quyền lợi loyalty hiển thị đúng khả năng thực thi

- **Ưu tiên:** P1 nếu đang hiển thị quyền lợi chưa có; P2 nếu phần này đã được ẩn. **Phụ thuộc:** EC-01; reversal từ EC-32 khi return được bật.
- **Files:** LoyaltyDtos.cs/LoyaltyService.cs, AccountPages.tsx, translations và loyalty docs.
- **Interfaces:** thêm capabilities/benefit eligibility additive; discountPercent/freeShipping metadata cũ không tự trở thành giảm tiền.
- [ ] Test UnimplementedTierBenefits_AreNotAdvertisedAsApplied; voucher points redemption đang hoạt động vẫn dùng đúng.
- [ ] Ẩn/ghi rõ phần quyền lợi chưa áp dụng; giữ tier lifetime. Không tự thêm cộng dồn coupon/tier/free ship hoặc thay giá quote đã chốt.
- [ ] Chạy V-APP loyalty DTO tests và V-FE/V-E2E account benefits.
- **Nghiệm thu:** người mua thấy quyền lợi khớp số tiền checkout; việc triển khai tier promotion mới cần task/spec riêng.

### [ ] EC-43 — Chính sách và thông báo đơn hàng khớp workflow thật

- **Ưu tiên:** P1. **Phụ thuộc:** EC-21, EC-25, EC-30, EC-31.
- **Files:** SupportPages.tsx, storefront translations/footer, checkout/customer/admin order services và email outbox hiện có; docs/runbook shop operations.
- **Interfaces:** policy version snapshot cho return mới; transactional email enqueue dùng CustomerEmailMessage/outbox, không thêm SMTP gọi trực tiếp.
- [ ] Test GuestCheckout_WithEmail_QueuesOneConfirmation và ReturnRequestAndRefund_EmailsDescribeActualState; retry không gửi trùng theo source key, không hứa đã hoàn trước Succeeded.
- [ ] Viết lại thông tin brand/liên hệ/bảo hành đang lẫn tên HyperWork theo thông tin shop cấu hình được xác thực; thiếu dữ liệu thật ghi chưa cấu hình, không tự chế hotline/address hay coi nội dung mẫu là đã được duyệt.
- [ ] Công khai thời hạn thanh toán, return 7 ngày của shop, cách tính phí hoàn và kênh hỗ trợ; chức năng chưa chạy phải ẩn hoặc ghi rõ. Guest không có email dùng mã đơn + possession proof và kênh hỗ trợ, không bắt tạo account.
- [ ] Chạy V-API filter OrderCommunicationIntegrationTests, V-INFRA email outbox, V-FE/V-E2E policy links/guest confirmation.
- **Nghiệm thu:** chính sách, UI và hành vi backend nhất quán; đối chiếu nghĩa vụ pháp lý theo thị trường bằng nguồn hiện hành ở release, không coi kế hoạch kỹ thuật là tư vấn pháp lý.

### Phase 5 — Diễn tập và mở bán

### [ ] EC-50 — E2E/UAT toàn vòng và migration/recovery

- **Ưu tiên:** P0 release. **Phụ thuộc:** Phase 1–3 và EC-40/41/42/43 phần P1; task có feature tắt phải được xác minh không lộ UI/endpoint ngoài phạm vi.
- **Files:** mở rộng API integration, frontend/e2e theo pattern/safety guards có sẵn; scripts migration/recovery/runbooks liên quan. Không đánh dấu UI fixture mock là E2E provider/DB thật.
- **Interfaces:** consumes candidate code/schema/config và toàn bộ contract từ các phase trước; produces bộ evidence test/UAT/migration/recovery gắn cùng candidate, không tạo public API mới.
- [ ] Diễn tập COD thu→giao→nộp; chuyển khoản→xác nhận→giao; VNPay success/fail/retry; cancel trước/sau capture; giao thất bại→giao lại/nhận hoàn; partial/full return→refund→đối soát.
- [ ] Chạy worker crash/replay, delayed/duplicate callback, expired lease, double click, 409 stale form và rollback DB; dùng requestId/correlation để chứng minh không double effect.
- [ ] V-MIG clean/upgrade trên dữ liệu legacy/import/order paid/outbox pending/serial activated; không backfill giả snapshots; rehearse backup/restore theo runbook hiện có.
- [ ] Chạy full backend build/test, frontend lint/test/typecheck/build, browser tests và contract/authorization/query budgets liên quan.
- **Nghiệm thu:** bằng chứng gắn candidate commit/diff và môi trường; FAIL/Blocked được ghi thật, không mở bán khi PG race hoặc migration gate chưa chạy.

### [ ] EC-51 — Cutover, tồn/tiền đầu kỳ và checklist chủ shop

- **Ưu tiên:** P0 release. **Phụ thuộc:** EC-50; tiếp tục tuân task/task.md production gates.
- **Files:** docs/backend/runbooks/production-release.md, docs/services/runbooks/workspace-shipping.md và runbook vận hành shop bổ sung.
- **Interfaces:** consumes evidence EC-50 và dữ liệu tồn/tiền được operator xác minh; produces cutover checklist, opening balances, exception ownership và quyết định enable/disable từng feature có audit.
- [ ] Dry-run read-only kiểm tra đơn legacy thiếu payment proof, RefundPending cũ, COD collected chưa nộp, lệch kho/đơn/vận đơn và dead letters; xuất exception, không bulk sửa database.
- [ ] Admin xác minh tồn đầu kỳ và tiền còn phải thu/hoàn bằng chứng thực tế; mọi correction có actor/reason/reference. Đơn không rõ giữ manual-review, không tự coi là Paid/Refunded.
- [ ] Deploy expand schema → compatible API/workers → frontend → cấu hình ngân hàng/bảng giá/cân nặng → enable từng feature. Disable-new-intake khi có sự cố; không rollback phá dữ liệu mới.
- [ ] Chủ shop chạy checklist mỗi ngày: đơn chờ xác nhận/tiền quá hạn, giao thất bại/hàng hoàn, COD chờ nộp, refund quá hạn, stock/quarantine, exception/dead-letter.
- **Nghiệm thu:** owner/operator xử lý được một ca bất thường qua admin; có người nhận alert, backup/restore và ngân sách hỗ trợ thật. Credentials/tariffs/bank account/brand thật vẫn cần người có thẩm quyền xác minh; không tự điền số giả.

## 6. Lệnh nghiệm thu và quy tắc bằng chứng

Chạy từ repository root, trừ frontend commands. Filter là tên class/test ghi trong từng task, không phải tên giả để skip test.

### V-APP / V-INFRA / V-API

Thay giá trị $ecomTestFilter bằng class/test đang triển khai:

~~~powershell
$ecomTestFilter = 'FullyQualifiedName~OrderCancellationConsistencyTests'
dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --no-restore --configuration Release --filter $ecomTestFilter
dotnet test tests/WorkspaceEcommerce.Infrastructure.Tests/WorkspaceEcommerce.Infrastructure.Tests.csproj --no-restore --configuration Release --filter $ecomTestFilter
dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --no-restore --configuration Release --filter $ecomTestFilter
~~~

Chỉ chạy project có test tương ứng và các regression cũ liên quan; output No test matches không phải PASS. V-API yêu cầu Docker/PostgreSQL Testcontainers hoạt động. Race dùng scope/DbContext độc lập và barrier điều khiển interleaving, không dùng sleep để cầu may.

### V-FE / V-E2E

Chạy trong frontend:

~~~powershell
corepack pnpm lint
corepack pnpm test
corepack pnpm typecheck
corepack pnpm build
corepack pnpm test:e2e:storefront
corepack pnpm test:ui:admin
~~~

Storefront Vitest không thay thế admin business tests. Script admin-ui hiện nằm trong working tree và có fixture; phải kiểm tra trạng thái thực tế khi task bắt đầu. Browser smoke với fixture chỉ chứng minh UI/transport; cần API integration/UAT riêng để chứng minh tiền/kho/provider.

### V-MIG / full release

~~~powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --configuration Release
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --configuration Release
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/WorkspaceEcommerce.Infrastructure --startup-project src/WorkspaceEcommerce.Api --context AppDbContext
./scripts/verify-prh-009-migrations.ps1
~~~

EF design time cần connection string/local settings hợp lệ và không commit. Mỗi migration mới phải review configuration/migration/snapshot, unique/FK/check/index, dữ liệu cũ và supported upgrade path. Không chạy delete volume hoặc sửa migration lịch sử để làm test xanh.

### Definition of Done cho một task

- Regression FAIL đúng hành vi trước sửa, PASS sau sửa; lưu command/result và giới hạn.
- Có xử lý duplicate, concurrency, ownership, provider failure, rollback và legacy nếu task chạm những phần đó.
- Backend/DTO/frontend/migration/docs thay đổi cùng behavior; không để UI hiển thị khả năng chưa chạy.
- Source/diff được review, không kéo theo file dirty không thuộc task hoặc secrets/local settings.
- Checkbox chỉ đổi sau khi có bằng chứng, chưa chạy không ghi Done; mọi Blocked có điều kiện mở chặn.

## 7. Nhật ký bằng chứng

| Ngày | Phạm vi | Kết quả | Giới hạn |
| --- | --- | --- | --- |
| 2026-11-05, phiên review trước | Application tests, Release, no-restore | 354 passed, 0 failed | Test hiện có; chưa thêm regression cho phát hiện mới |
| 2026-11-05, phiên review trước | Infrastructure tests, Release, no-build/no-restore | 241 passed, 0 failed | Không thay thế PostgreSQL/API race coverage |
| 2026-11-05, phiên review trước | Docker daemon | Blocked: không kết nối được Docker Desktop Linux engine | Chưa chạy API/PostgreSQL integration hoặc xác nhận race runtime |
| 2026-11-05, phiên lập kế hoạch | Đọc lại source/contracts/ADR, tạo kế hoạch | Chỉ planning; không sửa production code | Không chạy lại suite, không claim implementation hoặc production readiness |

Khi triển khai, thêm mỗi task một dòng: ID, candidate commit/diff, tests/commands, result, migration/UI/provider evidence và unresolved limitations.

## 8. Ranh giới cần dữ liệu thực và công việc để sau

- Có thể tự chốt thiết kế theo D01–D17; không cần dừng task chỉ để hỏi lại các lựa chọn kỹ thuật thường lệ đã được ủy quyền.
- Dữ liệu không thể suy luận: tài khoản nhận tiền thật, giá vận chuyển thật, thông tin shop, bằng chứng tiền/tồn cũ, credential/quyền API provider. Dùng cấu hình và checklist xác minh; không fabricate, không coi thời gian chờ là sự chấp thuận.
- Nếu tích hợp query/refund provider sau này, xác minh tài liệu chính thức, merchant permission, sandbox và retry/idempotency trước; không suy endpoint/protocol từ tên VNPay/MiniLogistics.
- Để sau: nhiều kho, split shipment, marketplace, wholesale pricing, loyalty promotion stacking, tự động refund provider, purchase-order/supplier đầy đủ, tax invoice integration và hệ kế toán. Có thể nâng ưu tiên khi shop thực sự cần, bằng task/spec riêng.
- Tiêu chí thành công: chủ shop giải thích được hàng ở đâu, tiền ở đâu và việc còn phải làm của từng đơn; xử lý ngoại lệ qua chức năng có audit, không sửa DB trực tiếp.
