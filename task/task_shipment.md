# Kế hoạch vận chuyển WorkspaceEC và đối tác

Ngày lập: 2026-10-29. Trạng thái: **đã hoàn tất SHIP-00–09; luồng mới mặc định tắt, SHIP-10–14 chưa hoàn tất**.

## 1. Yêu cầu đã xác nhận và phạm vi

- WorkspaceEC có người giao hàng thực tế và phục vụ toàn quốc Việt Nam.
- Khách chọn WorkspaceEC hoặc đơn vị vận chuyển đang tích hợp API MiniLogistics.
- Phí WorkspaceEC chủ yếu dựa trên vùng giao và tổng trọng lượng hàng.
- Admin trực tiếp cấu hình vùng, bảng phí và quản lý giao hàng WorkspaceEC.
- WorkspaceEC phải hoạt động khi service vận chuyển bên ngoài bị lỗi hoặc được tắt.
- Phí và tổng tiền do backend quyết định; frontend chỉ gửi lựa chọn và hiển thị báo giá.

Phạm vi bản đầu đã chốt tại SHIP-00: một phương thức/phương án giao cho mỗi đơn; một điểm xuất hàng cấu hình; trọng lượng thực; bảng phí theo vùng; quản lý giao nội bộ qua admin; tích hợp COD, VNPay và chuyển khoản theo điều kiện thanh toán được duyệt. SHIP-00–09 đã có nền dữ liệu, calculator, API/UI bảng phí, quote và checkout nguyên tử, routing fulfillment, xác nhận chuyển khoản/thu và nộp COD. Lifecycle giao nội bộ, storefront, read models đầy đủ và release gates thuộc SHIP-10–14; chưa enable nhận đơn mới thực tế.

Chưa bao gồm: nhiều kho/điểm xuất hàng, chia kiện/chia chuyến, định tuyến GPS, app riêng cho shipper, tối ưu tuyến, tự động đối soát ngân hàng, hoàn tiền tự động, bảo hiểm/phí lắp đặt/khuyến mãi miễn ship mới. Nếu các chính sách này cần ngay bản đầu, điều chỉnh SHIP-00 trước khi code.

## 2. Phân tích source trước triển khai (baseline)

| Điểm đọc | Hiện trạng và ảnh hưởng |
| --- | --- |
| [CheckoutRequest](../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutRequest.cs), [ShippingQuoteModels](../src/WorkspaceEcommerce.Application/Modules/Ordering/ShippingQuoteModels.cs) | Chưa có lựa chọn phương thức, mã vùng hành chính và quote đã được khách chấp nhận. |
| [CheckoutService](../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutService.cs), [CheckoutShippingCoordinator](../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutShippingCoordinator.cs) | Quote gắn với một provider. VNPay quote trước placement; COD/chuyển khoản refresh phí sau commit; lỗi quote có thể để đơn có phí ban đầu bằng 0. |
| [CheckoutOrderPlacer](../src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderPlacer.cs), [PaymentService](../src/WorkspaceEcommerce.Application/Modules/Payments/PaymentService.cs) | COD enqueue shipment khi tạo đơn; VNPay success enqueue shipment trong transaction. Cần phân luồng theo phương thức đã lưu trên đơn. |
| [OrderShipmentService](../src/WorkspaceEcommerce.Application/Modules/Shipments/OrderShipmentService.cs) | Trực tiếp dùng một provider để tạo/hủy/refresh, còn quote rồi gọi `Order.SetShippingFee()` khi tạo shipment. Parcel dựng lại từ catalog đang thay đổi. |
| [OrderShipment](../src/WorkspaceEcommerce.Domain/Modules/Shipments/OrderShipment.cs), [ShipmentTimelineEntry](../src/WorkspaceEcommerce.Domain/Modules/Shipments/ShipmentTimelineEntry.cs) | Shipment yêu cầu provider shipment ID và provider status. Chưa thể biểu diễn giao nội bộ một cách rõ ràng. |
| [ShipmentProviderContract](../src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentProviderContract.cs), [ShipmentOrderStatusTransitioner](../src/WorkspaceEcommerce.Application/Modules/Shipments/ShipmentOrderStatusTransitioner.cs) | Protocol và mapping trạng thái đang dành cho MiniLogistics; không đổi nghĩa để giả lập WorkspaceEC. |
| [CheckoutPage](../frontend/apps/storefront/src/pages/checkout/CheckoutPage.tsx) | Chỉ lấy một quote; quote chưa có thì `shippingFee` trở thành 0. Địa chỉ chọn theo code ở UI nhưng gửi tên sang backend. |
| [Vietnam administrative units](../frontend/apps/storefront/src/constants/vietnamAdministrativeUnits.ts) | Có dataset frontend kèm commit nguồn. Cần kiểm chứng phiên bản và dùng dữ liệu chuẩn ở backend trước khi cam kết phủ toàn quốc; không hard-code số tỉnh/xã vào nghiệp vụ. |
| [AdminOrderService](../src/WorkspaceEcommerce.Application/Modules/Ordering/AdminOrderService.cs), [CustomerOrderService](../src/WorkspaceEcommerce.Application/Modules/Customers/Orders/CustomerOrderService.cs) | Thay đổi trạng thái, cancel, stock, loyalty, notification và import phải được rà soát cùng luồng shipping mới. |
| [Infrastructure DI](../src/WorkspaceEcommerce.Infrastructure/DependencyInjection.cs) | Đăng ký MiniLogistics client/worker hiện không phân biệt chế độ chỉ giao nội bộ. |

Giữ kiến trúc modular monolith, direct EF qua focused stores, transaction và outbox/inbox hiện có. Không thêm microservice, generic repository, MediatR hoặc event bus. Đối chiếu [architecture](../.agents/architecture.md), [data/consistency](../docs/backend/architecture/data-and-consistency.md), [ADR 006](../docs/backend/adr/006-durable-external-side-effects.md).

## 3. Thiết kế đích đã chốt, triển khai theo từng task

### 3.1. Tách báo giá, tiền trên đơn và thực hiện giao

