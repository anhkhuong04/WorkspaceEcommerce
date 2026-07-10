# Chức năng Admin (Admin Features)

Tài liệu này mô tả các chức năng dành cho quản trị viên (Admin) trong hệ thống WorkspaceEcommerce MVP.

## 1. Các chức năng chính

### Xác thực & Phân quyền (Authentication)
- Đăng nhập vào Admin Portal sử dụng Email và Password.
- *Lưu ý:* Hệ thống hiện tại sử dụng một cấp độ quyền admin chung, chưa phân chia vai trò (Role-based access control) phức tạp.

### Dashboard (Bảng điều khiển)
- Xem tổng quan các chỉ số quan trọng:
  - Tổng số đơn hàng.
  - Tổng doanh thu.
  - Số lượng đơn hàng mới.
  - Danh sách các sản phẩm sắp hết hàng cần nhập thêm.

### Quản lý Danh mục (Category Management)
- Tạo mới, cập nhật thông tin danh mục.
- Ẩn/hiện danh mục trên Storefront.
- Hỗ trợ cấu trúc danh mục cha/con (ví dụ: Bàn làm việc -> Bàn nâng hạ).

### Quản lý Sản phẩm (Product Management)
- Thêm mới và cập nhật thông tin sản phẩm cơ bản (Tên, Mô tả, URL slug).
- Gán sản phẩm vào các danh mục tương ứng.
- Đánh dấu sản phẩm nổi bật (Featured).
- Ẩn/hiện sản phẩm.
- Quản lý hình ảnh sản phẩm (thêm, xóa, sắp xếp thứ tự hiển thị).
- Quản lý thông số kỹ thuật (Specifications) của sản phẩm.

### Quản lý Biến thể / SKU (Product Variant Management)
- Một sản phẩm có thể có nhiều biến thể (ví dụ: màu sắc, kích thước).
- Admin có thể quản lý từng biến thể với các thông tin:
  - Mã SKU.
  - Tên phân loại (ví dụ: Trắng - 1m2).
  - Màu sắc, kích thước.
  - Giá bán (Price) và Giá gốc (Compare-at Price) để hiển thị khuyến mãi.
  - Số lượng tồn kho.
  - Đánh dấu yêu cầu lắp đặt (`RequiresInstallation`).
  - Ẩn/hiện từng biến thể riêng biệt.

### Quản lý Đơn hàng (Order Management)
- Xem danh sách toàn bộ đơn hàng của khách.
- Xem chi tiết đơn hàng (thông tin khách hàng, sản phẩm đã đặt, tổng tiền, phương thức thanh toán).
- Cập nhật trạng thái đơn hàng theo luồng: `Pending` -> `Confirmed` -> `Processing` -> `Shipping` -> `Completed` (hoặc `FailedDelivery`, `Cancelled`).
- Thêm ghi chú nội bộ cho đơn hàng.
- Xem lịch sử thay đổi trạng thái của đơn hàng (ai đổi, đổi khi nào, từ trạng thái nào sang trạng thái nào).

### Quản lý Banner
- Thêm mới, cập nhật banner quảng cáo hiển thị ở trang chủ.
- Cấu hình hình ảnh, tiêu đề, và đường dẫn (URL) khi click vào banner.
- Sắp xếp thứ tự hiển thị và ẩn/hiện banner.

### Quản lý Blog & Khuyến mãi (Coupon)
- Tạo và quản lý các bài viết tin tức/blog.
- Quản lý các mã giảm giá (Coupon), thiết lập các chương trình khuyến mãi cơ bản.

## 2. Hạn chế so với dự án E-commerce thực tế

Do đây là phiên bản MVP (Minimum Viable Product), hệ thống Admin có một số hạn chế so với các nền tảng thương mại điện tử lớn (như Shopify, Magento, v.v.):

1. **Thiếu RBAC (Role-Based Access Control) nâng cao:** Không có sự phân chia chi tiết quyền hạn giữa các nhân viên (ví dụ: Nhân viên kho chỉ thấy đơn hàng, Nhân viên Marketing chỉ quản lý banner/blog).
2. **Quản lý tồn kho cơ bản:** Chỉ quản lý số lượng tồn kho trên từng SKU. Chưa có tính năng quản lý đa kho (Multi-warehouse), quản lý nhà cung cấp, nhập xuất kho (Purchase Orders, Stock Transfers).
3. **Thiếu hệ thống CRM & Phân khúc khách hàng:** Không có công cụ theo dõi hành vi, lịch sử mua hàng chi tiết để phân khúc và chăm sóc khách hàng (Loyalty, Điểm thưởng).
4. **Xử lý trả hàng/Hoàn tiền (Return/Refund):** Chưa có luồng xử lý tự động hoặc quản lý yêu cầu trả hàng, hoàn tiền ngay trên hệ thống. Mọi thao tác này phải thực hiện thủ công ngoài hệ thống.
5. **Tích hợp vận chuyển:** Chưa tích hợp trực tiếp với các đơn vị vận chuyển (GHN, GHTK, Viettel Post) để tự động đẩy đơn và in vận đơn. Admin phải cập nhật trạng thái giao hàng thủ công.
6. **Khuyến mãi và Marketing:** Quản lý mã giảm giá còn cơ bản, chưa hỗ trợ các luật khuyến mãi phức tạp (Ví dụ: Mua X tặng Y, Giảm giá theo bậc, Combo sản phẩm).
7. **Báo cáo và Phân tích:** Dashboard chỉ cung cấp các con số cơ bản. Thiếu các báo cáo chuyên sâu về doanh thu theo thời gian, phân tích lợi nhuận, cohort analysis.
