# Đánh giá Admin Ecommerce: UI/UX và luồng backend

Ngày kiểm tra: **12/11/2026**, trên working tree hiện tại của `D:\Projects\WorkspaceEcommerce`.

**Kết luận:** Admin đã có nền tảng giao diện khá thống nhất, bố cục sạch và nhiều xử lý bàn phím tốt. Tuy nhiên, **chưa nên coi là dashboard vận hành ecommerce hoàn thiện**: có lỗi chặn lưu catalogue, phản hồi bất đồng bộ làm hiển thị nhầm đơn, import đơn thiếu chống trùng và chưa đi qua đầy đủ vòng đời thanh toán. Cần sửa tính đúng đắn của luồng trước khi mở rộng biểu đồ hoặc thay đổi phong cách thiết kế.

Đợt này **không sửa mã ứng dụng, migration, cấu hình shop hoặc test hiện có; không thay đổi dữ liệu shop**. Chỉ bổ sung báo cáo này. Các probe kiểm tra, ảnh và log nằm trong `.tmp/` và `artifacts/` đã được Git ignore. Không commit/push.

## 1. Phát hiện ưu tiên

Quy ước:

- **P1:** xử lý trước khi dùng luồng tương ứng để vận hành thật; có thể chặn công việc hoặc làm sai dữ liệu/tiền/tồn kho/ngữ cảnh đơn.
- **P2:** xử lý trong đợt hoàn thiện vận hành; lỗi tương tác, dữ liệu khó tiếp cận hoặc kiểm thử thiếu.
- **P3:** cải thiện hiệu quả sử dụng và tính nhất quán.
- **Đã tái hiện — trình duyệt/HTTP/Application:** ghi rõ tầng kiểm tra; probe bộ nhớ không chứng minh giao dịch PostgreSQL.
- **Khoảng trống nghiệp vụ:** chức năng chưa khép kín, không đồng nghĩa một guard đang có là sai.
- **Rủi ro có điều kiện:** xác định đường lỗi từ source/EF, chưa tái hiện cạnh tranh trên PostgreSQL trong đợt này.

| ID | Mức | Vấn đề | Bằng chứng |
| --- | --- | --- | --- |
| A01 | P1 | Đổi đơn trong lúc chờ cập nhật có thể đưa dữ liệu đơn A vào drawer của B | Đã tái hiện trên trình duyệt, API giả có trì hoãn |
| A02 | P1 | Tạo/sửa Product và Category gửi sai kiểu `name`/`description` | Payload trình duyệt + 4 request qua HTTP pipeline thật đều `400` |
| A03 | P1 | Import lại cùng file/mã đơn ngoài tạo thêm đơn, trừ kho thêm | Đã tái hiện Application với bộ nhớ giả |
| A04 | P1 | Đơn trả trước mới tạo qua import vẫn đi luồng legacy: chưa trả tiền vẫn hoàn tất, thiếu hạn thanh toán và đường xác nhận | Đã tái hiện phần chuyển trạng thái ở Application; đối chiếu payment/fulfillment source |
| A05 | P1 khi triển khai WorkspaceEC shipping | Không có đường nhập trọng lượng SKU trong Admin/API catalogue | Đối chiếu form, DTO, service và bộ tính cước |
| A06 | P1 khi bật warranty | Đổi bảo hành của dòng mua 1 sản phẩm bị bế tắc ở bước gán unit thay thế | Đã tái hiện guard gán unit ở Application; đối chiếu điều kiện Replace |
| A07 | P2 | Import tiền VND chưa nhất quán: giá lẻ qua preview nhưng commit lỗi; phí ship lẻ được chấp nhận | Đã tái hiện Application |
| A08 | P2 | Bài viết/bình luận bị giới hạn 100; warranty chỉ lấy trang đầu, thiếu đường xem phần còn lại | Xác nhận source API/UI |
| A09 | P2 | Lưu/đóng form ảnh sản phẩm khi upload chưa xong; article dùng cùng cách nối state thiếu guard | Trình duyệt với ảnh sản phẩm; article xác nhận source |
| A10 | P2 | Shipping mất thay đổi chưa lưu, form vẫn thao tác được khi đang lưu, gate preview giữ kết quả cũ | Đã tái hiện trên trình duyệt |
| A11 | P2 | URL Admin không tồn tại hiện màn hình lỗi mặc định dành cho developer | Đã tái hiện trên trình duyệt |
| A12 | P2 | Bộ UI test thiếu payment fixture và chưa bắt được lệch hợp đồng FE/BE | Chạy bộ test hiện có: 15 đạt, 1 bị lỗi fixture |
| R01 | P1 cần kiểm chứng PostgreSQL | Admin sửa/tắt coupon có thể ghi đè bộ đếm sử dụng vừa được checkout cập nhật | Source + EF metadata probe; chưa chạy race PostgreSQL |
| R02 | P1 khi bật warranty, cần kiểm chứng PostgreSQL | Void/Replace không dùng khóa/revision như Activate, có thể tranh chấp trạng thái và audit | Source và EF configuration; chưa chạy race PostgreSQL |
| G01 | P1 trước khi nhận xử lý trả hàng thực tế | Chưa có trọn luồng nhận/kiểm hàng, hoàn một phần, đảo điểm và xử lý bảo hành | Khoảng trống đã tồn tại, xác nhận lại source hiện tại |