- `ShippingMethod`: định danh ổn định cho WorkspaceEC/MiniLogistics, độc lập `PaymentMethod`; persist trên đơn trước khi có vận đơn.
- `Order.ShippingFee`: phí khách chấp nhận trả, snapshot khi nhận đơn; `TotalAmount = Subtotal + ShippingFee - DiscountAmount` giữ nguyên công thức hiện tại.
- Chi phí giao thực tế: giá/chi phí đối tác hoặc chi phí nội bộ, nullable khi chưa biết; không dùng 0 thay cho chưa xác định và không tự cộng lại vào tiền khách.
- Snapshot giao hàng: địa chỉ code + tên, vùng, tổng trọng lượng/parcel, phiên bản bảng giá, cách tính phí và quote nguồn. Không dựng lại lịch sử từ catalog/bảng giá hiện tại.
- Mọi đơn mới trong luồng mới chốt phí trước khi commit, kể cả COD/chuyển khoản. Không tự đổi phương thức khi đối tác lỗi.
- Tạo vận đơn external tiếp tục dùng outbox; local shipment có thể tạo trong transaction DB cùng sự kiện đủ điều kiện. Không gọi HTTP external khi đang giữ khóa đặt hàng.

### 3.2. Bảng phí WorkspaceEC

Đề xuất bản đầu: nhóm mã tỉnh/thành thành vùng giao tùy chỉnh; từng tỉnh thuộc đúng một vùng trong một phiên bản bảng giá. Mỗi vùng có phí cơ bản và phí theo bước trọng lượng. Không suy vùng từ chuỗi tên hoặc tiền tố mã.

```text
W = tổng(trọng lượng thực từng sản phẩm × số lượng)
Số bước thêm = ceil(max(0, W - Trọng lượng cơ bản) / Bước trọng lượng)
Phí khách trả = Phí cơ bản của vùng + Số bước thêm × Phí mỗi bước
```

- Dùng đơn vị chuẩn cho tính toán trọng lượng, đề xuất gram nguyên; chốt quy tắc đổi/làm tròn tại SHIP-00. Tiền dùng decimal, VND nguyên, không âm.
- Thiếu trọng lượng, thiếu vùng hoặc cấu hình không hợp lệ phải trả lý do; không mặc định 0 kg, 0đ hoặc tự dùng fallback 0,5 kg hiện có cho WorkspaceEC.
- Publish chỉ thành công khi phủ đủ danh mục hành chính được phê duyệt, không trùng vùng và mọi vùng có giá hợp lệ cho phạm vi trọng lượng cam kết.
- Địa bàn đặc biệt/đảo/giới hạn trọng lượng cần chính sách rõ; chưa đủ quy tắc thì chưa được tuyên bố cấu hình sẵn sàng phục vụ toàn quốc.
- Bảng giá có Draft/Published/Retired. Bản Published cùng membership vùng là immutable; sửa bằng bản mới. Publish có concurrency control, lịch sử người thay đổi và thời gian UTC.
- Không seed mức phí thương mại giả định. Chỉ seed dữ liệu giả vào fixture/test; bảng giá production phải được admin nhập và xác nhận.

### 3.3. Quote và checkout

Quote do server phát hành, có ID khó đoán, hạn dùng, phương thức, chủ thể/session, cart fingerprint, địa chỉ, trọng lượng, currency, fee và phiên bản quy tắc. Không giữ/trừ stock khi chỉ báo giá.

Checkout dùng quote được chọn và kiểm tra lại điều kiện trong transaction placement; quote chỉ được gắn thành công cho một order. Hết hạn, sai người/giỏ/địa chỉ/phương thức, stock thay đổi hoặc quote bị thu hồi phải từ chối rõ ràng, không chốt giá mới âm thầm. Quy tắc honor quote khi publish bảng giá mới và thời hạn quote là quyết định tại SHIP-00.

Đối tác quote ở ngoài transaction có khóa; commit kiểm tra fingerprint và dữ liệu authoritative. Khi đối tác tạo shipment báo chi phí khác, lưu chênh lệch để xử lý vận hành; không cập nhật `Order.TotalAmount` hoặc số tiền VNPay đã chốt.

### 3.4. Dữ liệu/điểm mở rộng dự kiến

Các tên dưới đây là đề xuất để phân việc; chốt model/DTO trong SHIP-00/01, không coi là contract đã triển khai.

| Thành phần | Trách nhiệm |
| --- | --- |
| Administrative catalog có version | Validate mã tỉnh/xã, quan hệ cha/con và mapping tên cho API đối tác. |
| Rate plan version + zone membership + zone rate | Quản lý cấu hình/phiên bản/phủ toàn quốc trong schema `shipping`. |
| Shipping quote | Snapshot đầu vào/giá, expiry, chủ thể và liên kết tiêu thụ vào order; audit hợp lý, không log PII. |
| Shipping selection snapshot trên order | Lưu phương thức, fee, quote/version và parcel authoritative cho đơn mới. |
| Local shipment + timeline/audit | Dùng model shipment chung nếu semantics phù hợp; provider ID nullable có điều kiện, không tạo ID external giả cho WorkspaceEC. |
| Cost/payment records tối thiểu | Phân biệt chi phí giao, xác nhận chuyển khoản, xác nhận thu COD với trạng thái giao. |
| Application services | Chọn phương thức, tính phí local, quản lý bảng giá, điều phối fulfillment, thao tác giao nội bộ. |
| MiniLogistics adapter hiện có | Giữ nguyên protocol/header/signature/idempotency; chỉ xử lý đúng đơn external. |

`Order.ShipmentId` đang nhận ID phía provider; phải làm rõ mapping với local shipment ID ở SHIP-01, giữ tương thích các consumer cũ và không bulk-rename ID trong dữ liệu lịch sử.

## 4. Quyết định đã xác nhận tại SHIP-00

Product owner đã xác nhận triển khai D01–D08 theo đề xuất ngày 2026-10-29. [ADR 007](../docs/backend/adr/007-workspace-shipping-rates.md) là bản ghi quyết định. Giá thương mại, origin và giới hạn theo vùng là dữ liệu admin nhập, không phải các con số được tự seed từ ví dụ/test. Việc đã chốt thiết kế không có nghĩa mọi luồng sau SHIP-04 đã được triển khai.

