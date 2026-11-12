# Khắc phục storefront và backend — 11/11/2026

Đối chiếu [báo cáo khảo sát](report_storefront_uiux_backend_2026-11-11.md) và [kế hoạch nghiệp vụ](task_ecommerce_business_readiness.md). Giữ nguyên các thay đổi UI admin/banner đã có trong working tree; không commit, push hoặc triển khai lên môi trường bên ngoài.

## Phạm vi đã triển khai

| Finding | Xử lý |
|---|---|
| SF-01 | Xóa/cancel cache riêng khi đổi danh tính/đăng xuất; query key có customer ID; bỏ response profile/refresh/401 thuộc phiên cũ. Giữ cart theo session như thiết kế. |
| SF-02 | Public comment DTO chỉ còn ID, tên hiển thị, nội dung, thời điểm tạo. Email và audit kiểm duyệt chỉ ở admin. |
| SF-03 | Có lỗi cấu hình shipping kèm thử lại, giữ checkout fail closed. Rebuild/migration/runtime local được ghi riêng ở phần xác minh. |
| SF-04 | Payment result lấy trạng thái server, phân biệt Unpaid/Pending/Paid/Failed/Cancelled/RefundPending/Refunded; Pending poll hữu hạn và có nút cập nhật. Không dùng query string `status` làm bằng chứng thanh toán. |
| SF-05 | Card nhiều biến thể dẫn đến chọn cấu hình; quick-add chỉ dùng SKU duy nhất và kiểm tra lại product detail trước ghi cart. |
| SF-06 | Tìm tên đa ngôn ngữ trong JSONB cùng slug, truy vấn/paging tại PostgreSQL; ký tự `%`, `_` được coi là ký tự tìm kiếm. |
| SF-07 | Yêu cầu trả hàng được khóa và ghi audit một lần, giới hạn 7 ngày từ CompletedAt đầu tiên; giữ Completed/Paid. Không tự xác nhận nhận hàng, nhập kho hay hoàn tiền. Admin status edit không được bỏ qua luồng kiểm nhận. |
| SF-08 | Phân trang orders/warranties, query key gồm tài khoản và trang; không giữ dữ liệu tài khoản cũ làm placeholder. |
| SF-09 | Tăng tương phản chữ phụ, badge giảm giá, nút checkout/trả hàng và footer trong phạm vi storefront. |
| SF-10 | Bổ sung VI/EN cho biểu mẫu, phản hồi và các trang chính; schema validation giải nghĩa bản dịch khi render; trạng thái đơn/thanh toán có formatter riêng của storefront. Dữ liệu sản phẩm, tên hãng và lỗi API nguyên bản không tự dịch. |
| SF-11 | Nhãn checkout hiển thị, lỗi có liên kết aria-describedby/aria-invalid, coupon có tên truy cập, lỗi login liên kết đúng field. |
| SF-12 | Chấm sao dùng radio native có bàn phím; feedback live và lỗi tải review có thử lại. |
| SF-13 | Bỏ showroom/tuyển dụng chưa có đích và form newsletter không gửi dữ liệu. |
| SF-14 | Catch-all và error page thân thiện, không lộ stack UI cho khách. |
| SF-15 | Lọc danh mục cha bao gồm cây con active, có visited set để giới hạn vòng lặp dữ liệu sai. |
| SF-16 | Giá so sánh lấy cùng SKU với giá thấp nhất, tie-break theo SKU; áp dụng cả related products trong blog. |
| SF-17 | New arrivals sort `created-desc`, tối đa 10 sản phẩm. Desktop từ 1024px: 5 cột × tối đa 2 hàng; mobile giữ grid responsive. |
| SF-18 | Bỏ thông tin liên hệ/cam kết giả, ẩn quyền lợi tier chưa thực thi. Contact/bank lấy từ public merchant config; chưa đủ ngân hàng thì storefront không cho chọn chuyển khoản. Không tạo thông tin nhận tiền giả. |
| SF-19 | Checkout dùng footer gọn, không có FAQ dài; giữ ba tầng banner hiện hữu và thu gọn điều hướng tầng 3. |
| BE-01 | Capture VNPay hợp lệ sau local cancellation được lưu thành Success + RefundPending; đơn vẫn Cancelled, không tạo shipment. Retry không áp dụng lại. |
| BE-02 | Cancellation effects dùng chung cho admin/customer/provider, giao dịch chứa trạng thái + tiền + tồn + lượt coupon. Hủy sau failed delivery không tự đưa hàng vào kho bán. |
| BE-03 | SKU admin có expectedStockQuantity, locked reload và chỉ lưu field thay đổi. Import khóa/reload SKU trước trừ kho; legacy checkout refresh tracked snapshot dưới lock. |
| BE-04 | Live refresh/cancel response lấy lại payment → order → shipment locks sau HTTP; reload dữ liệu, áp dụng event và side effects trong transaction. |
| BE-05 | Cho phép FailedDelivery → Shipping → Completed khi giao lại, vẫn giữ điều kiện thu COD của shipping mới. |
| BE-06 | Có xác nhận hoàn **toàn bộ** thủ công có reference/reason/actor/time, idempotent; PaymentStatus.Refunded = 6. Có deadline snapshot và worker expiry cho đơn trả trước mới. Không gọi API hoàn tiền VNPay giả định. |
| BE-07 | COD đã thu có thể ghi nhận remittance sau Cancelled/Returned; không cho thu mới trên đơn đóng. |
| BE-08 | Review create/delete cùng khóa product trước kiểm tra/aggregate, không ghi đè toàn bộ product; hai khách review đồng thời không mất count/average. |