### A01 — Drawer Orders hiển thị nhầm đơn khi phản hồi về muộn

**Tái hiện:** mở A → Activity → Update status; giữ phản hồi API → đóng drawer → mở B → trả kết quả A. Drawer đang chọn `ui-order-b` chuyển sang hiện `Order WE-20261014-001` của A.

**Nguyên nhân:** callback mutation ghi `setQueryData(["admin-order", selectedOrderId], updatedOrder)` theo lựa chọn đang hoạt động khi callback chạy. Các callback refresh/retry/cancel shipment cũng dùng `selectedOrderId` theo cùng cách. Drawer vẫn đóng được trong lúc mutation chờ. Xem [OrdersPage][orders-ui].

**Tác động:** người vận hành đọc sai người mua, trạng thái và số tiền trong ngữ cảnh đang chọn; có nguy cơ thực hiện thao tác tiếp theo trên ngữ cảnh không khớp. Probe xác nhận hiển thị/cache sai, không gửi thay đổi tới backend thật.

**Cải thiện:** truyền `{ orderId, request }` cố định vào mutation, cập nhật cache theo ID của request/response; chỉ cập nhật giao diện đang mở khi ID trùng. Áp dụng cho cả thao tác shipment, receipt và các callback phụ thuộc selection.

**Nghiệm thu:** phản hồi A đến trước/sau B đều không đổi nội dung B; đóng drawer hoặc chuyển trang lúc chờ không làm ghi sai cache; error/success chỉ gắn đúng đơn và request.

### A02 — Hợp đồng Product/Category không khớp giữa FE và BE

**Tái hiện:** Admin gửi `name: "Desk"`, product còn gửi `description: "Description"`. Backend yêu cầu `IDictionary<string,string>`, ví dụ `name: {"vi":"Bàn","en":"Desk"}`. Cả POST và PUT cho hai resource đều trả `400` trước khi truy cập database. Không phải lỗi quyền hay mất kết nối database.

**Bằng chứng:** [TypeScript admin DTO][admin-types], [mapper form Product][product-forms], [Category page][categories-ui], [CreateProductRequest][product-request], [CreateCategoryRequest][category-request]. API client chuyển nguyên body, không có lớp chuyển đổi string sang dictionary. Toggle visibility Product/Category cũng gửi lại full request theo các mapper này nên bị ảnh hưởng.

**Cải thiện:** đồng bộ contract hai chiều, giữ dictionary bản dịch trong state; cho sửa theo ngôn ngữ rõ ràng và bảo toàn ngôn ngữ chưa chỉnh. Không vá bằng cách ghi cùng một chuỗi lên mọi ngôn ngữ vì sẽ mất nội dung dịch riêng. Nếu cần, tách status endpoint để toggle không phải gửi lại nội dung.

**Nghiệm thu:** tạo/sửa/toggle Product/Category với API thật; đọc lại VI/EN đúng; sửa giá trị VI không xóa EN; thêm contract test từ payload thực tế của form.

### A03 — Import đơn không chống nhập lặp

**Tái hiện Application:** import cùng CSV có `externalOrderCode=AUDIT-EXT-1` hai lần, mỗi lần 1 SKU × 1. Cả hai commit thành công; có 2 đơn mới, stock từ 10 xuống 8.

`ExternalOrderCode` chỉ được dùng nhóm dòng trong file, ghi vào nội dung lịch sử và trả kết quả. Không có khóa nguồn đơn ngoài/batch bền vững để phát hiện lần nhập trước. Xem [CommitImport][order-import].

**Cải thiện:** lưu nguồn import và external order ID với unique constraint phù hợp; thêm batch/checksum và idempotency cho retry. Preview cần phân biệt mới/trùng/xung đột dữ liệu. Import lặp cùng nội dung trả lại kết quả cũ hoặc skip có giải thích; khác nội dung yêu cầu xử lý xung đột.

**Nghiệm thu:** retry sau timeout, nhập lại file đã thành công và hai commit đồng thời đều không tạo thêm đơn/trừ kho thêm; vẫn cho phép cùng mã ở hai nguồn độc lập nếu quy tắc nguồn cho phép.

### A04 — Đơn import mới chưa đi qua đầy đủ vòng đời tiền và giao hàng

**Tái hiện Application:** import đơn `ManualBankTransfer` → lần lượt Confirmed, Processing, Shipping, Completed đều thành công; payment vẫn `Pending`, `PaymentDueAt=null`.

**Đường lỗi:** import tạo `Order` trực tiếp; không tạo shipping selection/fulfillment như checkout và không áp dụng deadline của [CheckoutOrderFactory][checkout-factory]. [FulfillmentCoordinator][fulfillment] bỏ qua guard khi không có shipping selection. [OrderPaymentService][order-payments] từ chối xác nhận transfer/COD cho đơn không có selection với thông báo legacy, ngoại trừ full refund. Admin không có đường khác để xác nhận chuyển khoản cho đơn import đó.