| ID | Quyết định | Nội dung được duyệt |
| --- | --- | --- |
| D01 | Điểm xuất hàng, định nghĩa các vùng và bảng phí thật | Bản đầu một origin; admin nhóm mã tỉnh vào vùng; không giới hạn sẵn Bắc/Trung/Nam. |
| D02 | Công thức, đơn vị/bước làm tròn, dữ liệu thiếu cân nặng, trọng lượng tối đa/địa bàn đặc biệt | Cân thực, tổng kg × quantity rồi làm tròn lên gram một lần; công thức mục 3.2. Admin cấu hình giá/giới hạn từng vùng, bao gồm địa bàn đặc biệt; không fallback cân giả hoặc tự dùng cân quy đổi. |
| D03 | Quote expiry, publish mới, tạm dừng phương thức | TTL dự kiến mặc định 10 phút, có cấu hình tại SHIP-06; quote hợp lệ giữ giá đến hết hạn khi publish mới; tạm dừng khẩn có thể thu hồi quote chưa dùng với lý do. |
| D04 | Phí đối tác và chênh lệch chi phí lúc tạo vận đơn | Chốt giá khách tại checkout; website xử lý chênh lệch vận hành; không ghi đè tổng tiền/amount đã cam kết. |
| D05 | Thời điểm tạo phiếu/cho xuất giao; VNPay/chuyển khoản/COD | Có thể tạo phiếu chờ sớm nhưng chỉ xuất giao prepaid khi Paid; COD theo xác nhận đơn. Chuyển khoản cần thao tác xác nhận thu tiền có audit. |
| D06 | COD: ghi nhận tiền thu, tiền nộp về và Completed | Thu đủ mới hoàn tất COD; thu tiền và nộp tiền là hai sự kiện riêng. Thiếu/thừa/partial cần luồng ngoại lệ, không sửa tổng tiền hoặc giả lập Paid chỉ vì bấm Delivered. |
| D07 | Đơn cũ/client cũ/admin import, lựa chọn mặc định | Giữ snapshot/luồng cũ, contract mới additive; không mặc định đơn thiếu selection thành WorkspaceEC. Local shipment ID khác nghĩa legacy external ID. |
| D08 | Giao thất bại, giao lại, hủy và đổi phương thức | Giữ chuyển trạng thái order hiện có. Bản đầu không tự đổi carrier/địa chỉ/tổng tiền sau nhận đơn; quy định việc giao lại và chi phí phát sinh. |

## 5. Danh sách task triển khai

Checkbox được cập nhật theo kết quả triển khai trong nhật ký mục 10. Mỗi task phải có diff nhỏ, test liên quan và validation thực tế; không ghi Done dựa vào kế hoạch hoặc test của working tree trước đó.

### [x] SHIP-00 — Chốt nghiệp vụ, contract và ADR

- **Ưu tiên:** P0. **Phụ thuộc:** không.
- Chốt D01–D08; ghi ví dụ số tiền/cân nặng ở các ngưỡng, bảng mapping payment/fulfillment và trường hợp bị từ chối.
- Chốt schema/DTO/routes, quote lifecycle/idempotency, ý nghĩa shipment IDs, rollout compatibility và phạm vi một origin/một shipment.
- Thêm ADR mới theo số kế tiếp trong `docs/backend/adr`; mô tả phần mở rộng/supersession cụ thể của ADR 006, không sửa lịch sử quyết định cũ.
- Ghi baseline test về checkout/payment/cancel/outbox, gồm đơn cũ/import. Không coi fixture hiện có là bằng chứng mọi tình huống mới đã được bảo vệ.
- **Nghiệm thu:** quyết định có chủ thể xác nhận; ví dụ tính phí và state matrix nhất quán; không còn câu hỏi tiền/fulfillment chặn SHIP-01/03/07/09.

### [x] SHIP-01 — Domain model và migration có tương thích lịch sử

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-00.
- Thêm model rate plan/version/zone/rate, quote, order shipping snapshots và audit trong module/schema đang sở hữu.
- Mở rộng shipment để local/external được biểu diễn đúng; chuẩn hóa ý nghĩa ID, provider-specific required fields và mã giao nội bộ unique. Giữ giá trị enum cũ, chỉ append khi cần.
- Thiết kế FK/index/precision/unique constraint, version chống lost update, quote consumption atomic và publish-active-version concurrency. Database bảo vệ invariant cạnh tranh; không chỉ pre-check.
- Migration additive cho đơn/shipments/outbox đang tồn tại. Phân loại legacy bằng bằng chứng; dữ liệu không xác định giữ trạng thái legacy rõ ràng. Không suy lại phí/cân nặng lịch sử từ catalog mới.
- Chốt thứ tự deploy expand/enable, semantics cho worker/client phiên bản cũ và forward fix khi đã có đơn WorkspaceEC. Không chạy worker cũ trên đơn mới chưa hiểu method.
- **Điểm sửa:** `Domain/Modules/Shipments`, `Domain/Modules/Ordering`, `Infrastructure/Persistence/Configurations`, `AppDbContext`, migrations mới; focused persistence ports.
- **Nghiệm thu:** domain/mapping tests; clean + upgrade migration với seed đơn cũ/paid/outbox pending; historical totals/IDs giữ nguyên; pending-model check pass trên diff liên quan.

### [x] SHIP-02 — Danh mục địa chỉ và vùng giao toàn quốc

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-01.
- Kiểm chứng nguồn danh mục hành chính với nguồn chính thức phù hợp tại thời điểm triển khai; ghi nguồn/version/date. Dataset đang nằm ở frontend chỉ là điểm xuất phát.
- Backend validate code và quan hệ tỉnh/xã. Frontend dùng cùng version qua API hoặc artifact sinh từ một nguồn; không để hai danh sách copy độc lập.
- Mỗi tỉnh thuộc đúng một vùng trong phiên bản rate plan; kiểm tra thiếu/trùng/không tồn tại và coverage toàn quốc trước publish.
- Lưu cả code và tên snapshot trên đơn; API MiniLogistics vẫn nhận đúng payload tên theo protocol hiện tại. Mapping legacy tên mơ hồ phải xử lý tường minh.
- Có báo cáo địa bàn thiếu rule/địa chỉ chưa ánh xạ; chốt phạm vi địa bàn đặc biệt theo D02.
- **Nghiệm thu:** test code sai/cha-con sai/duplicate/thiếu coverage/version thay đổi; không hard-code số lượng đơn vị hành chính; không đổi địa chỉ đơn cũ.

