# Chức năng Khách hàng (Customer Features)

Tài liệu này mô tả các chức năng dành cho khách hàng truy cập website (Storefront) trong hệ thống WorkspaceEcommerce MVP.

## 1. Các chức năng chính

### Trang chủ (Home Page)
- Xem các banner quảng cáo, chương trình khuyến mãi nổi bật.
- Khám phá các danh mục sản phẩm phổ biến.
- Xem danh sách các sản phẩm nổi bật (Featured products) do Admin chọn.

### Duyệt và Tìm kiếm Sản phẩm (Product Browsing & Search)
- Duyệt sản phẩm theo cấu trúc danh mục.
- Tìm kiếm sản phẩm cơ bản theo tên.
- Lọc danh sách sản phẩm theo:
  - Danh mục.
  - Khoảng giá.
  - Trạng thái (Còn hàng/Hết hàng).

### Chi tiết Sản phẩm (Product Detail)
- Xem thông tin chi tiết của sản phẩm bao gồm: Tên, Hình ảnh (hỗ trợ nhiều ảnh), Giá bán, Giá gốc (hiển thị giảm giá).
- Đọc mô tả sản phẩm và xem bảng thông số kỹ thuật.
- Chọn các biến thể sản phẩm (Màu sắc, Kích thước) để xem giá và tình trạng tồn kho tương ứng.
- Nhận biết các sản phẩm có yêu cầu dịch vụ lắp đặt đặc biệt (`RequiresInstallation`).

### Giỏ hàng (Cart)
- Thêm sản phẩm (cụ thể là một biến thể - SKU) vào giỏ hàng.
- Xem danh sách các sản phẩm trong giỏ, tổng số lượng và tạm tính tổng tiền.
- Cập nhật số lượng của từng sản phẩm.
- Xóa sản phẩm khỏi giỏ hàng.
- *Lưu ý:* Giỏ hàng có thể được lưu trữ qua Session đối với khách vãng lai (Guest) hoặc gắn với tài khoản nếu đã đăng nhập.

### Đặt hàng (Checkout)
- Hỗ trợ tính năng mua hàng không cần đăng nhập (Guest Checkout).
- Điền thông tin giao hàng: Họ tên, Số điện thoại, Email, Địa chỉ, và Ghi chú thêm.
- Chọn phương thức thanh toán:
  - Thanh toán khi nhận hàng (COD).
  - Chuyển khoản ngân hàng thủ công (Manual Bank Transfer).
- Xem lại tóm tắt đơn hàng (phí vận chuyển, giảm giá, tổng thanh toán) trước khi xác nhận.

### Quản lý Tài khoản (Account) & Tra cứu đơn hàng
- Đăng ký và đăng nhập tài khoản khách hàng.
- Xem lịch sử mua hàng và thông tin cá nhân.
- (Đối với khách vãng lai): Có trang chuyên biệt để tra cứu tình trạng đơn hàng bằng Mã đơn hàng và Số điện thoại.

### Tin tức (News/Blog)
- Đọc các bài viết, tin tức, hoặc hướng dẫn setup góc làm việc do hệ thống đăng tải.

## 2. Hạn chế so với dự án E-commerce thực tế

So với các website bán hàng hoàn thiện, MVP Storefront có một số giới hạn:

1. **Thanh toán trực tuyến:** Chưa tích hợp các cổng thanh toán online (như VNPay, MoMo, Stripe, PayPal). Khách hàng chọn chuyển khoản phải thực hiện thủ công bên ngoài và chờ Admin xác nhận.
2. **Theo dõi đơn hàng thời gian thực:** Khách hàng chỉ xem được trạng thái nội bộ của hệ thống (Ví dụ: Đang giao hàng), không thể xem lộ trình giao hàng chi tiết (tracking trên bản đồ) từ đối tác vận chuyển.
3. **Phí vận chuyển tự động:** Phí vận chuyển có thể đang được thiết lập cố định hoặc tính toán đơn giản, chưa liên kết với API của hãng vận chuyển để ra phí chính xác theo kích thước/khối lượng và khoảng cách.
4. **Đánh giá và Nhận xét (Reviews & Ratings):** Chưa có hệ thống cho phép khách hàng để lại sao đánh giá và bình luận về sản phẩm đã mua.
5. **Wishlist (Sản phẩm yêu thích):** Không có chức năng lưu lại sản phẩm để mua sau.
6. **Gợi ý sản phẩm thông minh:** Không có các thuật toán gợi ý "Sản phẩm liên quan" hoặc "Khách hàng khác cũng mua" dựa trên AI.
7. **Khách hàng thân thiết:** Không có tích điểm, hạng thẻ thành viên hay chương trình giới thiệu (Referral).
8. **Đa ngôn ngữ & Đa tiền tệ:** Chỉ hỗ trợ một ngôn ngữ (Tiếng Việt) và một loại tiền tệ (VND).