Giữ nguyên hành vi của **đơn legacy đã tồn tại** là chủ ý của hệ thống. Vấn đề là **đơn mới tạo hôm nay qua import** cũng rơi vào nhánh legacy. [Dashboard query][dashboard-query] cộng tổng mọi đơn Completed, nên trạng thái hoàn tất này còn có thể làm KPI được đọc như doanh thu dù chưa xác nhận tiền.

**Cải thiện:** phân biệt import bán hàng mới với nhập lịch sử. Đơn mới phải dùng cùng invariants về payment, deadline, fulfillment và stock. Import lịch sử cần cờ nguồn, trạng thái/chứng từ rõ ràng và không tự phát sinh thu tiền/giao hàng/trừ kho như đơn mới nếu không đúng mục đích.

**Nghiệm thu:** ma trận COD/transfer/VNPay × nguồn checkout/import mới/legacy; trả trước chưa xác nhận không xuất giao; COD chưa thu không hoàn tất; có đường xác nhận tiền đúng loại; không đưa dữ liệu lịch sử vào outbox ngoài ý muốn.

### A05 — Shipping báo thiếu trọng lượng nhưng Admin không sửa được

[Form variant][variant-form] và [CreateProductVariantRequest][variant-request] không có trọng lượng/kích thước; create/update service cũng không gọi `UpdateDimensions`. SKU tạo qua Admin có `WeightKg=null`. Trang Shipping chỉ đọc weight issues, không có hành động sửa.

[WorkspaceShippingRateCalculator][shipping-calculator] yêu cầu actual weight dương cho mọi dòng, không dùng trọng lượng mặc định. Do đó thiếu đường cấu hình ở Admin là trở ngại thực tế khi bật WorkspaceEC shipping. Không suy rộng kết luận này thành mọi phương thức shipping đều đang hỏng; rollout/config và nhánh MiniLogistics có hành vi riêng.

**Cải thiện:** bổ sung trọng lượng hợp lệ xuyên suốt domain/DTO/API/form; ghi đơn vị rõ ràng, chuyển đổi kg/g nhất quán; dẫn trực tiếp từ weight issue tới đúng SKU. Không gán hàng loạt trọng lượng giả để làm mất cảnh báo.

**Nghiệm thu:** tạo SKU → nhập trọng lượng → hết weight issue → báo giá với số lượng >1 đúng và nhất quán với snapshot khi đặt đơn.

### A06 — Warranty replacement thiếu đường cấp unit thay thế

Với đơn chỉ có một dòng số lượng 1 đã gán unit cũ, gán thêm unit còn hàng vào dòng đó trả Conflict: `All physical units for this order item are already assigned.` Probe Application đã xác nhận. Nhưng `ReplaceAsync` lại yêu cầu unit thay thế đã có entitlement PendingActivation, được gán vào cùng order.

Xem [AssignUnit count guard][warranty-assign] và [Replace conditions][warranty-replace]. Guard chống gán dư khi đóng gói là đúng; chưa có nghiệp vụ cấp unit thay thế tách khỏi quota bán hàng ban đầu. Không nên bỏ guard đếm để làm tính năng Replace chạy.

**Cải thiện:** use case thay thế nguyên tử: chọn unit kho phù hợp, khóa unit/entitlement cũ và mới theo thứ tự thống nhất, ghi quan hệ thay thế, xử lý tồn và giữ ngày coverage theo chính sách. Unit thay thế phải được cấp trong luồng replacement có audit, không cần gán dư vào dòng bán hàng.

**Nghiệm thu:** đổi lần đầu và đổi tiếp lần hai cho đơn quantity=1; retry không nhân đôi quyền lợi; hai người cùng chọn một spare chỉ một người thành công. Điều kiện này áp dụng khi bật warranty; feature tắt trả unavailable là hành vi đúng.

### A07 — Preview và commit import chưa cùng quy tắc tiền

Hai probe Application đã xác nhận:

1. `unitPrice=100000.5`: preview báo 0 dòng lỗi; commit trả Conflict do `OrderItem` yêu cầu VND nguyên.
2. `shippingFee=0.5`: commit thành công, tổng đơn `100000.5 VND` vì `Order.SetShippingFee` chỉ kiểm tra không âm.

Xem [BuildImportPlan/TryParseOptionalMoney][import-validation] và [Order][order-domain]. Đây là hành vi ở domain/Application; persistence PostgreSQL của hai trường hợp chưa được kiểm tra trong phiên này.

**Cải thiện:** dùng cùng quy tắc whole VND tại preview, use case và domain; trả lỗi theo dòng/cột, không để người dùng thấy preview thành công rồi mới lỗi ở commit. Rà cùng lượt email, độ dài trường, số lượng, tổng vượt ngưỡng và định dạng số theo locale; những điểm mở rộng này là tiêu chí kiểm thử bổ sung, chưa kết luận đều đang lỗi.