### [x] SHIP-03 — Bộ tính phí vùng và trọng lượng

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-00, SHIP-01, SHIP-02.
- Implement calculator thuần trong Application/Domain thích hợp; đọc rule Published theo version, tổng trọng lượng nhân đủ quantity, trả breakdown có thể giải thích.
- Áp dụng D02 cho chuyển đổi đơn vị/làm tròn, cân nặng thiếu/sai/ngoài giới hạn; báo danh sách catalog cần bổ sung trọng lượng trước enable.
- Snapshot parcel/weight cho đơn mới; external shipment dùng snapshot đã chốt, không lấy lại dữ liệu variant mới để quyết định giá khách.
- Không thêm miễn ship, phụ phí COD/insurance/lắp đặt hoặc thay đổi coupon/loyalty ngoài chính sách đã duyệt.
- **Nghiệm thu:** test ngay dưới/bằng/trên base weight và step, nhiều SKU/quantity, số tiền lớn/overflow, cấu hình âm/bước 0, thiếu giá/cân nặng; cùng input/version luôn cùng fee.

### [x] SHIP-04 — Admin API quản trị bảng phí và publish

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-01, SHIP-02, SHIP-03.
- Use case draft/clone/edit/validate/publish/retire/history/preview. Chỉ draft được sửa; preview chỉ rõ draft hay giá đang áp dụng.
- Publish nguyên tử cả mapping vùng và rates, chỉ một phiên bản active cho phạm vi v1; request mang version để phát hiện hai admin sửa/publish đồng thời.
- Auth role Admin phía server; actor lấy từ claims, audit ai/lúc nào/đổi gì, lý do publish/disable. Không nhận actor hay giá quote tin cậy từ client.
- API dự kiến dưới `api/admin/shipping/...`; giữ envelope/error conventions và bounded paging. Public API không lộ chi phí nội bộ/audit.
- **Nghiệm thu:** integration 401/403/validation/409; publish thiếu vùng, concurrent publish, edit Published bị chặn; đơn/quote cũ không bị rewrite.

### [x] SHIP-05 — Admin UI vùng, bảng phí và công cụ thử giá

- **Ưu tiên:** P1. **Phụ thuộc:** SHIP-04.
- Thêm lazy route/nav “Vận chuyển”; trang bảng giá, vùng/thành viên, form phí/cân nặng và trang lịch sử. Chia page/component/query hook vừa đủ, không dồn vào `OrdersPage.tsx`.
- Dùng stack hiện có, React Hook Form/Zod, TanStack Query; contracts ở `api-types`, adapter ở app API, hooks dưới `hooks/queries`.
- Hiển thị Draft/Published, đơn vị g/kg và VND, coverage thiếu/trùng, lỗi field, conflict version; preview địa chỉ/trọng lượng trước publish.
- Publish/retire có thông tin tác động và xác nhận thao tác; invalidate đúng cache. Không hiển thị preview draft như bảng giá đã áp dụng.
- **Nghiệm thu:** Playwright UI với API mock cho edit/preview/publish/conflict, keyboard/error/loading/empty; lint/typecheck/build. Không tự thêm framework/unit-test runner admin.

### [x] SHIP-06 — API phương thức khả dụng và quote server-side

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-02, SHIP-03, SHIP-04.
- API trả các lựa chọn cho giỏ/địa chỉ/payment context: WorkspaceEC tính local; MiniLogistics timeout/retry có giới hạn. Không bắt WorkspaceEC chờ provider mới hiển thị/chấp nhận được.
- Quote có fee, currency, expiry, method, fingerprint/version; lưu/bảo vệ server-side và ràng buộc đúng customer/session. Có TTL/retention phù hợp, không lộ PII qua ID/log.
- Đối tác lỗi/disabled thì option báo unavailable có lý do an toàn; không trả phí 0 và không tự đổi lựa chọn.
- Không tin client weight/fee/region; frontend gửi code, backend resolve rule. D03 quy định honor/revoke quote khi cấu hình thay đổi.
- Chốt API mới/additive và hành vi endpoint quote hiện tại theo D07; OpenAPI/các DTO dùng chung cập nhật cùng lượt.
- **Nghiệm thu:** WorkspaceEC quote thành công khi provider timeout/throw; cross-customer/session bị chặn; stale input/rule/expiry có kết quả rõ; list options không bị lỗi toàn bộ do một provider.

### [x] SHIP-07 — Checkout chốt phí và tổng tiền nguyên tử

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-01, SHIP-03, SHIP-06.
- Mở rộng request bằng method/quote selection và địa chỉ code theo contract đã chốt. Bảo vệ raw frontend fee/status/customer/method tampering.
- Quote external trước transaction; trong placement kiểm tra quote, input fingerprint, stock/coupon authoritative và rate snapshot. Thay đổi giá/địa chỉ/cân nặng làm quote không còn phù hợp phải yêu cầu xác nhận lại.
- Lưu order/items/shipping snapshots, consume quote và reserve stock/coupon trong cùng transaction. Retry sau response mất phải có kết quả theo contract idempotency; không tạo đơn/trừ stock hai lần.
- Luồng mới bỏ refresh làm thay đổi phí khách sau commit cho COD/chuyển khoản; VNPay transaction lấy chính xác total đã chốt. Không reprice `UnitPriceSnapshot` giỏ ngoài phạm vi.
- Audit mọi nơi `SetShippingFee` và chặn worker/admin/import gọi sai semantics với đơn mới. Đơn legacy đi nhánh được D07 quy định, không bị sửa snapshot âm thầm.
- **Nghiệm thu:** quote bị sửa/hết hạn/đã dùng, 2 request cùng quote, stock cuối cùng, publish đồng thời; failure không còn order/payment/outbox/stock/coupon dở dang. Số tiền response/DB/VNPay trùng nhau.

