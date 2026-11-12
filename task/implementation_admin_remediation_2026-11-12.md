# Hoàn thiện Admin Ecommerce — 12/11/2026

Phạm vi: xử lý báo cáo `report_admin_uiux_backend_2026-11-12.md`, đồng thời bổ sung tìm kiếm tổng quát và tìm kiếm/bộ lọc Products. Thực hiện trên working tree hiện tại, giữ các thay đổi có sẵn. Sau nghiệm thu, người dùng yêu cầu commit/push và khởi động chương trình; code đã được push lên `origin/main` tại commit `8a6bbc7`.

Skill áp dụng: [ui-ux-pro-max](C:/Users/Admin/.agents/skills/ui-ux-pro-max/SKILL.md) cho layout, accessibility và tương tác; [React best practices](C:/Users/Admin/.codex/plugins/cache/openai-curated-remote/vercel/0.54.1/skills/react-best-practices/SKILL.md) cho query/cache, vòng đời component và phân trang. Giữ modular monolith/direct EF và UI primitives hiện có.

## 1. Tìm kiếm và dashboard

- Thanh tìm kiếm ở header mọi page Admin, có `Ctrl/⌘ K`, debounce, trạng thái chờ/lỗi/rỗng và điều hướng bàn phím. Backend tìm sản phẩm/bản dịch/SKU, đơn/mã tracking/khách/điện thoại, danh mục, coupon và bài viết; giới hạn năm kết quả mỗi nhóm. Link mở đúng bản ghi qua URL, không dò catalogue từng trang.
- Products có search, category, visibility, inventory, featured, khoảng giá VND, sort và reset. Truy vấn/filter chạy ở server; URL giữ lựa chọn khi reload/Back. Lọc category là category được chọn; giá áp dụng trên các SKU active, sort giá dùng mức giá thấp nhất trong sản phẩm.
- Picker sản phẩm của coupon/article tìm kiếm và phân trang server, giữ các sản phẩm đã chọn ngoài trang hiện tại. Tên các lựa chọn được lấy bằng tập ID tối đa 100; không tải tuần tự toàn catalogue.
- Dashboard có drill-down tới chuyển khoản chờ xác minh, COD chờ đối soát, giao thất bại, return, refund pending, chênh lệch tiền, bình luận chờ duyệt và background failure records.
- KPI mới `NetCollectedRevenue`: all-time, đơn Completed/Returned có `PaidAt`, trừ tiền hoàn đã ghi nhận, theo tỷ giá snapshot của đơn. Đây là tiền đã được người vận hành/gateway xác nhận trong hệ thống, không phải lợi nhuận hoặc số dư ngân hàng. Field `TotalRevenue` cũ vẫn giữ để tương thích; UI dùng KPI mới.
- Thêm Operations: summary outbox, latest 50/100 terminal failures, mở đơn liên quan và xác nhận trước replay. Các failed records đã replay vẫn được giữ và có thể nằm trong số liệu; không coi số đó là toàn bộ công việc chưa giải quyết.

## 2. Các lỗi trong báo cáo và kết quả xử lý