### A08 — Dữ liệu còn tồn tại nhưng màn hình quản trị không tiếp cận được

- [AdminBlogService][blog-service] lấy tối đa 100 bài và 100 comment, không trả pagination/cursor. UI dùng danh sách này. Bài cũ và comment cũ có thể không xuất hiện; comment chờ duyệt có thể bị đẩy khỏi tập 100 kết quả mà UI đang tải.
- [WarrantiesPage][warranty-ui] lấy 20 registrations, 20 units và 100 plans ở trang đầu; không có pagination/search nối tới API cho các danh sách này. API warranty đã có phân trang, phần thiếu nằm ở UI.

**Cải thiện:** bài viết/comment dùng phân trang server và bộ lọc moderation; warranty nối page/search/status, giữ URL và hiển thị tổng số. Không giải quyết bằng tăng cap lên một con số lớn khác.

**Nghiệm thu:** với >100 bài/comment, >20 units/registrations, mọi bản ghi đều tìm và thao tác được qua UI; approve/reject không làm người duyệt mất vị trí hoặc bỏ sót queue.

### A09 — Upload ảnh chưa khóa toàn bộ thao tác liên quan

Probe trình duyệt: mở Edit product image, trì hoãn upload; cả Save và Close dialog vẫn enabled. Người dùng có thể lưu URL cũ khi ảnh mới chưa lên, hoặc đóng form trước khi callback upload trả về.

[ImageModal][image-modal] không nối `onUploadingChange` của [ImagePickerField][image-picker] vào `busy`/Save. Cover image của [BlogPostModal][blog-modal] cũng thiếu nối state này; phần article mới xác nhận bằng source. Banners đã nối upload state, nên không quy kết mọi form ảnh đều sai.

**Cải thiện:** upload và save có chung trạng thái busy; bảo vệ callback khi form đổi entity/đóng; giữ input khi lỗi, có retry và cleanup asset tạm theo lifecycle hiện tại.

**Nghiệm thu:** đổi ảnh có sẵn rồi bấm Save ngay trên mạng chậm không lưu ảnh cũ; callback của form đã đóng không thay ảnh của form khác; lỗi upload không xóa nội dung đang nhập.

### A10 — Trang Shipping thiếu bảo vệ thay đổi và tính nhất quán của preview

Đã tái hiện với fixture trì hoãn/đổi input:

- Chỉnh tên draft → New rate plan: mất nội dung cũ ngay, không có xác nhận dù đã có nhãn Unsaved changes.
- Khi PUT Save draft đang chờ: input Name và New rate plan vẫn enabled; người dùng có thể đổi ngữ cảnh trong khi save cũ chưa hoàn tất.
- Preview thành công → đổi trọng lượng về 0: kết quả preview hiện tại bị xóa nhưng Publish plan vẫn enabled. Parent giữ preview cũ cùng revision, child chỉ reset mutation cục bộ.

Xem [ShippingRatesPage][shipping-ui], [RatePlanEditor][shipping-editor] và [RatePlanTools][shipping-tools]. Backend vẫn kiểm tra coverage/revision khi publish; probe này **không chứng minh server chấp nhận rate plan sai**, mà chứng minh gate và thông tin xác nhận trên UI không còn khớp trạng thái đang thấy.

**Cải thiện:** chặn/confirm rời draft bẩn; khóa editor và selection trong khi mutation pending hoặc xử lý response theo operation ID; reset preview ở cùng một chủ sở hữu state, chỉ cho publish khi bằng chứng preview còn phù hợp.

### A11 — Error/404 chưa phải trải nghiệm quản trị hoàn chỉnh

Mở `/unknown-admin-page` hiện `Unexpected Application Error!`, `404 Not Found`, lời hướng dẫn developer. [Router][admin-router] chưa có trang không tìm thấy/error boundary riêng.

**Cải thiện:** trang 404 có quay lại Dashboard, lỗi render có retry và mã hỗ trợ, giữ navigation khi phù hợp; không hiện stack trace ở production. Tách 401 hết phiên, 403 thiếu quyền, 404 không có resource, 409 dữ liệu đã đổi và lỗi mạng để hướng dẫn đúng hành động.

### A12 — Kiểm thử UI đang tạo cảm giác an toàn cao hơn phạm vi thực

Lần chạy `test:ui:admin`: **15/16 đạt**. Ca nested receipt/cancellation bị lỗi `Unexpected admin fixture request: GET /api/admin/orders/ui-order/payment`. Fixture chưa mô phỏng endpoint payment đã được UI gọi; chưa đủ cơ sở kết luận thao tác hủy shipment thật bị hỏng.

Fixture Product/Category dùng `name` chuỗi giống TypeScript hiện tại; UI test không gọi API thật nên không phát hiện A02. Danh sách smoke của suite cũng chưa bao gồm Shipping. Các probe mới trong đợt audit đã kiểm tra Shipping riêng nhưng không thay đổi bộ test hiện có.