### [x] SHIP-08 — Điều phối fulfillment theo phương thức và bảo vệ đối tác

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-01, SHIP-07.
- Dùng một coordinator/use case nhỏ để chọn local hoặc external từ order snapshot; không phân tán `if WorkspaceEC` trong mọi controller và callback.
- COD theo D05 tạo local work hoặc external outbox cùng transaction nhận đơn. VNPay callback đúng amount/signature tạo đúng work trong transaction payment; duplicate callback không tạo lại.
- Chỉ đơn external enqueue/gọi MiniLogistics create/cancel/refresh. Webhook kiểm tra provider, order mapping và event identity; webhook external không cập nhật local shipment.
- Giữ leases/retry/dead-letter/replay và idempotency keys hiện có. Replay resolve method/provider đã persist; không dựa cấu hình mặc định đang đổi. Có xử lý rõ backlog legacy/unknown.
- Feature/config phân biệt ngừng nhận đơn external mới với khả năng xử lý backlog/callback đơn external cũ. Khi không có external work, local-only boot/checkout/tracking không cần MiniLogistics khỏe hay có credentials hoạt động.
- **Nghiệm thu:** fake provider đếm 0 call cho WorkspaceEC xuyên quote/create/cancel/refresh/replay/payment; external contracts và concurrency tests cũ pass; disable nhận mới không làm mất callback/backlog.

### [x] SHIP-09 — Xác nhận thanh toán chuyển khoản và thu COD tối thiểu

- **Ưu tiên:** P0 nếu bật đủ ba payment method. **Phụ thuộc:** SHIP-00, SHIP-07, SHIP-08.
- Thêm admin command xác nhận chuyển khoản đúng order/method/amount/currency, tham chiếu giao dịch/lý do/actor/time; duplicate confirmation idempotent, concurrent cancel/payment dùng lock order thống nhất.
- Xác nhận Paid và tạo work đúng method trong một transaction; khách không tự xác nhận được. Chưa xác nhận thì không cho xuất giao prepaid.
- Ghi thu COD theo D06 với guard amount/state; tách collected và remitted nếu được duyệt. Không tự thay doanh thu/tổng tiền để khớp khoản thu sai.
- Bổ sung thao tác UI phù hợp trong chi tiết đơn/giao hàng; không mở rộng thành kế toán/đối soát ngân hàng tự động.
- Nếu hoãn chuyển khoản hoặc COD collection, ghi phạm vi rõ và chặn các kết hợp payment mới chưa được hỗ trợ; không đánh dấu bộ tính năng toàn bộ payment đã hoàn tất.
- **Nghiệm thu:** duplicate/partial/overpayment/cancel race, 401/403, không giao prepaid chưa Paid, không tạo hai shipment, không thay đổi semantics thanh toán đơn lịch sử ngoài D07.

### [x] SHIP-10 — Lifecycle giao nội bộ và thao tác admin

- **Ưu tiên:** P0. **Phụ thuộc:** SHIP-08, SHIP-09 theo payment được bật.
- Tạo local shipment idempotent với mã nội bộ unique và trạng thái chờ phù hợp; gán người phụ trách bằng tham chiếu vận hành tối thiểu được duyệt, không tạo identity/shipper platform mới.
- Các command: nhận giao/xuất giao, giao thành công, thất bại, giao lại, hủy theo D05/D06/D08. Actor/time lấy từ server; reason bắt buộc cho ngoại lệ; lock/version bảo vệ thao tác cạnh tranh.
- Mapping local status sang order status tường minh, dùng domain transition hiện có; cập nhật shipment + order + timeline/history trong transaction. Không giả lập webhook/provider ID MiniLogistics.
- Rà admin đổi order status trực tiếp và customer cancel để tránh hai nguồn trạng thái mâu thuẫn. Stock restore, loyalty, CompletedAt/warranty và thông báo đi đường nghiệp vụ hiện có, idempotent theo kết quả.
- Không dùng `Returned` của đối tác như business return/refund. Hủy/giao thất bại không tự tạo refund đã chi tiền; giữ RefundPending theo chính sách hiện tại.
- **Nghiệm thu:** lặp thao tác không lặp history/points/stock; không giao đơn hủy/chưa đủ payment; failed-delivery retry đúng graph; concurrent cancel/deliver deterministic; local cancel không enqueue MiniLogistics.

### [x] SHIP-11 — Storefront lựa chọn phương thức và quản lý quote

- **Ưu tiên:** P1. **Phụ thuộc:** SHIP-06, SHIP-07, SHIP-08.
- Thêm lựa chọn WorkspaceEC/đối tác với fee, trạng thái khả dụng và thông tin thời gian giao nếu có cấu hình thật; không tự hứa ETA.
- Query key gồm giỏ/fingerprint, địa chỉ code, phương thức/payment và yếu tố thực sự ảnh hưởng phí. Xóa quote đã chọn khi input thay đổi; chặn response cũ ghi đè quote mới.
- Phân biệt loading/unknown/unavailable/0đ có căn cứ. Chặn submit khi chưa có quote hợp lệ; UI xử lý expiry/conflict và yêu cầu xác nhận giá mới.
- Đổi phương thức cập nhật total từ quote server; không tự đổi sang carrier khác khi lựa chọn lỗi. Không sửa hành vi guest checkout/login guard trong task này.
- Tách form/selection/summary hook/component khi phù hợp; hỗ trợ vi/en, keyboard và lỗi field.
- **Nghiệm thu:** Vitest logic/API liên quan và Playwright cho hai lựa chọn, địa chỉ/quantity đổi nhanh, external down, phí 0 hợp lệ, hết hạn, retry không trùng order; đúng total ở success/VNPay.

### [ ] SHIP-12 — Read models, tracking, receipts và admin import

- **Ưu tiên:** P1. **Phụ thuộc:** SHIP-08, SHIP-10, SHIP-11.
- Cập nhật DTO/mappers/admin order list/detail, customer order list/detail, guest lookup/tracking, receipt PDF và success/payment-result để đọc method/fee snapshot nhất quán.
- Capability theo method: local có thao tác nội bộ; external có retry/refresh phù hợp. Read local tracking không gọi API external. Public view không lộ actual cost, thông tin nhân viên nội bộ, PII ngoài quyền hay internal notes.
- Admin import có semantics legacy/new rõ theo D07: phí import, địa chỉ/cân nặng, method và eligibility giao phải được validate/preview; không áp giá mới cho đơn import cũ hoặc tự dispatch khi dữ liệu chưa đủ.
- Preserve ownership/customer filters và guest proof order-code + phone; không biến mã local tracking thành đường truy cập bỏ qua authorization.
- **Nghiệm thu:** contract/auth tests cho admin/customer/guest; PDF tổng tiền khớp order; đơn cũ vẫn đọc được; import preview/commit không gây shipment ngoài ý muốn.

