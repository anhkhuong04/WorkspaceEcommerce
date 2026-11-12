# Dashboard và UI/UX quản trị

Phạm vi: admin React trong `frontend/apps/admin`. Đánh giá và refactor ngày 14/10/2026 dựa trên source, ảnh trước/sau với dữ liệu local và kiểm tra trình duyệt bằng fixtures. [UI/UX style](ui-ux-style.md) sở hữu định hướng storefront; tài liệu này sở hữu quy ước cho màn hình vận hành.

## Đánh giá

Trước refactor, dashboard chưa đáp ứng tốt trải nghiệm quản trị: bảng làm tràn trang ở nhiều kích thước, typography đậm và panel lớn gây nhiễu, thông tin kỹ thuật chiếm diện tích, nhiều nút cạnh nhau và modal thiếu quản lý focus. Các chức năng đã có không đồng nghĩa với luồng thao tác đã hoàn thiện.

Bản hiện tại có nền tảng hiển thị và tương tác thống nhất cho 9 trang quản trị. Các trạng thái đã kiểm tra đáp ứng kiểm tra responsive, bàn phím và accessibility tự động bên dưới. Chưa đủ bằng chứng để tuyên bố đạt toàn bộ WCAG hoặc tối ưu tuyệt đối: vẫn cần kiểm tra với người vận hành, thiết bị thật và dữ liệu lớn.

| Khu vực | Vấn đề chính trước refactor | Implementation hiện tại |
| --- | --- | --- |
| Điều hướng | Menu mobile trải ngang, phân cấp yếu | Sidebar chia Overview, Commerce, Content, Operations; menu mobile có focus trap, Escape và trả focus; có skip link |
| Dashboard | Hero lớn, nhãn/KPI và empty state dễ gây hiểu nhầm | 4 KPI gọn, trạng thái đơn dạng thanh tỷ lệ và danh sách có số lượng; click mở bộ lọc; chỉ một thông báo khi chưa có đơn |
| Products | Slug, URL ảnh, badge và nhiều thao tác cạnh tranh; chi tiết rất dài | Bảng desktop, thẻ mobile có giá/tồn kho/nút sửa; menu thao tác phụ; tabs biến thể/ảnh/thông số; link tồn kho tìm đúng trang sản phẩm |
| Categories | Bảng rộng, quan hệ cha/con khó quét | Tên phân cấp, slug là metadata, thứ tự và trạng thái rõ; tạo/sửa bằng form có nhãn và submit bằng Enter |
| Orders | Dữ liệu rải nhiều cột; drawer kéo dài; xác nhận native | Gộp mã/ngày, khách/điện thoại, tiền/số item, thanh toán; drawer chia Overview, Delivery, Items, Activity; xác nhận hủy shipment trong dialog |
| Coupons | Bộ lọc dựa vào placeholder; chỉ chọn được 100 sản phẩm đầu | Nhãn Search/status, Search/Reset và URL giữ bộ lọc; nhóm hiệu lực/giới hạn/đối tượng; bộ chọn tìm tên/danh mục/SKU qua tất cả trang |
| Banners / Articles | URL dài chiếm cột; thao tác và trạng thái thiếu nhất quán | Thumbnail cùng tiêu đề, đích đến/ngày, trạng thái và nút icon; article có tabs nội dung/bình luận, bộ chọn sản phẩm có tìm kiếm |
| Reviews | Bình luận dài làm khó quét; nút xóa lặp | Rating có số sao và icon, mở toàn bộ bình luận khi cần, nút xóa có tên truy cập và xác nhận |
| Warranties | Các công cụ nằm chung một trang; lỗi API khi feature tắt; prompt nhập lý do | Tabs registrations/units/plans, trạng thái unavailable khi API trả 404, dialog yêu cầu lý do và giữ input khi lỗi; feature flag giữ nguyên |

Thông tin phục vụ quyết định vẫn được giữ: giá và stock, trạng thái thanh toán/vận chuyển, coupon redemption, order snapshot, reason và audit bảo hành. URL ảnh chỉ xuất hiện khi chỉnh sửa; slug không cần là cột trong danh sách sản phẩm. Các bảng nhiều thuộc tính cuộn trong vùng có nhãn và hướng dẫn khi thực sự vượt chiều rộng.

Banner được bổ sung ngày 15/10/2026: title tùy chọn, vị trí theo các tầng homepage và khóa form khi upload. Chi tiết sở hữu tại [Banner trang chủ](banner-layout.md).

## Quy ước thiết kế

Nguồn implementation: [admin globals.css](../../frontend/apps/admin/src/styles/globals.css). Tokens admin override trong app để độc lập với storefront.

