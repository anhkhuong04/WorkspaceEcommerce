# Banner trang chủ

Implementation ngày 15/10/2026. Tài liệu sở hữu bố cục nhiều tầng, title tùy chọn và cách kiểm tra banner; xem [Frontend guide](README.md) cho cấu trúc app và cấu hình.

## Tham khảo Hyperwork

Đã mở [Hyperwork](https://hyperwork.vn/) và chụp bằng Chromium ở 1440px và 390px ngày 15/10/2026. Giao diện quan sát được gồm hero toàn chiều ngang, hai banner sản phẩm cạnh nhau trên desktop/xếp dọc trên mobile, sau đó một dải banner chiến dịch. Ảnh hero sáng, ảnh giữ vai trò chính, khoảng cách giữa các tầng nhỏ và CTA có hình pill.

Ảnh tham khảo local, không đưa vào assets của website:

- `artifacts/frontend/banners/hyperwork/1440-hero.png`
- `artifacts/frontend/banners/hyperwork/1440-second-tier.png`
- `artifacts/frontend/banners/hyperwork/390-hero.png`
- `artifacts/frontend/banners/hyperwork/390-second-tier.png`

WorkspaceEcommerce dùng cách tổ chức tầng tương tự với dữ liệu banner hiện có. Caption của dự án nằm trên nền trắng dưới artwork để ảnh không cần lớp phủ tối. Đây là cách áp dụng cho dữ liệu hiện tại, không sao chép toàn bộ theme Hyperwork.

## Cách bố trí

[HomeBanners](../../frontend/apps/storefront/src/components/ui/HomeBanners.tsx) dùng danh sách active từ `/api/banners`, theo thứ tự API trả về:

| Vị trí trong danh sách active | Khu vực |
| --- | --- |
| Banner đầu tiên | Hero toàn chiều ngang, tải ảnh ưu tiên |
| Banner thứ 2–3 | Mỗi banner chiếm 1/2 chiều ngang từ 768px, kể cả khi chỉ có một banner; xếp dọc ở màn hình nhỏ |
| Banner thứ 4 trở đi | Carousel xoay: banner active ở giữa chiếm khoảng 55% chiều ngang desktop, lộ hai banner bên cạnh; mobile dùng 90% để giữ phần xem trước |

Mỗi banner xuất hiện ở một khu vực. Khoảng cách giữa các tầng là 8px. Tầng không có dữ liệu được ẩn; feed rỗng không hiển thị hướng dẫn quản trị cho khách hàng. Heading chính của homepage độc lập với title banner.

Admin hiển thị **Display order / section** để người vận hành biết vị trí hiện tại. Đổi Display order hoặc bật/tắt một banner sẽ thay đổi phân bố các banner phía sau. API sắp xếp theo `SortOrder`, `Title`, rồi `Id` để giữ thứ tự ổn định khi title trống hoặc trùng nhau. Chưa có placement riêng từng banner; nếu cần gán vị trí độc lập với thứ tự, đó là thay đổi contract/schema riêng.

## Title và ảnh

- **Title tùy chọn:** có thể bỏ khỏi request, gửi `null`, chuỗi trống hoặc whitespace. Domain lưu title thiếu thành `""`; response vẫn trả `title` dạng string. Giới hạn 250 ký tự và yêu cầu ảnh/link hiện có giữ nguyên. Cột title vẫn `NOT NULL`; không cần migration.
- Title có nội dung xuất hiện dưới ảnh, cùng CTA nếu có link. Title trống cho banner chỉ ảnh, không tạo heading rỗng hoặc caption/CTA lặp. Link của artwork vẫn có tên truy cập dự phòng.
- [BannerArtwork](../../frontend/apps/storefront/src/components/ui/BannerArtwork.tsx): hero giữ tỷ lệ tự nhiên. Tầng 2 desktop dùng khung 3:2, carousel dùng khung 8:5 để giảm nhẹ chiều cao; cả hai dùng `object-cover` để ảnh lấp đầy. Artwork cần chuẩn bị theo tỷ lệ khung và đặt nội dung quan trọng trong vùng an toàn. Không phủ tối hoặc filter lên ảnh active. Chỉ các phần xem trước hai bên được phủ trắng nhẹ để phân biệt với banner chính. Khi ảnh lỗi, có fallback và vẫn giữ đường dẫn của banner.
- [BannerCarousel](../../frontend/apps/storefront/src/components/ui/BannerCarousel.tsx) đặt controls ngoài ảnh: hai nút tròn 56px và cụm chấm trong pill xám. Chuyển ngang 400ms khi bấm mũi tên/chấm, không tự xoay. Dãy chấm dài cuộn trong pill, không làm tràn viewport. Reduced motion bỏ chuyển động; slide không active dùng `inert` để loại khỏi keyboard navigation.
- Mỗi banner nhận link độc lập, ví dụ `/products/atlas-standing-desk`. Banner chính trong carousel có thể mở sản phẩm; các banner xem trước không tham gia thứ tự Tab. Khi chỉ có hai slide, phần xem trước đối diện là bản sao chỉ để hiển thị, không thêm link hoặc focus target.
- Form admin ghi **Title (optional)**, preview ảnh bằng `object-contain`. Upload đi qua pipeline media hiện có; khóa Save và đóng dialog trong lúc upload để tránh lưu URL cũ hoặc rỗng.

Ưu tiên artwork 3:2 cho tầng 2 và 8:5 cho tầng 3 để tránh cắt nội dung khi lấp đầy khung. Hero và tầng 2 trên điện thoại giữ tỷ lệ tự nhiên; carousel luôn dùng khung 8:5. Hiện chưa có URL ảnh mobile riêng. Caption/link có nhãn không thay thế việc chuẩn bị nội dung mô tả có ý nghĩa cho người dùng công cụ hỗ trợ.

## Kiểm tra

Chạy trong `frontend/`:

```powershell
corepack pnpm test:ui
corepack pnpm test:ui:admin
corepack pnpm lint
corepack pnpm typecheck
corepack pnpm build
```

Backend, từ root:

```powershell
dotnet test tests/WorkspaceEcommerce.Application.Tests/WorkspaceEcommerce.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~AdminBannerServiceTests"
dotnet test tests/WorkspaceEcommerce.Api.IntegrationTests/WorkspaceEcommerce.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AdminBannerIntegrationTests"
```

Regression gồm title thiếu/trống/quá dài, thứ tự title trống ổn định, round-trip qua PostgreSQL test riêng, form image-only, upload pending, 3 tầng ở 320/390/768/1440px, ảnh không bị lớp DOM che, feed rỗng, ảnh lỗi, slideshow và axe cho các trạng thái banner đã quét. UI tests dùng fixtures, không ghi database local.

Kết quả ngày 15/10/2026: 9 storefront UI tests, 15 admin UI tests và test upload banner riêng đều pass; 15 Application banner tests và 6 API banner integration tests pass. Lint, typecheck và build frontend pass. Còn warning có sẵn ở CheckoutPage, bundle lớn và NU1902/NU1903 cho `System.Security.Cryptography.Pkcs 9.0.0` trong Docker build API; không nâng dependency trong task banner.

Lần chỉnh bố cục theo ảnh người dùng cùng ngày: toàn bộ 10 storefront UI tests pass, bao gồm banner tầng 2 đơn lẻ vẫn giữ nửa chiều ngang desktop, carousel ở giữa có hai phần xem trước, tự xoay và link sản phẩm. Storefront lint và build (kèm typecheck) pass với warning có sẵn ở CheckoutPage và bundle lớn. Đã chụp và kiểm tra bố cục ở 320/390/768/1440px; ảnh đủ ba tầng dùng fixtures nằm trong `artifacts/frontend/ui/banner-corrected-final/`. Không chạy lại backend tests cho lần chỉnh bố cục này vì không sửa backend.

Ảnh website với dữ liệu thực nằm ở `artifacts/frontend/banners/local/`. Kiểm tra màu so sánh ảnh hero đang phục vụ với ảnh render ở từng viewport, không chứng minh độ chính xác màu từ file gốc trước bước encode media. Database hiện có 2 banner active nên hiển thị hero và một thẻ phụ; thêm banner active sẽ mở các vị trí tiếp theo.

So sánh trung bình RGB toàn ảnh ở 4 viewport cho chênh lệch từng kênh tối đa 0.31/255 giữa ảnh được phục vụ và ảnh render sau resize. Số liệu nằm trong `artifacts/frontend/banners/local/color-comparison.json`; không phải phép kiểm định độ chính xác màu từng pixel. Title placeholder `.` của hero local đã được xóa; bản ghi trước sửa nằm trong `banners-before-title-cleanup.json` cùng thư mục.