### [ ] SHIP-13 — Kiểm thử xuyên luồng, concurrency và hiệu năng

- **Ưu tiên:** P0 release gate. **Phụ thuộc:** SHIP-04–12; test từng task thực hiện ngay, không đợi task này.
- Hoàn tất ma trận mục 7, PostgreSQL independent scopes cho race; kiểm tra command counts và bounded paging của list/quote, tránh N+1 theo item/vùng.
- Browser có hai lớp: mock API cho UI states; E2E thật với PostgreSQL/API/provider giả để chứng minh cart → quote → order → payment → fulfillment. Không chỉ dựa ảnh screenshot hoặc browser request mocking.
- Fixture/test xác định thời gian bằng TimeProvider, dữ liệu synthetic, không gọi VNPay/MiniLogistics thật; giữ safety wrapper của E2E.
- Đưa test mới vào CI đúng job. Chạy full regression sau checks tập trung; ghi rõ skipped/block vì Docker/tooling và mọi regression do working tree chưa liên quan.
- **Nghiệm thu:** toàn bộ matrix có test hoặc ngoại lệ được giải thích; build/test/lint/typecheck pass theo phạm vi; không tăng query theo số item không cần thiết; không tuyên bố capacity chưa load-test.

### [ ] SHIP-14 — Tài liệu, seed/enable và rollout vận hành

- **Ưu tiên:** P0 release gate. **Phụ thuộc:** SHIP-13.
- Cập nhật tài liệu sở hữu: business rules, backend consistency/API, frontend, services runbooks và ADR index; phân biệt thật đã làm với proposal. Sửa link liên quan trong phạm vi shipping, không dọn docs toàn repo.
- Runbook: nhập/publish bảng phí, kiểm tra coverage/cân nặng, preview đối chiếu đơn mẫu, tạm dừng nhận đơn theo method, xử lý quote conflict/chênh lệch chi phí/dead letter/giao thất bại và khôi phục.
- Deploy schema → code hiểu cả legacy/new (mọi worker replica) → cấu hình → admin nhập bảng phí thật → validate/publish → smoke → enable nhận đơn. Không tự publish seed demo trên production.
- Theo dõi theo method: quote failure/latency, mismatch/expiry, rate-plan version, publish audit, local backlog/failed delivery, external queue age/cost discrepancy; không log payload/địa chỉ/số điện thoại.
- Rollback bằng disable nhận đơn mới và forward fix; tiếp tục xử lý đơn/quote/work đã cam kết. Không xóa tariff/order/shipment/audit hoặc chạy destructive Down sau phát sinh đơn mới.
- **Nghiệm thu:** chạy local-only với đối tác unavailable, chạy hai method với provider giả, kiểm chứng backlog đơn cũ; business duyệt giá/coverage và QA ký checklist trên candidate cụ thể.

## 6. Thứ tự thực hiện và mốc bàn giao

| Mốc | Tasks | Kết quả có thể kiểm chứng |
| --- | --- | --- |
| M0 — Thiết kế | 00 | Quy tắc tiền, phạm vi, state graph, contract và migration strategy được chốt. |
| M1 — Cấu hình phí | 01 → 02 → 03 → 04 → 05 | Admin tạo/publish bảng phí đủ vùng; preview backend đúng, chưa bật checkout mới. |
| M2 — Checkout và routing | 06 → 07 → 08 | Chốt phí authoritative, quote có kiểm soát, local không gọi đối tác. |
| M3 — Vận hành | 09 → 10; 11 sau 06–08; 12 sau 10–11 | Khách chọn, admin giao hàng/ghi nhận payment, các read model nhất quán. |
| M4 — Release | 13 → 14 | Migration/regression/E2E, cấu hình thật, docs và rollout gates hoàn tất. |

Chỉ mở WorkspaceEC khi có ít nhất một vertical slice từ bảng phí đã publish đến checkout, payment đủ điều kiện, giao/cancel/tracking và test tương ứng. Nếu chia release COD trước, VNPay/chuyển khoản sau, phải được D05/D06 xác nhận và công khai capability được bật; không nhận đơn chưa có cách xử lý hoàn chỉnh.

## 7. Ma trận nghiệm thu bắt buộc

| Nhóm | Tình huống bắt buộc |
| --- | --- |
| Phủ toàn quốc | Toàn bộ mã trong catalog đã duyệt thuộc đúng một vùng; không missing/duplicate; địa bàn đặc biệt theo D02. |
| Phí | Ngưỡng cân/bước, quantity, sai/mất cân nặng, giới hạn và VND nguyên; báo giá và order/receipt/payment bằng nhau. |
| Admin | CRUD draft, preview, publish nguyên tử, version conflict, history/actor; sửa/publish không đổi đơn cũ. |
| Quote | Sai chủ thể/session, cart/address/method/payment đổi, expiry, republish/revoke, reuse/race và response retry. |
| Thanh toán | Hai method × COD/VNPay/chuyển khoản theo capability đã bật; wrong amount/signature, duplicate callback/confirmation, unpaid không được xuất giao prepaid. |
| Độc lập đối tác | Local-only khởi động và hoàn tất vòng đời khi MiniLogistics down/disabled; không có HTTP/outbox external cho local. |
| Fulfillment | Duplicate create/deliver/cancel, concurrent cancel/deliver/payment, thất bại/giao lại, không lặp stock/points; webhook external không mutate local. |
| Chi phí | Quote provider và create cost lệch nhau không đổi total/VNPay amount; chưa biết cost không thành số 0. |
| Bảo mật | Admin-only writes, customer ownership, guest proof, quote tampering; public DTO/log không lộ actual cost/PII/notes nội bộ. |
| Tương thích | Đơn cũ, paid order, import, pending outbox, callback/replay sau deploy; không đổi enum/ID/snapshot/protocol lịch sử. |
| Frontend | Hai phương thức, input đổi nhanh, loading/error/zero, quote stale/expiry, unavailable, xác nhận lại giá, keyboard/vi/en. |
| Database | Clean/upgrade migration, constraints/atomicity trên PostgreSQL, pending-model và query budget; không dùng fake/InMemory chứng minh lock. |

