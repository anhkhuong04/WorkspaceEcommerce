# UI/UX — định hướng đề xuất tham khảo Hyperwork

Tài liệu sở hữu định hướng hình ảnh và tiêu chí review UI/UX cho Frontend. **Trạng thái: định hướng thiết kế; các mục đã triển khai được ghi riêng bên dưới.** Kiến trúc, API/session và lệnh phát triển vẫn nằm tại [Frontend guide](README.md).

## Cơ sở khảo sát

Khảo sát ngày **14/10/2026**: HTML/CSS công khai của Hyperwork, cấu trúc trang và ảnh hero. Trình duyệt tương tác không khả dụng trong lần khảo sát này; chưa kiểm chứng giao diện render, thao tác touch, animation, checkout có hàng hay hiệu năng thực tế. Các mục sau phần khảo sát là đề xuất riêng cho WorkspaceEcommerce, không phải thông số đo của Hyperwork.

| Nguồn | Điều kiểm chứng được |
| --- | --- |
| [Trang chủ](https://hyperwork.vn/) | Điều hướng theo nhóm, danh mục, sản phẩm mới, blog và nội dung hỗ trợ. CSS khai báo nền trắng, chữ `#333333`, CTA chính `#1a1a1a`, CTA phụ `#d44c00`, container tối đa 1300px và button radius 60px. Font mặc định Manrope được inline CSS override thành Google Sans; chưa xác nhận font render. |
| [Ảnh hero](https://hyperwork.vn/cdn/shop/files/apex_cover_copy-2_9b0d3c35-5b5b-46c7-87aa-0b26d18116bf.jpg?v=1791368264&width=1400) | Bàn/ghế trong bối cảnh setup, nền xám sáng, khoảng trống lớn và bố cục ít chi tiết gây nhiễu. |
| [Danh sách sản phẩm](https://hyperwork.vn/collections/all) | Filter/sort, trạng thái còn hàng, giá/giá so sánh, lựa chọn biến thể và quick-buy drawer. CSS định nghĩa grid tăng từ 2 lên 3/4 cột theo breakpoint; chưa xác nhận bố cục render tại từng viewport. |
| [Chi tiết Apex Halo](https://hyperwork.vn/products/ban-nang-ha-apex-halo-dp02r) | Gallery, chọn cấu hình, CTA mua, nội dung lợi ích, thông số và tài liệu hỗ trợ. Markup có sticky product-info; CSS chuyển từ một cột sang gallery/info khoảng 55/45. |
| [Giỏ hàng](https://hyperwork.vn/cart) | Trạng thái giỏ trống có đường quay lại catalog; chưa khảo sát giỏ có sản phẩm hoặc hoàn tất thanh toán. |

Từ các bằng chứng này, định hướng phù hợp là **tối giản, đặt sản phẩm ở trung tâm, có chất liệu lifestyle và thông tin mua hàng rõ ràng**. Học cách tổ chức thị giác; dùng ảnh, nội dung và nhận diện riêng của WorkspaceEcommerce.

## Nguyên tắc hình ảnh

- **Ảnh dẫn dắt:** hero kể một bối cảnh sử dụng; ảnh catalog làm rõ hình dáng sản phẩm; gallery bổ sung góc chụp, kích thước và chi tiết. Tránh nhúng tên/giá/CTA quan trọng vào ảnh vì khó responsive và dịch.
- **Nền nhẹ, điểm nhấn có mục đích:** trắng/xám cho canvas, chữ tối cho nội dung, màu accent cho promotion có dữ liệu thật. Dùng màu semantic và nhãn cho lỗi/thành công; không dùng màu trang trí thay thế trạng thái.
- **Phân cấp rõ:** mỗi section có một tiêu đề, nội dung ngắn và một CTA chính. Giá, biến thể đang chọn và khả năng mua phải dễ thấy hơn nội dung marketing.
- **Nhịp điệu thống nhất:** container, khoảng cách, tỷ lệ ảnh và vị trí giá nhất quán giữa home/list/detail. Shadow nhẹ cho bề mặt nổi; khoảng trắng và border đảm nhiệm phân nhóm.
- **Motion hỗ trợ thao tác:** hover/transition ngắn để phản hồi, không làm nội dung nhảy hoặc trì hoãn click. Cung cấp trải nghiệm tương đương cho keyboard và touch.

## Tokens và thông số đề xuất

Dùng [design-tokens.css](../../frontend/shared/styles/design-tokens.css) làm nguồn sở hữu tokens; [storefront globals.css](../../frontend/apps/storefront/src/styles/globals.css) sở hữu typography và utility classes. Các giá trị dưới đây là mục tiêu thiết kế, không phải bản sao CSS Hyperwork hay xác nhận đã áp dụng.

| Nhóm | Quy ước cho WorkspaceEcommerce |
| --- | --- |
| Palette | Giữ `--brand`/`--ink` `#111111`, `--surface` `#ffffff`, `--brand-soft` `#f1f1f1`. Giữ `--accent` `#e52b1f` cho điểm nhấn; giá thường dùng ink, sale badge dùng accent. |
| Font | Giữ stack Inter/system đang khai báo; thống nhất weight 400/500/600 cho nội dung/control. Dùng font có glyph tiếng Việt đầy đủ; không cần đổi font để đạt tinh thần thiết kế. |
| Type scale | Body/input 16px, metadata 13–14px; section title 24–36px; hero title 32–56px responsive. Body line-height khoảng 1.5; heading 1.15–1.3. Typography admin có thể gọn hơn nhưng phải đọc rõ. |
| Container | Chọn một content max-width khoảng 1300–1440px và dùng chung cho catalog. Gutter 16–20px mobile, 32–40px desktop; hero có thể full-bleed. |
| Spacing | Dùng bước 4/8px; gap control 8–12px, card 16–24px, section 40–64px mobile và 64–96px desktop. |
| Radius | Giữ control 8px, card 12px, panel 16px. CTA mua/xem thêm có thể pill; form và bảng không cần đổi toàn bộ sang pill. |
| Images | Catalog ưu tiên nền trung tính, khung vuông và `object-contain` để giữ hình sản phẩm. Ảnh lifestyle dùng `object-cover` với focal point riêng cho mobile. |
| Motion | Mục tiêu 150–250ms cho hover/drawer feedback; giảm/tắt chuyển động trang trí với `prefers-reduced-motion`. |

Nếu cần tokens riêng cho storefront, khai báo override trong stylesheet app; thay đổi shared tokens phải kiểm tra cả admin. Màu thực tế cần đo contrast trên từng tổ hợp nền/chữ, kể cả metadata và disabled/error states.

## Áp dụng theo màn hình

| Khu vực | Hướng thiết kế và UX |
| --- | --- |
| Header/search | Logo, catalog, search, account và cart là ưu tiên. Desktop có thể dùng mega-menu; mobile dùng menu/drawer có nút đóng rõ. Search có loading/no-result/error và đường mở sản phẩm; tránh menu chỉ mở bằng hover. |
| Home | Hero với một thông điệp và CTA → danh mục → sản phẩm → blog/hỗ trợ. Giữ sản phẩm sớm trong luồng cuộn; hero mobile phải cho thấy đường tiếp tục mua. Chỉ hiển thị chính sách/lợi ích mà sản phẩm thực sự hỗ trợ. |
| Product list | Grid 2 cột trên mobile khi đủ chiều rộng, 3–4 trên desktop; thu về 1 cột nếu tên/control quá chật. Filter mobile dạng drawer có Apply/Reset; giữ filter/sort/page trong URL để Back không mất lựa chọn. Không đưa filter mới vào UI khi API chưa hỗ trợ. |
| Product card | Cùng tỷ lệ ảnh và thứ tự category → tên → giá → trạng thái. Cho phép tên dài ít nhất hai dòng; sale/stock badges không đè nhau. CTA thấy được trên touch và `focus-visible`, có pending/error/success feedback. |
| Product detail | Gallery trước, summary mua hàng cạnh gallery trên desktop và theo thứ tự đọc trên mobile. Variant/stock/quantity đi cùng giá và CTA. Sticky summary chỉ dùng khi không che nội dung; benefit/specification/support chia section rõ. |
| Cart/checkout | Drawer hỗ trợ xem nhanh, trang cart hỗ trợ chỉnh số lượng và đọc tổng tiền. Checkout tập trung form, lỗi ngay field, giữ input sau lỗi và summary dễ kiểm tra. Thể hiện trạng thái lấy phí vận chuyển/thanh toán đúng dữ liệu backend. |
| Account/admin | Dùng cùng tokens, typography và trạng thái control; account ưu tiên lịch sử/tracking, admin ưu tiên bảng/filter/thao tác. Hero marketing và khoảng trắng lớn của storefront không áp dụng máy móc cho màn hình vận hành. |

Quick-buy có chọn biến thể là hạng mục đề xuất riêng: hook hiện tại chọn variant đầu tiên còn hàng. Nếu triển khai selector, giữ lựa chọn rõ ràng và kiểm tra lại stock/price từ backend. Route guard checkout, VND, payment result và ownership theo [Frontend guide](README.md) và [Business rules](../backend/product/business-rules.md); thiết kế mới phải phản ánh các contract đó.

## Phần đã triển khai trong storefront

| Điểm đọc | Implementation hiện tại |
| --- | --- |
| [ProductCard](../../frontend/apps/storefront/src/components/ui/ProductCard.tsx) | Home/catalog dùng chung card: ảnh vuông/contain, tên hai dòng, giá wrap, image fallback và lỗi mutation có `role=alert`. CTA hiện trên touch; desktop hỗ trợ hover/focus-within. |
| [BannerCarousel](../../frontend/apps/storefront/src/components/ui/BannerCarousel.tsx) | Hero dùng chiều cao responsive 340–620px, điều khiển 44px, pause/resume, dừng khi tương tác và không auto-advance khi reduced motion. Slide không active dùng `inert` để loại CTA khỏi keyboard navigation. |
| [HomePage](../../frontend/apps/storefront/src/pages/home/HomePage.tsx), [StorefrontLayout](../../frontend/apps/storefront/src/components/layout/StorefrontLayout.tsx) | Header/content dùng container 1440px với gutter responsive. Header sáng, sticky, có disclosure menu desktop, navigation dialog mobile và skip link. Caption 13px; body/input 16px trong storefront stylesheet. |
| [useModalDialog](../../frontend/apps/storefront/src/hooks/useModalDialog.ts) | Search, cart, mobile filters/navigation và login prompt dùng native dialog, vòng Tab, Escape, backdrop dismissal, body scroll lock và trả focus. Cart nhớ nút Add ngay cả khi nó tạm disabled trong mutation. |
| [ProductDetailPage](../../frontend/apps/storefront/src/pages/product-detail/ProductDetailPage.tsx) | Gallery/summary 55/45 trên desktop; ảnh có kích thước ổn định, thumbnail có selected state, variant hỗ trợ Arrow/Home/End, CTA pill và live error/success feedback. |
| [useAddProductToCart](../../frontend/apps/storefront/src/features/cart/useAddProductToCart.ts) | Quick-add vẫn chọn variant đầu tiên còn hàng; chưa thêm quick-buy selector. Filters vẫn cập nhật URL như trước, nút “Xem kết quả” đóng dialog sau khi xem/chỉnh bộ lọc. |

UI regression tại [storefront-ui.spec.ts](../../frontend/e2e/storefront-ui.spec.ts) dùng API fixtures local, không chứng minh backend/provider semantics. Chạy `corepack pnpm test:ui` trong `frontend/`; ảnh và trace nằm dưới ignored `artifacts/frontend/ui/`. Checkout form, bộ lọc chọn biến thể nhanh và trải nghiệm trên thiết bị thật vẫn cần task/QA riêng khi mở rộng.

## Tiêu chí review khi triển khai

- Kiểm tra viewport 360/390, 768 và 1440px, tiếng Việt/Anh, tên dài, giá lớn và zoom 200%. Nội dung chính không tràn ngang; ảnh/CTA không che tên, giá hay form.
- Chữ thường có contrast tối thiểu 4.5:1; chữ lớn đủ điều kiện tối thiểu 3:1 theo [WCAG contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html). Đo trên màu/ảnh nền thực tế.
- Control chính trên touch hướng đến vùng bấm 44×44 CSS px, là mục tiêu của dự án. [WCAG 2.2 target size AA](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) quy định 24×24px hoặc spacing/ngoại lệ phù hợp; không nhầm hai mức này.
- Menu/drawer/dialog dùng được bằng keyboard, có focus rõ, Escape đóng và trả focus về trigger; label form luôn hiện, lỗi không chỉ phân biệt bằng màu. Carousel có pause và tôn trọng reduced motion.
- Review loading/empty/error/retry, ảnh lỗi, variant hết hàng, mutation pending, API 401 và payment pending/failure. Không báo thành công trước response; giữ dữ liệu nhập khi thao tác thất bại.
- Ảnh có kích thước/aspect-ratio ổn định, responsive sources; lazy-load dưới fold, không lazy-load hero chính. Đo trên môi trường đại diện; mục tiêu field p75: LCP ≤2.5s, INP ≤200ms, CLS ≤0.1 theo [Core Web Vitals](https://web.dev/articles/vitals). Một lần chạy Lighthouse không chứng minh field p75.

Triển khai theo thứ tự: **tokens/typography + ProductCard → header/home/list/detail → cart/checkout và các trạng thái**. Mỗi task giữ phạm vi nhỏ, kèm ảnh trước/sau desktop/mobile và kiểm tra tương tác phù hợp trong [Testing and quality](../backend/development/testing.md). Tài liệu này chưa chứng minh conversion hay hiệu năng đã cải thiện; đánh giá bằng kết quả kiểm thử và dữ liệu sau triển khai.