## Quy tắc vận hành mới

- `OrderExpiry:VNPayMinutes=30`, `OrderExpiry:ManualTransferMinutes=1440`, snapshot vào đơn mới khi checkout. Worker quét mỗi phút, tối đa 100 đơn; chỉ Pending/Confirmed chưa thanh toán và có deadline. COD và đơn cũ deadline null được giữ nguyên, không backfill suy đoán.
- Expiry/cancellation trả lượt coupon trước bàn giao bằng `CouponRedemption.ReleasedAt`; giữ redemption audit, snapshot giảm giá và điểm đã đổi. Voucher loyalty được dùng lại nếu còn hiệu lực; không hoàn điểm lần hai.
- Full refund là xác nhận **tiền đã chuyển thực tế**, không phải lệnh chuyển tiền. `POST /api/admin/orders/{id}/payment/confirm-full-refund` chỉ nhận đúng tổng tiền/currency đã đóng băng và đơn đóng đang RefundPending. Admin cần bằng chứng giao dịch trước khi xác nhận.
- `VITE_SHOP_SUPPORT_EMAIL`, `VITE_SHOP_SUPPORT_PHONE`, `VITE_SHOP_BANK_NAME`, `VITE_SHOP_BANK_ACCOUNT_NAME`, `VITE_SHOP_BANK_ACCOUNT_NUMBER` là thông tin công khai trong bundle. Các trường mẫu để trống; chủ shop phải cung cấp thông tin thật và rebuild frontend.
- Migrations mới: `AddOrderReturnRequests`, `AddAuditedFullRefund`, `AddPrepaidOrderDeadlines`. Không sửa migration lịch sử. Không rollback về API không hiểu Refunded sau khi đã ghi dữ liệu mới.

## Phần chưa được coi là hoàn thiện nghiệp vụ

Yêu cầu trả hàng hiện là hàng chờ xem xét. Workflow kiểm nhận theo từng item/unit, disposition bán lại/cách ly, hoàn tiền một phần, phân bổ discount/phí ship, đảo điểm có debt và void bảo hành theo unit vẫn cần triển khai theo EC-30/31/32/33. Đã chặn chuyển Returned bằng status edit để không tạo bằng chứng nhận hàng/hoàn tiền sai. Không đánh dấu các task này Done.

Lỗi từ API/provider và một số câu động ngoài luồng chính có thể vẫn bằng tiếng Anh; cần message codes để dịch đầy đủ. Merchant contact/bank, cam kết bảo hành theo SKU, provider sandbox/UAT và các gate production trong runbook cần xác minh riêng. Suite xanh không chứng minh shop đã sẵn sàng mở bán.

## Xác minh