## 8. Lệnh validation khi triển khai

Tham chiếu [commands](../.agents/commands.md) và CI hiện tại. Chạy test tập trung theo task trước, sau đó gate toàn feature:

```powershell
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --configuration Release
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --configuration Release
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/WorkspaceEcommerce.Infrastructure --startup-project src/WorkspaceEcommerce.Api --context AppDbContext
./scripts/verify-prh-009-migrations.ps1
```

Trong `frontend/`:

```powershell
corepack pnpm lint
corepack pnpm test
corepack pnpm typecheck
corepack pnpm build
corepack pnpm test:ui:admin
```

Browser E2E thật từ root: mở rộng và chạy `./scripts/run-prh-015-storefront-e2e.ps1` với kịch bản shipping synthetic/provider giả. Admin UI mock không thay thế end-to-end backend. PostgreSQL integration/migration cần Docker; lỗi Docker là blocked environment, không ghi test pass. Không chạy load hay gọi provider thật trên môi trường chưa được phép.

## 9. Definition of Done và trạng thái tài liệu

- [ ] SHIP-00–14 hoàn tất theo capability được duyệt; mọi quyết định D01–D08 được ghi nhận.
- [ ] WorkspaceEC phủ đủ phạm vi địa lý/trọng lượng đã cam kết, có bảng giá thật quản lý từ admin và hoạt động độc lập đối tác.
- [ ] Khách chọn phương thức; quote/đơn/payment/receipt nhất quán; phí đã chốt không bị worker hay thay đổi bảng phí viết lại.
- [ ] Payment/dispatch/COD/cancel/delivery có guard, audit, idempotency và coverage thực tế.
- [ ] Đơn cũ/import/backlog/protocol an toàn; migration và rollback/forward-fix được kiểm chứng.
- [ ] Test và tài liệu/runbook được cập nhật; skipped/blocked được báo trung thực.

Bản kế hoạch ban đầu chưa thay đổi code; nhật ký dưới đây ghi riêng các task đã triển khai và validation mới. Working tree có nhiều thay đổi ngoài phạm vi được giữ nguyên, không đưa vào các commit shipping. Migration chỉ chạy trên database test cô lập; chưa áp dụng vào database local/production của người dùng.


## 10. Nhật ký triển khai SHIP-00–04

- 2026-10-29 — SHIP-00: product owner xác nhận D01–D08 theo đề xuất. [ADR 007](../docs/backend/adr/007-workspace-shipping-rates.md) chốt công thức/cân thực, TTL quote dự kiến 10 phút, snapshot tiền/địa chỉ/parcel, lifecycle và compatibility. Giá thương mại/origin cụ thể do admin nhập, không seed giả.
- Baseline trước sửa code: `dotnet test WorkspaceEcommerce.slnx --no-restore --configuration Release --verbosity minimal`: 682 pass (330 Application, 238 Infrastructure, 114 API integration). EF pending-model check Release/no-build: không có thay đổi model trước triển khai.
- SHIP-01–04: đã hoàn tất phần backend theo phạm vi; checkout/fulfillment mới chưa bật, admin UI thuộc SHIP-05.
- SHIP-01: đã thêm model bảng giá/vùng/audit, quote consumption và snapshot lựa chọn đơn; local shipment có ID riêng, provider ID nullable có constraint, actual cost nullable tách trường legacy. Không sửa ID/tổng tiền đơn cũ. Migration mới `20261029041328_AddWorkspaceShippingFoundation` có guard chống Down khi đã có dữ liệu mới.
- Validation SHIP-01: 9 domain tests, 1 mapping test và 10 integration tests tập trung pass (upgrade có dữ liệu cũ, constraints/quote race, webhook regression); script `verify-prh-009-migrations.ps1 -PostgresPort 55438` pass clean + upgrade; sẽ chạy lại full regression ở cuối SHIP-04.
- SHIP-02: danh mục canonical có version, đối chiếu mã từ hai tập Công báo QĐ 19/2025 và cập nhật Đồng Nai theo NQ 237/2026 + NQ 30/2026. Backend validate code/parent; TypeScript sinh từ cùng JSON và CI kiểm tra drift. Không sửa địa chỉ lịch sử. Coverage dùng danh mục, không hard-code số tỉnh/xã. Chi tiết nguồn tại [catalog provenance](../docs/backend/architecture/shipping-administrative-catalog.md).
- Validation SHIP-02: 2 Application coverage tests + 1 Infrastructure catalog test pass; `node scripts/generate-shipping-catalog.mjs --check` và storefront typecheck pass. API expose catalog/coverage được nối trong SHIP-04.
- SHIP-03: calculator thuần tính cân thực theo quantity, làm tròn tổng lên gram đúng một lần, tính bước vượt cân và trả breakdown VND. Từ chối thiếu/sai cân nặng, vượt giới hạn/cỡ số tiền; không fallback miễn phí hoặc 0,5 kg. Snapshot parcel đã có schema từ SHIP-01; nối calculator vào checkout tạo snapshot thuộc SHIP-06/07, không đổi luồng cũ ở lượt này.
- Validation SHIP-03: 13 calculator tests pass (ngưỡng base/step, tổng gram lẻ/quantity, dữ liệu thiếu/sai, overflow/maximum và phí 0 chỉ khi cấu hình tường minh); cùng 2 coverage tests tạo thành 15 Application tests tập trung pass.
- SHIP-04: API Admin-only tạo/sửa draft, clone, validate coverage, preview, publish/replace, retire và history; catalog/ward paging và báo cáo SKU thiếu cân nặng trước enable. Revision + compare active-plan + PostgreSQL advisory lock bảo vệ config trên nhiều replica; DB constraints là lớp bảo vệ bổ sung. Preview đọc toàn bộ revision/zones trong một SQL snapshot, không phát hành quote và không gọi đối tác. Có audit session/reason/before-after, OpenAPI và shared TypeScript contracts. Hướng dẫn: [shipping rates API](../docs/backend/api/shipping-rates.md).
- Validation cuối SHIP-04: `dotnet test WorkspaceEcommerce.slnx --no-restore --configuration Release --verbosity minimal` pass **728/728** (354 Application, 240 Infrastructure, 134 API integration), không skip. Có 46 regression cases mới so với baseline; 16 case API admin gồm phân quyền mọi route, input null/sai, incomplete coverage, concurrent edit/publish, immutable quote/history, rollback trung gian, outage preview và báo cáo thiếu cân nặng.
- EF pending-model check Release/no-build pass; migration clean + upgrade và upgrade với dữ liệu paid/legacy/pending outbox đã pass. Không sửa migration lịch sử.
- Frontend: typecheck toàn workspace pass; 14 Vitest pass; lint pass với 1 warning `react-hooks/incompatible-library` tại checkout đã tồn tại trước task; build hai app pass với cảnh báo bundle >500 kB đã có trước. Catalog generator `--check` pass, đã chuẩn hóa hash CRLF/LF cho Windows/CI.
- Kiểm tra trước commit: scoped diff không có lỗi whitespace; tài liệu UTF-8/link/code fence hợp lệ; tracked runtime secret scan pass (958 file, không in giá trị secrets).
- Browser E2E, load/soak, triển khai production và các luồng mới SHIP-05–14 chưa chạy/triển khai trong phạm vi này. Không coi API preview là bằng chứng đã giao hàng WorkspaceEC end-to-end.