| ID | Thay đổi |
| --- | --- |
| A01 | Cache cập nhật theo ID đơn trong response; các vùng payment/return/delivery và tabs tách state theo order ID. Kiểm thử giữ response A tới khi đã mở B không thay nội dung B. |
| A02 | Product/Category gửi dictionary VI/EN; giữ bản dịch khác; toggle dùng dữ liệu gốc. Fixture cũng dùng đúng contract, có HTTP/PostgreSQL round-trip và kiểm tra payload trình duyệt. |
| A03 | Persist source + external code + fingerprint; unique index, advisory transaction lock theo source. Retry cùng nội dung skip; khác nội dung báo lỗi ở preview/commit. Không trừ kho thêm. |
| A04 | Import mới được đánh dấu manual fulfillment, có hạn thanh toán trả trước và đường xác nhận transfer/COD. Chưa xác nhận tiền không xuất giao trả trước/hoàn tất COD. Không tự book carrier cho đơn nhập thủ công. |
| A05 | Nhập/sửa actual weight kg và kích thước cm xuyên DTO/API/form. Client cũ giữ dimensions khi không yêu cầu thay; không gán trọng lượng giả. |
| A06 | Replacement dùng spare Available cùng SKU, không cần gán dư quota quantity=1. Cấp spare, trừ kho và tạo coverage trong cùng transaction; đổi lần tiếp theo dùng entitlement đang active. |
| A07 | Preview/commit/domain cùng quy tắc VND nguyên, giới hạn số lượng/độ dài/số tiền. VNPay import bị từ chối vì không có gateway session; dùng checkout cho VNPay. |
| A08 | API/page riêng có search và phân trang cho articles/comments; moderation toàn cục. Warranty có search/phân trang từng tab và lookup SKU/order item/spare; coverage thành các field riêng. |
| A09 | Upload sản phẩm/article khóa Save/Cancel/Close/Escape tới khi xong; bỏ callback đổi giá trị sau unmount. |
| A10 | Shipping có xác nhận bỏ draft khi đổi plan/điều hướng, beforeunload, khóa field lúc pending, invalidation preview theo đầu vào. Reload revision giải thích rõ việc thay draft. |
| A11 | Màn hình 404/error có đường về dashboard/retry, không hiện stack dành cho developer. |
| A12 | Sửa payment fixture và endpoints phân trang; bổ sung regressions. Typecheck cả fixtures/spec Admin trước UI suite, thêm `@types/node` đúng phiên bản 22.18.0 đã có trong lockfile. |
| R01 | Admin sửa/status/delete coupon khóa và reload coupon trong transaction, chỉ ghi các field thay đổi. Race PostgreSQL giữ được UsedCount mới. |
| R02 | Void/Replace khóa/reload unit và entitlement theo thứ tự thống nhất, idempotent retry; audit/coverage/stock cùng transaction. Chặn replacement khi coverage đều hết hạn. |
| G01 | Bổ sung RMA item/unit, nhận/kiểm hàng, disposition, partial refund, điểm đã tiêu, warranty và đối soát thiếu/thừa tiền; chi tiết bên dưới. |

Bổ sung UI: Orders dùng card trên mobile để thấy ngay mã, khách, tổng tiền, order/payment status. Có payment/operational queue filters. Điều chỉnh tồn thủ công yêu cầu expected count + reason, lưu trước/sau/actor/time; có history phân trang trong form SKU, giữ audit khi ngừng bán sản phẩm.

## 3. Quy tắc nghiệp vụ đã triển khai

### Import

- Import tạo **đơn bán mới**, giữ tồn và giao thủ công; không phải chức năng backfill đơn lịch sử. Source mặc định `manual`, nên dùng mã nguồn ổn định cho từng kênh.
- Source + external order code là identity; fingerprint bảo vệ retry. Cùng code ở hai nguồn khác nhau được phép. File tối đa 1.000 dòng; preview phân biệt đã nhập và xung đột.
- COD phải xác nhận đủ tiền trước hoàn tất; chuyển khoản có deadline và phải xác nhận trước xuất giao. Xác nhận collection và remittance vẫn tách nhau.
- Đơn cũ không có import marker/shipping selection không tự được đổi nghiệp vụ. Nếu cần nhập lịch sử, phải có luồng đối soát/backfill được xác định riêng.

### Trả hàng và hoàn tiền