- Application: **357/357**, Infrastructure: **241/241**, PostgreSQL/API integration: **178/178**. Tổng **776 backend tests** pass. Có ca mới cho late capture sau hủy, callback/provider hủy lặp, giao lại, refresh đến sau cancellation, stock tracker cũ, review đồng thời, refund audit, expiry nhiều worker, coupon release và chặn status Returned không kiểm nhận.
- Frontend Vitest **19/19**; toàn workspace lint/typecheck/build pass. Build vẫn cảnh báo chunk lớn hơn 500 kB; chưa xử lý chia bundle trong thay đổi này.
- Playwright UI: bộ **15/15** pass; sau bổ sung rating keyboard, chạy lại **3/3** ca liên quan (rating, phân trang đơn thứ 21, grid 5×2). Tổng 16 ca UI khác nhau đã pass. Các fixture chặn API/provider thật; SignalR không được giả lập nên có log negotiate 404 trong hai ca account, không dùng làm bằng chứng realtime đã được kiểm thử.
- Migration pending-model check pass. Script clean-create + upgrade đến `20261111024702_AddPrepaidOrderDeadlines` pass với `-PostgresPort 15439`; cổng mặc định 55432 bị Windows chặn. Test nâng cấp được sửa để seed schema lịch sử bằng SQL, thay vì dùng EF model mới chèn cột chưa tồn tại.
- Docker API/migrate build pass. Đã dừng API local, `pg_dump`, khôi phục vào database kiểm tra riêng, so fingerprint, xóa database kiểm tra, rồi chạy migration job một lần và bật API lại. Không sửa migration lịch sử hoặc tạo đơn test trên database local.
- Backup ở `.tmp/backups/workspace-remediation-before-20261111.dump` (Git ignored), SHA-256 `9972AB52BFC9E0BBFDAC8FD7D5749232ADC05ED42281F07B2C3D20ED1DC2BDBB`. Fingerprint đơn/items/payment/redemption và 585 SKU khớp sau migration; database local trước cập nhật không có đơn hàng.
- API đang healthy với image `sha256:8fce1e9d9f308e5ba38980d74015f2c7a269dff061a5f85b432b795836f73e1f`. `/health/ready`, storefront 5173, admin 5174 đều HTTP 200. Shipping capabilities nay HTTP 200, vẫn `enabled=false` theo cấu hình local đã có. Không tự bật shipping mới hoặc tạo bảng giá vận chuyển giả.
- Smoke GET trên dữ liệu local: new arrivals trả 10; tìm nguyên tên sản phẩm trả 1; danh mục cha có 24 sản phẩm; có 6 banner active.
- Browser trên dữ liệu local, 1440px/390px: lần lượt 5 cột × 2 hàng và 2 cột × 5 hàng; banner tầng 3 chuyển ngang khi bấm; không overflow/pageerror; axe vùng main không có vi phạm trong trạng thái đã quét. Asset ngoài localhost bị chặn trong probe nên ảnh sản phẩm CDN có thể hiện fallback; đây không phải kết luận ảnh thật bị lỗi.
- Ảnh/JSON: `artifacts/frontend/remediation-live-20261111/`, `artifacts/frontend/ui/remediation-complete-20261111/`, `artifacts/frontend/ui/remediation-extra-20261111/`. Đã xem ảnh grid và controls; screenshot toàn dải có thể chứa sticky header do vị trí scroll khi capture element.
- `git diff --check` pass; không commit/push. Chưa thực hiện VNPay/MiniLogistics sandbox UAT, kiểm tra gửi email thật, restore production hoặc production release gates.

## Thứ tự phần việc tiếp theo

1. **P1 — EC-30/31/32/33:** workbench nhận/kiểm hàng trả, số lượng theo item/unit, disposition, phân bổ tiền/phí ship, hoàn một phần, reversal debt và warranty void. Điều kiện nghiệm thu: nhận một phần/lặp/đồng thời không tăng dư tồn, hoàn dư tiền, tích/đảo điểm hai lần hoặc vô hiệu sai serial. Giữ guard Returned cho đến khi workflow này hoàn chỉnh.
2. **P1 — cấu hình/UAT mở bán:** chủ shop cung cấp ngân hàng/liên hệ thật, xác nhận điều kiện bảo hành theo SKU; chạy provider sandbox, payment → fulfillment → reconciliation trên môi trường riêng. Thông tin nhận tiền chưa được cung cấp nên hiện vẫn để trống.
3. **P2 — UI còn lại:** error/message codes cho bản dịch API, rà soát câu động ngoài luồng chính, metadata theo route và chia bundle; kiểm tra accessibility thủ công với screen reader, không chỉ axe.