**Cải thiện:** sửa fixture theo response/contract thật trong đợt sửa code; thêm test hợp đồng FE→HTTP, một bộ hành trình Admin→API→PostgreSQL tách biệt và các ca phản hồi đảo thứ tự. Giữ fixture test cho layout/a11y vì vẫn có ích.

### R01 — Coupon có thể mất bộ đếm khi Admin sửa đồng thời với checkout

[AdminCouponService][coupon-service] đọc coupon không khóa, sau đó gọi `Update(coupon)` cho cả entity khi edit/toggle/deactivate. Checkout có khóa coupon để reserve usage nhưng khóa phía checkout không bảo vệ một entity đã được Admin đọc trước đó.

Interleaving có thể xảy ra: Admin đọc UsedCount=0 → checkout reserve và commit UsedCount=1 → Admin save entity cũ, ghi lại UsedCount=0. EF probe không cần database xác nhận `UsedCount.IsModified=true` sau cùng cách `Update`, và model không có concurrency token. Điều này có thể mở lại quota đã tiêu thụ hoặc làm sai thống kê.

**Trạng thái:** rủi ro có bằng chứng source/EF; **chưa tái hiện hai transaction thật**, vì Docker không sẵn sàng. Không coi probe metadata là kiểm chứng isolation/locking PostgreSQL.

**Cải thiện:** transaction và khóa cùng coupon khi đổi điều kiện/targets/status; chỉ ghi các trường thực sự sửa, hoặc concurrency token/revision phù hợp. Kiểm tra lại usage limit sau khóa.

**Nghiệm thu:** checkout chạy cùng edit/toggle/delete-deactivate; UsedCount/redemption/release nhất quán, không vượt quota và không có thời điểm đọc bộ target trộn giữa hai phiên bản.

### R02 — Warranty Void/Replace chưa cùng mức bảo vệ cạnh tranh với Activate

Activate dùng coordinator khóa unit và entitlement trong transaction. [VoidAsync/ReplaceAsync][warranty-replace] đọc entity bằng query thường rồi ghi toàn entity; configuration không có concurrency token để từ chối bản ghi cũ.

Hai request Void/Replace hoặc hai Replace trên cùng entitlement có thể cùng vượt qua kiểm tra ban đầu; save cuối ghi đè trạng thái/quan hệ, trong khi audit và coverage đã ghi bởi request trước. Một `SaveChanges` có tính nguyên tử không tự ngăn được hai request cùng quyết định từ trạng thái cũ.

**Trạng thái:** rủi ro có điều kiện khi bật feature; chưa kiểm chứng race PostgreSQL trong đợt này.

**Cải thiện:** khóa/reload các unit và entitlement liên quan theo thứ tự cố định, dùng revision/idempotency, ghi audit cùng transaction; không ghi đè toàn entity bằng state cũ.

### G01 — Chưa khép kín nghiệp vụ trả hàng và ngoại lệ thanh toán

Customer return request hiện chỉ tạo yêu cầu chờ duyệt; Admin thấy thông tin trong chi tiết đơn. Direct Returned bị chặn có chủ ý để không giả định đã nhận hàng/hoàn tiền. Full refund audit đã có nhưng không phải giải pháp cho mọi ngoại lệ.

Còn thiếu queue xử lý return, duyệt/từ chối có lý do, nhận và kiểm từng item/unit, quyết định nhập kho bán lại/cách ly, hoàn tiền theo item/một phần, xử lý thiếu/thừa tiền, đảo điểm có khoản đã tiêu và void/đổi warranty. Không nên khắc phục bằng cách mở lại nút Returned.

**Cải thiện:** triển khai RMA theo item/unit, có số lượng và số tiền đối chiếu; phân biệt refund cần thực hiện với refund đã đối chiếu chứng từ. Các thao tác nhạy cảm phải có reason, actor, thời điểm và idempotency.

## 2. Đánh giá UI/UX theo từng page

Skill áp dụng: [ui-ux-pro-max](C:/Users/Admin/.agents/skills/ui-ux-pro-max/SKILL.md), ưu tiên accessibility, feedback, form, navigation, responsive và khả năng đọc dữ liệu. Đánh giá trên implementation đang có; không đề nghị thay framework hay thiết kế lại toàn bộ.