- Chỉ đơn VND đã trả tiền, Completed, có completion timestamp và còn trong chính sách bảy ngày; yêu cầu khách gửi đúng hạn vẫn chờ duyệt được. Reject request là quyết định cuối cho yêu cầu đó, không bị bỏ qua bằng cách tạo case Admin.
- Approval theo số lượng; chưa tăng tồn/chưa ghi hoàn tiền. Khi hàng về, ghi đúng số nhận và số đã kiểm có thể bán lại; phần còn lại cách ly. Nhận thiếu có thể mở case khác cho số lượng còn lại.
- Unit có serial/IMEI phải được chỉ định đúng, vào **quarantine**, void entitlement và không tăng kho bán được. Hiện chưa có quy trình reprovision để bán lại unit trả về.
- Hoàn theo giá/chiết khấu snapshot, phân bổ tích lũy VND nguyên để không hụt/thừa do làm tròn. Phí ship chỉ được xét hoàn khi nhận toàn bộ đơn; phần chiết khấu vượt giá hàng giảm tiền ship có thể hoàn. Tổng hoàn không vượt tổng đã chốt.
- Partial return giữ Completed; nhận đủ toàn bộ hàng mới Returned. Tiền chuyển RefundPending → PartiallyRefunded/Refunded sau khi ghi chứng từ đã hoàn. Mỗi case có revision, retry identity và audit.
- Đảo điểm theo phần hàng đã hoàn tiền; điểm đã tiêu thành `PointsDebt`, không làm âm điểm khả dụng. Earning sau đó bù debt trước; tính lại tier. Không xóa voucher/transactions cũ.
- Unit assignment được kiểm tra lại sau khi lấy order lock: nếu replacement/packing vừa thêm unit trong lúc đợi, yêu cầu reload thay vì chạm unit chưa khóa hoặc deadlock.
- Không tự chuyển khoản/hoàn VNPay/gọi carrier trả hàng. Người vận hành kiểm chứng giao dịch thực và hàng thực trước khi ghi nhận. Thông báo khách đi qua email outbox hiện có.

### Thu thiếu/thừa

- Ghi actual receipt, reference, reason, actor/time trước khi xác nhận đủ tiền. Chỉ một exception đang mở cho một đơn; API không gán Paid khi còn chênh lệch chưa đối soát.
- Thu bù đúng khoản thiếu hoặc hoàn đúng phần thừa với reference giao dịch riêng. Với đơn đang mở, settlement và full collection commit nguyên tử qua use case payment hiện có.
- Nếu hủy/expiry xảy ra trước, phải hoàn đúng **số thực nhận**; không đánh dấu đơn hủy thành Paid. Retry settlement không thêm receipt.
- Áp dụng cho manual transfer pending và COD đang giao/unpaid của luồng mới; VNPay tiếp tục xác minh qua callback gateway. Đây không phải tích hợp ngân hàng tự động.

## 4. Kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| Release build toàn solution | Đạt, 0 warning / 0 error |
| Application tests | **357/357** |
| Infrastructure tests | **241/241** |
| API integration tests | **199/199**, PostgreSQL Testcontainers; gồm **21** ca mới của đợt này |
| Admin UI suite | **29/29**, Chromium; gồm Shipping và RMA |
| Storefront unit tests | **19/19** |
| Frontend typecheck | Đạt cả hai app, shared packages và Admin E2E fixtures/spec |
| Frontend lint/build | Đạt; còn cảnh báo bundle >500 kB |
| Migration | Đạt clean create và upgrade tới `20261112015609_AuditManualStockAdjustments`; model không còn pending changes |
| Bảo toàn audit khi rollback | PostgreSQL regression xác nhận Down không xóa stock audit đã ghi |
| Diff/encoding | `git diff --check` đạt; không phát hiện ký tự Unicode lỗi mới; không mất file gốc trong baseline |

UI quét 10 page ở 1440/768/390/320px: không có document overflow hoặc axe A/AA violations trong các trạng thái đã kiểm tra. Đã xem lại ảnh Dashboard, Products desktop và Orders mobile. Không suy kết quả này thành chứng nhận WCAG toàn ứng dụng.

Log và ảnh:

- [Backend cuối](../.tmp/admin-remediation-20261112/full-tests-final.log), [TRX cuối](../.tmp/admin-remediation-20261112/final-full-results/).
- [UI cuối](../.tmp/admin-remediation-20261112/admin-ui-final.log), [ảnh/trace](../artifacts/frontend/admin/ui/verified/).
- [Typecheck](../.tmp/admin-remediation-20261112/typecheck-final.log), [lint](../.tmp/admin-remediation-20261112/frontend-lint-final.log), [build](../.tmp/admin-remediation-20261112/frontend-build-final.log), [storefront tests](../.tmp/admin-remediation-20261112/storefront-tests.log).
- [Migration clean/upgrade](../.tmp/admin-remediation-20261112/migration-verification.log), [model check](../.tmp/admin-remediation-20261112/model-check.log).