| Thành phần | Quy ước |
| --- | --- |
| Màu | Canvas `#f6f8fa`, surface trắng, ink `#17212f`, brand teal `#0f766e`; màu trạng thái luôn kèm chữ/icon |
| Typography | Inter/system có sẵn; body/control 14px, metadata 12px, title 24px; weight 400/500/600; input mobile 16px |
| Khoảng cách | Bước 4/8px; gap control 8–12px, panel 16–20px, gutter 16–28px; mỗi trang có một heading và CTA chính |
| Border / radius | Border nhẹ phân nhóm, input có viền rõ; control 8px, panel 12px, dialog 14px; shadow dành cho lớp nổi |
| Control | Button tối thiểu 40px desktop, 44px mobile; tab 44px; switch có vùng hit mở rộng; icon 16–22px có tên truy cập |
| Trạng thái | Loading, empty, error, success rõ ràng; lỗi form cạnh field, lỗi API ngay dialog; giữ dữ liệu khi lỗi và khóa form/đóng dialog trong lúc lưu |
| Responsive | Sidebar từ 1024px; Products chuyển thành thẻ dưới 640px; bảng khác cuộn trong vùng riêng; modal có body cuộn và footer cố định |

[WCAG contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) yêu cầu 4.5:1 cho chữ thường, 3:1 cho chữ lớn. [WCAG target size AA](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) quy định 24×24 CSS px cùng các ngoại lệ; 44px trên mobile là mục tiêu của dự án. Đo từng trạng thái thực tế, không suy ra compliance chỉ từ token. Giữ focus outline rõ và hỗ trợ `prefers-reduced-motion`.

## Component và thư viện