| Page | Điểm đang tốt | Điểm cần hoàn thiện |
| --- | --- | --- |
| Login | Nhãn rõ, autocomplete, lỗi giữ input, nút có trạng thái chờ | Thông báo hết phiên và giữ đích đến/draft nhất quán; nhãn/ngôn ngữ phù hợp shop |
| Dashboard | KPI gọn, label all-time và tối đa 10 stock alerts trung thực; có drill-down đơn/tồn kho | Thiếu bộ thời gian và hàng đợi việc cần làm; completed order value không thay thế được tiền thực thu/net revenue |
| Products | Thumbnail, giá/tồn kho, mobile cards, tab variants/images/specifications, action menu rõ | A02/A05/A09; thiếu search/filter server; nhập tồn trực tiếp chưa có reason/ledger dễ kiểm tra |
| Categories | Phân cấp, chặn chọn cha là chính nó/descendant ở UI, backend có guard tuần tự | A02; bảng mobile phải cuộn ngang; nguy cơ cây quá dài, chưa có search/collapse phù hợp dữ liệu lớn |
| Orders | Nhóm thông tin tốt, drawer tabs, phân trang/filter URL, receipt tách khỏi VAT invoice | A01/A03/A04/G01; bộ lọc tiền/giao hàng/return còn thiếu; next status hiện theo order status thay vì capability của cả payment + provider |
| Shipping | Có revision, coverage, preview, lịch sử và xác nhận publish/retire | A05/A10; trang dài, nhiều dữ liệu kỹ thuật, chọn tỉnh dạng checklist dài; thiếu đường sửa từ readiness tới SKU |
| Coupons | Search/status/effective time có label; hiện quota/redemptions và target; giữ bộ lọc | R01; cần giải thích UsedCount hiện hành so với redemption lịch sử sau hủy; picker tải toàn catalogue |
| Banners | Thumbnail và nút rõ; title tùy chọn; upload được bảo vệ | Nên có preview đúng từng tầng homepage, kích thước/khung crop và tên vị trí dễ hiểu thay cho chỉ nhập Sort order; đây là cải thiện, chưa kết luận lỗi lưu banner |
| Articles | Bảng gọn, publish/moderation tách tab, có thông tin trạng thái comment | A08/A09; thiếu hàng đợi moderation toàn cục, search, preview bài; textarea HTML hiện dành cho người có kỹ năng kỹ thuật |
| Reviews | Rating có chữ/số, nội dung dài mở thêm, xóa có xác nhận | Thiếu search/filter theo sản phẩm/rating/ngày và lý do/audit moderation; chưa có luồng phản hồi khách |
| Warranties | Feature tắt hiện unavailable đúng; có tabs và reason cho thao tác nhạy cảm | A06/A08/R02; còn nhập UUID variant/order item/spare và coverage dạng `FRAME|Frame|60`; khó dùng với nhân sự vận hành |

### Những tinh chỉnh cần thiết để đạt trải nghiệm chuyên nghiệp

1. **Dashboard phục vụ hành động:** hiển thị đơn chờ xác nhận, tiền chờ đối soát, refund pending, return chờ duyệt, giao thất bại, outbox dead-letter. Mỗi số mở đúng danh sách đã lọc. Backend đã có operations/outbox API nhưng chưa có page tương ứng trong Admin. Thêm chỉ số theo kỳ chỉ khi có API với định nghĩa và timezone rõ ràng.
2. **Tìm kiếm và hiệu năng catalogue:** API Admin products hiện chỉ phân trang. Picker coupon/article tải lần lượt toàn bộ catalogue 100 sản phẩm/trang và cả variants/images/specs, gây tăng dữ liệu theo quy mô. Bổ sung lookup/search nhẹ cho picker, tìm SKU/ID và giữ lựa chọn qua trang; dashboard mở đúng SKU bằng lookup thay vì dò nhiều trang.
3. **Mobile cho người xử lý đơn:** ở 390px, bảng Orders không làm tràn document nhưng cột tổng tiền/trạng thái nằm ngoài phần thấy ngay. Đây là lựa chọn bảng có cuộn hợp lệ, không phải lỗi overflow; nên bổ sung card/summary hoặc cột ưu tiên để xem mã + khách + tiền + trạng thái trong một lần quét.
4. **Ngôn ngữ và form:** phần lớn Admin tiếng Anh nhưng Shipping/menu trộn Việt/Anh. Chọn ngôn ngữ mặc định thống nhất; format ngày/giờ/tiền nhất quán. Dùng bộ chọn đơn/SKU/unit thay UUID, editor coverage có trường riêng và preview bài viết. Error summary, inline errors và focus tới lỗi đầu cần thống nhất, nhất là form Shipping/Warranty dài.
5. **Audit và quyền vừa với shop cá nhân:** chưa cần hệ RBAC phức tạp nếu chỉ một chủ shop, nhưng khi có nhân viên cần giới hạn quyền sửa tồn, xác nhận tiền, hoàn tiền và xóa nội dung. Thêm ledger điều chỉnh tồn có lý do và đối chiếu trước/sau. MFA/session revoke là cải thiện bảo vệ Admin cần cân nhắc, không coi việc chưa có chúng là bằng chứng đã bị xâm nhập.

## 3. Những phần backend có nền tảng đúng

Đọc source và chạy test hiện có cho thấy các cơ chế cần giữ khi sửa:

- Controller nghiệp vụ Admin có role `Admin`; login đi qua credential validation, có rate limit tại middleware. Không thấy lý do thay cơ chế auth chỉ để xử lý lỗi form.
- Stock variant update đã có khóa và expected quantity; không nên quay lại kiểu ghi tồn theo bản đọc cũ để giải quyết xung đột UI.
- Category có guard cha/con, delete có child/product bị từ chối; Product có lịch sử order không cho hard delete. Đây là behavior cần giữ. Cạnh tranh chỉnh cây chưa được kiểm chứng PostgreSQL trong phiên này.
- Order status có domain transition và audit; cancellation dùng luồng chung xử lý stock/coupon/payment. Guard không nhận Returned trực tiếp là chủ ý bảo toàn dữ liệu.
- Payment confirmations kiểm tra amount/currency và chứng từ, tách COD collection/remittance; full refund chỉ ghi nhận việc đã hoàn có bằng chứng. Không thay bằng một nút tự gán Paid/Refunded.
- Shipping rate có revision, kiểm tra active plan/coverage, publish/retire và audit; fulfillment/provider có outbox và tracking workflow. Vấn đề A10 thuộc state UI, không cần bỏ CAS/revision ở backend.
- Review create/delete đã có product lock khi cập nhật aggregate; warranty activation có khóa/ownership và identifier masking/HMAC; feature flag phải được giữ.

Các điểm trên không phải chứng nhận toàn bộ hệ thống đã chạy đúng end-to-end hôm nay. Đặc biệt, provider thật, PostgreSQL races và dữ liệu lớn chưa được xác minh trong phiên này.

## 4. Kết quả kiểm tra và giới hạn bằng chứng

| Kiểm tra | Kết quả trong đợt này | Giới hạn |
| --- | --- | --- |
| UI suite hiện có, Chromium | 15 đạt / 1 lỗi fixture payment | API giả; không chứng minh luồng database/provider |
| 9 page trong suite, 1440/768/390/320px | Không có document overflow hoặc axe A/AA violations trong trạng thái đã quét | Không phải chứng nhận WCAG toàn bộ ứng dụng |
| Shipping editor bổ sung, 1440/390/320px | Không có document overflow/axe violations ở fixture đã quét | Catalogue 2 tỉnh đại diện; chưa stress toàn bộ cấu hình thật |
| Browser probes bổ sung | Xác nhận A01, A09, A10, A11 và payload A02 | Toàn bộ request mutation được fixture xử lý, không gửi vào DB shop |
| Application tests hiện có | **357/357 đạt** | Bộ nhớ giả không chứng minh EF SQL/isolation |
| Infrastructure tests hiện có | **241/241 đạt** | Không thay thế API→PostgreSQL/provider chạy thật |
| API integration suite hiện có | 14 kiểm tra không cần PostgreSQL đạt; 164 bị chặn bởi fixture Docker | Không báo 164 này thành lỗi nghiệp vụ; không coi là suite đạt |
| Probe mới tạm trong `.tmp` | **10/10 xác nhận hành vi hiện tại**: 4 HTTP contract + 5 Application + 1 EF metadata | Nhiều probe cố ý assert hành vi lỗi đang tồn tại; không có nghĩa đã sửa lỗi |
| Admin lint/typecheck/build | Đạt | Bundle entry khoảng **563.08 kB**, gzip 175.58 kB; còn cảnh báo chunk >500 kB |
| `git diff --check` | Đạt, có cảnh báo LF/CRLF trên working tree sẵn có | Không chỉnh format hàng loạt |

**Môi trường:** Docker Desktop có process nhưng `docker desktop status` báo `stopping`; engine/API/PostgreSQL local không sẵn sàng. Kiểm tra trạng thái/start trả already running không khôi phục engine. Không reset/restart cưỡng bức Docker, không xóa volume hoặc thay cấu hình máy trong phạm vi audit này. Frontend được kiểm tra qua Vite và API fixtures tách biệt. HTTP probe dùng TestServer thật của API, bỏ hosted workers và dùng connection trỏ cổng không có database để bảo đảm A02 bị từ chối ở model binding trước truy cập dữ liệu.

**Chưa xác minh:** thao tác ghi trên PostgreSQL, race coupon/warranty/category, provider MiniLogistics/VNPay và đối soát ngân hàng thật, gửi email thực, người dùng thật/thiết bị cảm ứng/screen reader thủ công, load test, khôi phục backup. Các kết quả test lịch sử ở báo cáo khác không được tính là kiểm tra thành công trong phiên này.

### Artifact để xem lại