Các lượt UI đầu từng lỗi do fixture cũ/locator và một dữ liệu order lọt vào fixture catalogue; đã sửa và chạy lại toàn suite đạt 29/29. Lượt migration/PG đầu của audit trước bị Docker chặn; đợt triển khai này đã khôi phục engine và chạy kiểm tra thật trên DB tách biệt. Không dùng các probe cố ý assert lỗi cũ làm bằng chứng đã sửa.

Các ca mới kiểm tra HTTP → service → PostgreSQL thật qua Testcontainers tách biệt: localized catalogue/dimensions/filter/search, import retry/race/xung đột/VND/payment lifecycle, coupon usage race, RMA approval/receipt/refund races và làm tròn, points debt, serialized quarantine, warranty replacement lần hai/void race/hết hạn, chênh lệch tiền/hủy đơn, stock audit/CAS và articles/comments ngoài mốc 100.

UI dùng API fixtures và chặn mutation tới shop thật. Không thay thế kiểm tra provider thực, đối soát ngân hàng, email SMTP thực, thiết bị cảm ứng hoặc screen reader thủ công. Build vẫn cảnh báo bundle >500 kB; chưa thực hiện load test hoặc đo Core Web Vitals.

## 5. Áp dụng và giới hạn

Ba migration mới:

1. `20261112012901_AdminOperationsRemediation`
2. `20261112015210_PaymentDifferenceReconciliation`
3. `20261112015609_AuditManualStockAdjustments`

Đã kiểm tra trên database tách biệt, sau đó áp dụng thành công cả ba migration vào database shop local theo yêu cầu khởi động chương trình. Trước khi áp dụng đã tạo backup `.tmp/backups/before-admin-start-20261112-093446.dump` (255.234 bytes). Docker API và migration image được build lại từ source đã commit. Migration là additive; Down từ chối xóa reconciliation/audit khi đã có dữ liệu. Các lệnh dùng để cập nhật runtime:

```powershell
docker compose --env-file .env --profile tools run --rm --build migrate
docker compose --env-file .env up -d --build api
```

Admin Vite cần dependencies hiện tại (`corepack pnpm install --frozen-lockfile` trong `frontend/`); bản build cần được cập nhật cùng backend. Giữ nguyên feature flags và thông tin ngân hàng/email/điện thoại shop đang để trống theo yêu cầu.

Xác nhận sau khởi động: API/PostgreSQL đều `healthy`; `/health/ready` trả 200. Đăng nhập Admin thành công và đọc dashboard, global search, Products filter, articles page, operations outbox đều trả HTTP 200. Storefront `http://localhost:5173` và Admin `http://localhost:5174` đều trả 200. Không seed lại dữ liệu shop. Thay đổi xóa `review-code.md` đã có sẵn được giữ ngoài commit vì không thuộc mã ứng dụng của đợt này.

Không backfill audit tồn, import identity, PaidAt hoặc các đơn hoàn lịch sử. Không mở lại đơn/request đã reject, không tự đưa unit quarantine lên bán. Chưa bổ sung MFA/nhân viên/RBAC chi tiết, rich-text editor, phản hồi review, KPI theo kỳ hay một hệ kế toán/đối soát provider. Đây là các hướng mở rộng; không được suy từ số test pass thành đã chứng nhận toàn bộ vận hành production.

Tài liệu vận hành: [Returns](../docs/backend/api/order-returns.md), [Payment confirmations](../docs/backend/api/order-payment-confirmations.md), [Warranty](../docs/backend/runbooks/warranty-operations.md), [Admin UI](../docs/frontend/admin-ui-ux.md).