- [Lucide React](https://lucide.dev/guide/react): icon SVG thống nhất, import theo tên; icon trang trí `aria-hidden`, icon button có `aria-label` và tooltip native.
- [Radix Primitives](https://www.radix-ui.com/primitives/docs/components/dialog): Dialog, AlertDialog, DropdownMenu, Switch, Tabs. Dùng primitive cho vòng focus, keyboard và semantics; stylesheet admin sở hữu phần nhìn.
- [AdminUi.tsx](../../frontend/apps/admin/src/components/ui/AdminUi.tsx): Button, Field, input, notice, pill, table viewport và các shell tương tác. Page quản lý mutation, pending, error và điều kiện nghiệp vụ.
- [Pagination](../../frontend/apps/admin/src/components/ui/Pagination.tsx): số kết quả, khoảng đang xem, trước/sau; ẩn điều khiển khi chỉ có một trang. Products, Orders, Coupons, Reviews dùng chung.
- [ProductChecklist](../../frontend/apps/admin/src/components/ui/ProductChecklist.tsx): search, số lựa chọn, checkbox có nhãn, tải/lỗi/retry. Coupon và article dùng chung; tìm kiếm không làm mất lựa chọn.
- [Router](../../frontend/apps/admin/src/app/router.tsx): tải code từng module qua [React Router lazy](https://reactrouter.com/7.18.4/start/data/route-object#lazy), có phản hồi khi chuyển trang; routes và auth guard giữ nguyên.

Không thêm một framework form hoặc state management khác. React Hook Form/Zod, TanStack Query và API facade tiếp tục sở hữu các trách nhiệm hiện có.

## Ý nghĩa dữ liệu và giới hạn

- Revenue là tổng đơn hoàn tất mọi thời điểm. Backend chưa cung cấp KPI theo kỳ, trend hoặc so sánh kỳ trước; không vẽ chart thời gian hoặc phần trăm tăng trưởng từ dữ liệu hiện tại.
- Dashboard trả tối đa 10 biến thể tồn kho thấp. Nhãn **Stock alerts shown** và mô tả “up to 10” phản ánh số đang hiển thị; không gọi đây là tổng cảnh báo của catalog. “Inventory to review” dùng ngưỡng backend trả về.
- API admin products hiện phân trang, chưa có lookup theo ID hoặc search toàn catalog. Link dashboard tìm qua các trang và dùng lại cache. Bộ chọn coupon/article đọc từng trang 100 sản phẩm để không bỏ sót; catalog lớn cần endpoint lookup/search trước khi mở rộng quy mô.
- Warranty hiện có giới hạn trang đầu trong các danh sách và còn nhập variant/order-item/replacement-unit ID, coverage dạng dòng. Cần task riêng cho pagination, bộ chọn và form coverage để hoàn thiện trải nghiệm khi feature được bật; chưa coi luồng này sẵn sàng cho mọi người vận hành.
- Article content vẫn là textarea với định dạng backend hiện hỗ trợ. Chưa có rich-text editor hoặc preview bài viết. Không thay đổi quy tắc lưu/render nội dung trong refactor này.

## Kiểm tra và bảo trì

### Bổ sung ngày 12/11/2026

- Header có tìm kiếm toàn Admin (`Ctrl/⌘ K`): sản phẩm/SKU, đơn/khách/điện thoại/
  tracking, danh mục, coupon và bài viết; tối đa năm kết quả mỗi nhóm. Link mở
  đúng bản ghi qua URL; không tải toàn bộ dữ liệu về trình duyệt để tìm.
- Products có Search/Apply/Reset, category, visibility, inventory, featured,
  giá VND và sắp xếp, giữ bộ lọc/phân trang trên URL. Form lưu dictionary VI/EN,
  bảo toàn bản dịch khác. SKU có actual weight/kích thước và lịch sử điều chỉnh
  tồn thủ công trước/sau với lý do và người thực hiện.
- Picker coupon/article dùng tìm kiếm/phân trang server, tải tên các lựa chọn
  bằng tập ID tối đa 100. Lựa chọn ngoài trang vẫn nhìn thấy và bỏ chọn được.
- Dashboard có nhóm việc mở danh sách đã lọc, net collected all-time dùng tiền
  đã xác nhận trên đơn Completed/Returned trừ hoàn đã ghi nhận, đổi sang VND theo
  tỷ giá đóng băng trong đơn. Đây không phải lợi nhuận hoặc đối soát ngân hàng.
- Orders có mobile cards, payment/queue filters, Returns và đối soát thu
  thiếu/thừa. Xem [quy trình RMA](../backend/api/order-returns.md) trước vận hành.
- Articles/comments và warranty phân trang, có search/moderation toàn cục.
  Warranty dùng lookup SKU/order item/spare và editor coverage có trường riêng.
- Upload khóa Save/Cancel/Close; Shipping có bảo vệ draft, khóa khi lưu và xóa
  preview cũ khi đổi đầu vào. URL sai và lỗi route có màn hình phục hồi riêng.
- Operations đọc summary/terminal failures đã giữ lại (kể cả bản ghi đã replay),
  mở đơn liên quan và xác nhận trước khi queue replay. Latest 50/100 là giới hạn
  bản ghi hiển thị, không phải số lỗi đang mở trên toàn hệ thống.

`corepack pnpm typecheck` và `test:ui:admin` kiểm tra cả kiểu DTO của fixtures
Admin qua `tsconfig.admin-e2e.json`. Bộ UI dùng API fixtures, bộ integration
kiểm tra HTTP/PostgreSQL riêng; xem kết quả của đợt hiện tại tại
[báo cáo triển khai](../../task/implementation_admin_remediation_2026-11-12.md).

Chạy trong `frontend/`:

```powershell
corepack pnpm test:ui:admin
corepack pnpm --filter @workspace-ecommerce/admin lint
corepack pnpm --filter @workspace-ecommerce/admin typecheck
corepack pnpm --filter @workspace-ecommerce/admin build
```

[admin-ui.spec.ts](../../frontend/e2e/admin-ui.spec.ts) dùng Chromium và API fixtures bị chặn khỏi backend thật. Kiểm tra 9 trang ở 320/390/768/1440px, document overflow, axe WCAG A/AA, menu mobile, tabs, focus restoration, modal lồng nhau, validation/lỗi API, khóa form khi lưu, pagination/Back, bộ lọc URL và bộ chọn hơn 100 sản phẩm. Warranty được kiểm tra cả unavailable và actions với fixture enabled. Ảnh/trace nằm dưới ignored `artifacts/frontend/admin/ui/verified/`.

Kết quả lần kiểm tra ngày 14/10/2026: 14 UI tests pass; không có vi phạm axe trong các trạng thái đã quét hoặc tràn ngang toàn trang ở 4 viewport. Lint, typecheck và build frontend pass; storefront có một lint warning sẵn có ở CheckoutPage, hai app có cảnh báo bundle lớn. Đây là kết quả kiểm tra có phạm vi, không phải chứng nhận accessibility.

Ảnh local dữ liệu thực dưới `artifacts/frontend/admin/final/` chỉ chứng minh phần nhìn tại thời điểm chụp; không thay thế kiểm tra nghiệp vụ. UI fixtures không chứng minh phân quyền backend, PostgreSQL, thanh toán, carrier hoặc import thật. Build vẫn có cảnh báo chunk lớn; lazy routes giảm tải code module ban đầu nhưng chưa có đo Core Web Vitals trên bản deploy.

Khi bổ sung màn hình, ưu tiên dùng component trên, giữ URL cho bộ lọc/phân trang phù hợp, kiểm tra nội dung dài và dialog lỗi bằng bàn phím. Để nghiệm thu đầy đủ còn cần QA trên thiết bị thật, zoom/screen reader và người vận hành thực hiện các tác vụ điển hình.