## SHIP-05 validation (2026-10-29)

Admin shipping lazy page, draft zones/rates, coverage, persisted preview, publish/retire confirmation, clone, paged history and weight readiness are implemented. Playwright mock UI: 3/3 pass (edit/preview/publish, revision conflict retaining edits, keyboard/empty/validation). Admin typecheck/build and workspace lint pass; existing storefront watch and bundle warnings remain. This does not enable storefront checkout.

## SHIP-06 validation (2026-10-29)

Server-owned quotes/options, bounded external deadline, default 10-minute validity, subject/input binding, explicit unavailability, audited emergency revocation and unused-quote retention are implemented behind a default-off issuance flag. Three PostgreSQL/API cases pass: local independence/outage, disabled external/geography rejection, admin-only revocation with persisted actor/reason. Acceptance/fulfillment checks follow in SHIP-07/08. New migration adds only nullable quote audit/acceptance fields; no user database was migrated.

## SHIP-07/08 validation (2026-10-29)

Quoted checkout consumes a locked subject/input-bound quote atomically with order, cart removal, stock/coupon reservation, frozen payment amount and selected fulfillment. Exact accepted-request retries return the same order after cart removal. WorkspaceEC work is local; external work keeps the durable outbox, idempotency and provider protocol. Worker pricing/parcel rebuild is legacy-only; new fees and parcels are frozen. Partner creation response rechecks cancellation under the order lock and durably compensates in-flight cancellation. New prepaid dispatch is guarded, local direct delivery transitions remain blocked until SHIP-10. Legacy regressions pass. Focused PostgreSQL coverage includes quote/session/customer tampering, input/weight/payment/expiry/revocation, duplicate requests, last-stock race, rollback after intermediate save, callbacks, replay, cancellation-in-flight and disabled-intake backlog. Local-only DI resolves without partner credentials. Full regression (including SHIP-09 working implementation) passed 752 tests; task-specific tests live with these changes.

## SHIP-09 validation (2026-10-29)

Admin-only exact-amount manual-transfer, COD collection and separate COD-remittance commands persist immutable actor/reference/reason/time facts. Duplicate confirmations are idempotent; conflicting references and partial/excess amounts are rejected. Payment confirmation and eligible work commit together with payment/order/shipment lock order matching cancellation. New COD Delivered events alone do not mark Paid or Completed; full collection completes a previously delivered order through existing history/loyalty semantics. Legacy orders keep their prior workflow. An order-detail payment panel supports review/confirmation and receipt history.

Validation: full backend 752/752 passed (354 Application, 241 Infrastructure, 157 API integration), no skips; frontend 14 Vitest and 5 Playwright shipping/admin scenarios passed, workspace lint/typecheck/build passed. Existing storefront React Hook Form watch warning and >500 kB bundle warnings remain. PostgreSQL clean/upgrade migrations and EF pending-model check passed; catalog drift check passed. No provider-real calls, user/production database migration, production enablement, load/soak or complete storefront shipping E2E were performed. SHIP-10-14 remain release gates.

Commit-isolation check: copied the staged frontend into an ignored isolated directory, installed its own frozen lockfile, and passed admin lint/build plus all 5 Playwright scenarios without the user's unrelated UI/package changes. Shipping route has a hydration loading state. No changes to the original user files were detected outside the three intentional route/menu/order-panel integration files.

## SHIP-10 (2026-11-01)

Implemented WorkspaceEC assignment, confirmed/paid dispatch, delivery, failure/retry and order-workflow cancellation. Row locks/revisions and immutable operation IDs/hashes protect retries and races; private assignment/reason/actor/time audit is separate from customer timeline. COD delivery waits for full collection; prepaid delivery requires Paid. Existing stock/refund/loyalty/history paths remain in use. Focused shipping API suite: 29 cases passed (including 6 new lifecycle/auth/race cases); 5 admin Playwright cases passed including reviewed assignment and existing payment controls. Migration is additive; production intake remains disabled.

## SHIP-11 (2026-11-01)

Storefront reads server capabilities and offers explicit WorkspaceEC/MiniLogistics selection without changing its existing login guard. Query identity includes address/payment/coupon/cart/customer; changes invalidate selection and late responses cannot replace another method. Explicit server zero is distinguished from unknown/failed fees. Expiry and confirmation gate submission; uncertain responses retain the exact request/quote for retry, including when the cart has disappeared. Shipping controls support vi/en. Validation: 17 Vitest and 4 storefront mock Playwright cases passed; storefront lint/typecheck passed. Real browser/API/database/provider validation follows in SHIP-13.