- [Facts UI/payload/Shipping](D:/Projects/WorkspaceEcommerce/artifacts/frontend/admin/audit-20261112/facts.json).
- [Facts phản hồi Orders và upload](D:/Projects/WorkspaceEcommerce/artifacts/frontend/admin/audit-20261112/races.json).
- [Ảnh drawer bị phản hồi cũ ghi đè](D:/Projects/WorkspaceEcommerce/artifacts/frontend/admin/audit-20261112/order-switch-stale-response.png).
- [Shipping desktop](D:/Projects/WorkspaceEcommerce/artifacts/frontend/admin/audit-20261112/shipping-1440.png), [Shipping mobile](D:/Projects/WorkspaceEcommerce/artifacts/frontend/admin/audit-20261112/shipping-390.png), [404](D:/Projects/WorkspaceEcommerce/artifacts/frontend/admin/audit-20261112/404.png).
- [Probe và kết quả TRX](D:/Projects/WorkspaceEcommerce/.tmp/admin-audit/results/verified-probes.trx), [Application TRX](D:/Projects/WorkspaceEcommerce/.tmp/admin-audit/results/application.trx), [Infrastructure TRX](D:/Projects/WorkspaceEcommerce/.tmp/admin-audit/results/infrastructure.trx).
- Bộ UI hiện có ghi ảnh/trace vào `D:\Projects\WorkspaceEcommerce\artifacts\frontend\admin\ui\verified\`; tên thư mục `verified` là cấu hình sẵn có, không có nghĩa toàn bộ suite lần này đạt.

## 5. Thứ tự xử lý đề xuất — chưa thực hiện

| Phase | Task đề xuất | Phạm vi và tiêu chí hoàn tất |
| --- | --- | --- |
| 1 — Chặn sai dữ liệu/ngữ cảnh | ADM-01: A01; ADM-02: A02; ADM-03: A03 | Sửa callback theo order ID bất biến; đồng bộ DTO đa ngôn ngữ; khóa chống trùng import. Có HTTP/DB tests và ca phản hồi đảo thứ tự |
| 2 — Đồng bộ vòng đời commerce | ADM-04: A04/A07; ADM-05: R01; ADM-06: A05 | Phân loại import mới/lịch sử; payment/expiry/fulfillment đúng nguồn; quy tắc whole VND chung; coupon race PostgreSQL; quản trị actual weight end-to-end |
| 3 — Khép kín hậu mãi | ADM-07: G01; ADM-08: A06/R02 | RMA theo item/unit, đối soát refund/điểm/kho/warranty; replacement có unit provision và transaction; không mở feature khi luồng chưa đủ |
| 4 — Hoàn thiện tương tác và khả năng tiếp cận dữ liệu | ADM-09: A08; ADM-10: A09/A10/A11 | Pagination/search moderation/warranty; upload busy; draft dirty guard; state preview; error/404; dữ liệu cũ/mạng chậm không gây mất công việc |
| 5 — Dashboard phục vụ vận hành | ADM-11: queue/KPI/filters; ADM-12: lookup/mobile/ngôn ngữ | Queue có drill-down; định nghĩa tiền và kỳ thời gian rõ; lookup server; thao tác thông dụng ít bước; tiếng Việt/format và mobile nhất quán |
| Xuyên suốt | ADM-QA: A12 + toàn bộ regression | Sửa fixture trong đợt triển khai; chạy UI→HTTP→PostgreSQL với DB tách biệt; ma trận payment × provider × order state; fault/retry/out-of-order tests |

**Điều kiện trước nghiệm thu vận hành:** xử lý A01–A05 và R01 cho các luồng bật thực tế; A06/R02 trước bật warranty; G01 trước cam kết trọn luồng trả hàng. Khôi phục môi trường PostgreSQL và chạy lại các ca đang bị chặn. Đánh giá lại với người trực tiếp xử lý đơn sau khi các luồng đúng; không lấy việc không có axe violation hoặc build thành công làm tiêu chí duy nhất.

## Tham chiếu mã nguồn

[orders-ui]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/orders/OrdersPage.tsx:213
[admin-types]: D:/Projects/WorkspaceEcommerce/frontend/packages/api-types/src/admin.ts:34
[product-forms]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/products/productTypes.ts:72
[categories-ui]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/categories/CategoriesPage.tsx:125
[product-request]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Catalog/Products/CreateProductRequest.cs:7
[category-request]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Catalog/Categories/CreateCategoryRequest.cs:7
[order-import]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Ordering/AdminOrderService.cs:293
[import-validation]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Ordering/AdminOrderService.cs:373
[checkout-factory]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Ordering/CheckoutOrderFactory.cs:35
[fulfillment]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Shipments/FulfillmentCoordinator.cs:65
[order-payments]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Payments/OrderPaymentService.cs:35
[dashboard-query]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Infrastructure/Persistence/Queries/EfAdminDashboardQuery.cs:19
[variant-form]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/products/components/VariantModal.tsx:77
[variant-request]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Catalog/Products/CreateProductVariantRequest.cs:3
[shipping-calculator]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Shipments/WorkspaceShippingRateCalculator.cs:18
[warranty-assign]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Warranties/AdminWarrantyService.cs:505
[warranty-replace]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Warranties/AdminWarrantyService.cs:657
[order-domain]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Domain/Modules/Ordering/Order.cs:200
[blog-service]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Blogs/AdminBlogService.cs:19
[warranty-ui]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/warranties/WarrantiesPage.tsx:61
[image-modal]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/products/components/ImageModal.tsx:47
[image-picker]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/components/media/ImagePickerField.tsx:35
[blog-modal]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/blogs/components/BlogPostModal.tsx:203
[shipping-ui]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/shipping/ShippingRatesPage.tsx:81
[shipping-editor]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/shipping/RatePlanEditor.tsx:110
[shipping-tools]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/pages/shipping/RatePlanTools.tsx:53
[admin-router]: D:/Projects/WorkspaceEcommerce/frontend/apps/admin/src/app/router.tsx:7
[coupon-service]: D:/Projects/WorkspaceEcommerce/src/WorkspaceEcommerce.Application/Modules/Coupons/AdminCouponService.cs:231
