# Các Luồng Xử Lý Chính (Main Flows)

Tài liệu này mô tả các luồng nghiệp vụ (Business Flows) cốt lõi trong hệ thống WorkspaceEcommerce MVP.

## 1. Luồng Mua sắm và Đặt hàng (Shopping & Checkout Flow)

Đây là luồng quan trọng nhất đối với trải nghiệm của khách hàng trên Storefront.

1. **Khám phá:** Khách hàng truy cập trang chủ, xem các banner, danh mục hoặc tìm kiếm sản phẩm.
2. **Xem chi tiết:** Khách hàng click vào một sản phẩm để vào Trang Chi Tiết Sản Phẩm (Product Detail Page).
3. **Chọn lựa:** Khách hàng đọc mô tả, chọn các biến thể mong muốn (ví dụ: Màu Đen, Kích thước 1m6). Hệ thống sẽ hiển thị giá và số lượng tồn kho của SKU tương ứng.
4. **Thêm vào giỏ:** Khách hàng bấm "Thêm vào giỏ hàng". Hệ thống lưu thông tin giỏ hàng (Cart) kèm theo Session ID (với khách vãng lai) hoặc Customer ID (với khách đăng nhập).
5. **Xem giỏ hàng:** Khách hàng chuyển tới trang Giỏ hàng, kiểm tra lại danh sách sản phẩm, thay đổi số lượng nếu cần.
6. **Thanh toán (Checkout):** 
   - Khách hàng điền thông tin người nhận (Họ tên, SĐT, Email, Địa chỉ, Ghi chú).
   - Chọn phương thức thanh toán (COD hoặc Chuyển khoản thủ công).
7. **Tạo đơn hàng:** 
   - Khi bấm "Đặt hàng", hệ thống sẽ tiến hành snapshot (lưu lại) thông tin sản phẩm và giá cả tại thời điểm đó vào bảng `OrderItem`. Điều này đảm bảo nếu sau này Admin đổi giá sản phẩm thì đơn hàng cũ không bị ảnh hưởng.
   - Hệ thống tiến hành trừ số lượng tồn kho của SKU tương ứng (tùy theo cấu hình lưu trữ lúc đặt hay lúc xác nhận).
   - Trạng thái đơn hàng lúc này là **`Pending`** (Chờ xử lý).
8. **Hoàn tất:** Khách hàng được chuyển đến trang "Đặt hàng thành công" kèm theo Mã Đơn Hàng để tiện tra cứu sau này.

## 2. Luồng Xử lý Đơn hàng (Order Processing Flow - Admin)

Sau khi khách hàng đặt đơn, luồng xử lý sẽ được tiếp nối bởi Admin trong trang quản trị.

1. **Tiếp nhận:** Admin đăng nhập, vào mục "Quản lý đơn hàng" và thấy các đơn hàng mới ở trạng thái **`Pending`**.
2. **Xác nhận:** 
   - Admin kiểm tra thông tin. Nếu khách chọn "Chuyển khoản thủ công", Admin kiểm tra tài khoản ngân hàng xem tiền đã vào chưa.
   - Sau khi mọi thứ ổn thỏa, Admin cập nhật trạng thái đơn thành **`Confirmed`** (Đã xác nhận). Lúc này một record lịch sử thay đổi trạng thái sẽ được ghi lại (`OrderStatusHistory`).
3. **Chuẩn bị hàng:** Bộ phận kho/đóng gói bắt đầu lấy hàng. Trạng thái có thể chuyển sang **`Processing`** (Đang xử lý).
4. **Giao hàng:** Đơn hàng được giao cho đơn vị vận chuyển. Trạng thái chuyển thành **`Shipping`** (Đang giao hàng).
5. **Kết thúc:**
   - Nếu giao hàng thành công: Trạng thái chuyển thành **`Completed`** (Hoàn tất).
   - Nếu giao hàng thất bại (khách không nghe máy, boom hàng...): Trạng thái chuyển thành **`FailedDelivery`**. Từ trạng thái này, Admin có thể cho giao lại (về `Shipping`) hoặc hủy hẳn (chuyển sang **`Cancelled`**).
   - Nếu đơn hàng bị hủy bất cứ lúc nào (khách yêu cầu hủy, hết hàng...): Trạng thái là **`Cancelled`** và hệ thống có thể hoàn lại tồn kho.

## 3. Luồng Quản lý Sản phẩm (Product Management Flow - Admin)

Luồng để Admin đưa một sản phẩm mới lên Storefront.

1. **Tạo Danh mục:** (Nếu chưa có) Admin vào Quản lý danh mục, tạo mới danh mục "Ghế Công Thái Học".
2. **Tạo Sản phẩm:** Admin vào mục Sản phẩm -> Thêm mới. Nhập Tên, Mô tả, gán vào danh mục "Ghế Công Thái Học".
3. **Thêm Hình ảnh & Thông số:** Admin tải lên các hình ảnh quảng bá sản phẩm và nhập các thông số kỹ thuật (Chất liệu, Trọng tải, v.v.).
4. **Tạo Biến thể (Variants/SKU):** 
   - Sản phẩm bắt buộc phải có ít nhất 1 biến thể để có thể bán được.
   - Admin tạo biến thể "Khung Đen - Lưới Đen". Nhập Mã SKU, Giá bán, Giá gốc, và Số lượng tồn kho.
5. **Xuất bản:** Admin bật cờ `IsActive` (Hiển thị) cho cả Sản phẩm và Biến thể. Lúc này khách hàng mới có thể nhìn thấy và tìm kiếm sản phẩm trên giao diện Storefront.

## 4. Luồng Đăng ký / Đăng nhập Khách hàng (Customer Auth Flow)

Mặc dù hệ thống hỗ trợ Guest Checkout (mua không cần tài khoản), khách hàng vẫn có thể tạo tài khoản để theo dõi đơn hàng dễ hơn.

1. Khách hàng truy cập trang Đăng ký, nhập Email, Mật khẩu, Họ tên.
2. Hệ thống mã hóa mật khẩu, tạo bản ghi `Customer`.
3. Khách hàng Đăng nhập bằng Email/Mật khẩu.
4. Hệ thống xác thực và trả về một chuỗi JWT (JSON Web Token).
5. Trình duyệt (Frontend) lưu trữ JWT này và đính kèm vào các Request API tiếp theo.
6. Các giỏ hàng và đơn hàng phát sinh sau đó sẽ được tự động liên kết với `CustomerId` của người dùng.
