# Báo cáo kiểm tra storefront UI/UX và backend — 11/11/2026

**Phạm vi:** chỉ kiểm tra và báo cáo. Không sửa production source, test source, database local đang dùng, cấu hình hoặc image đang chạy.

**Kết luận:** storefront có nền tảng bố cục và tương tác tốt, nhưng chưa nên coi là sẵn sàng cho khách thật. Ưu tiên cao nhất là cô lập cache theo tài khoản, loại dữ liệu riêng khỏi API bình luận, thống nhất trạng thái thanh toán và xử lý các ngoại lệ hàng/tiền. Các test hiện tại đều có giá trị nhưng chưa bao phủ những ca được phát hiện thêm trong báo cáo.

## 1. Phương pháp và giới hạn bằng chứng

- Áp dụng skill ui-ux-pro-max: checklist accessibility, form/feedback, touch, responsive, navigation và React. Đọc hướng dẫn computer-use; browser access host không kết nối được nên dùng Playwright/test tooling đã có của repo.
- Đọc source storefront, API client/types, owning backend services/domain, persistence và test. Tôn trọng ADR, intentional direct EF, snapshots và guest ownership.
- Kiểm tra localhost bằng HTTP GET và render trình duyệt. Chặn request ghi trong lượt khảo sát đọc; không đặt đơn, tạo tài khoản, gửi email/comment hoặc gọi thanh toán/vận chuyển thật.
- 17 route/case được khảo sát ở 1440×1000 và 390×1000: 34 lượt, có ảnh, DOM measurements và axe. Thêm 8 scenario account/checkout/payment/review với API fixtures và 16 lượt cho account overview/loyalty/warranties/success/các trang support còn lại. Các fixture là dữ liệu giả, không phải bằng chứng backend/provider thật đã hoàn tất luồng đó.
- Có kiểm tra riêng việc đổi tài khoản A → B trong cùng SPA, bằng API fixtures; không dùng thông tin tài khoản thật.
- API integration chạy source hiện tại trên PostgreSQL Testcontainers riêng. Test fixture thay provider thật bằng adapter giả; không chứng minh VNPay/MiniLogistics production.
- Asset ngoài localhost bị chặn trong các probe khảo sát. Vì vậy ảnh ngoài không tải/font ngoài thay thế trong những lượt này không được kết luận là lỗi asset production. Không dùng số đo dev server làm LCP/INP production.
- Việc chỉ kiểm tra 390/1440 trong probe không đủ kết luận mọi thiết bị/zoom. Bộ regression UI hiện có bổ sung viewport 320/768 và tương tác bàn phím; chưa thử thiết bị thật hoặc screen reader thật.
- Source baseline HEAD: 8878958, kèm working tree đang có thay đổi của người dùng. Không đưa những thay đổi có sẵn vào phạm vi sửa của báo cáo.

**Nhãn bằng chứng:** Live = quan sát HTTP/render local; Fixture = tái hiện qua UI với response giả kiểm soát; Source = trace được trực tiếp trong code; Risk = race/điều kiện chưa tái hiện bằng regression mới; Gap = nghiệp vụ chưa được xây đủ; Recommendation = đề xuất UX, không phải lỗi contract.

**Mức độ:** P1 cần xử lý trước khách thật/mở bán rộng; P2 nên xử lý để hoàn thiện trải nghiệm và tính đúng đắn; P3 tối ưu sau. Không dùng số điểm tổng hợp giả để thay bằng chứng.

## 2. Kết quả kiểm tra đã chạy

| Kiểm tra | Kết quả | Ý nghĩa và giới hạn |
| --- | --- | --- |
| Application .NET tests, Release/no-restore | 354 passed, 0 failed | Invariants và use cases hiện có |
| Infrastructure .NET tests, Release/no-restore | 241 passed, 0 failed | Provider/mapping/config hiện có |
| API integration, PostgreSQL Testcontainers | 169 passed, 0 failed; 4m47s test time | HTTP/ownership/DB/concurrency đã có test; không bao phủ mọi phát hiện dưới đây |
| Storefront Vitest | 4 files, 17 tests passed | API/session/i18n/formatting/shipping-state hiện có |
| Storefront typecheck + ESLint | Passed, exit code 0 | Kiểm tra tĩnh; không thay thế UI/behavior tests |
| Storefront Playwright UI regression, timeout mặc định 60s | 8 passed, 2 timeout | Hai test timeout ở banner/chuỗi capture, không phải assertion xác nhận backend sai |
| Chạy lại riêng 2 test timeout với --timeout 120000 | 2 passed, khoảng 44s và 45s/test | Không sửa test. Không coi lần chạy mặc định là xanh; cho thấy giới hạn thời gian/độ ổn định cần ghi nhận |
| Read-only page audit, 34 lượt | 0 horizontal-overflow, 0 pageerror, không lượt nào lỗi điều hướng trong probe | Chỉ các viewport/data đã chạy; trang 404 mặc định vẫn là UX lỗi dù không phát pageerror |
| Cross-account cache fixture | B nhìn thấy PRIVATE-ORDER-A-ONLY; chỉ có request orders của A | Tái hiện được nguy cơ lộ dữ liệu trong cache trình duyệt, không phải backend cho phép B đọc DB của A |

Tổng backend: **764 tests passed**. Kết quả này không xóa bỏ các khoảng trống coverage nêu trong từng finding.

Artifacts: [audit.json](../artifacts/frontend/storefront-audit-20261111/audit.json), [scenarios.json](../artifacts/frontend/storefront-audit-20261111/scenarios.json), [extra-scenarios.json](../artifacts/frontend/storefront-audit-20261111/extra-scenarios.json), [cache.json](../artifacts/frontend/storefront-audit-20261111/cache.json).

## 3. Phát hiện cần ưu tiên trước

### SF-01 — P1 — Cache dữ liệu khách hàng không tách theo tài khoản

**Bằng chứng: Fixture + Source.** CustomerAuthProvider signOut/clearSession xóa auth state nhưng không xóa/cancel customer queries. QueryClient sống ở App, staleTime 30 giây. Account orders/loyalty/warranty query keys không có customerId.

**Tái hiện:** A mở /account/orders → sign out → B đăng nhập trong cùng tab → mở Orders trước khi cache stale. Probe ghi nhận trong 323ms B vẫn thấy PRIVATE-ORDER-A-ONLY, không có request orders của B.

**Tác động:** lộ thông tin của người dùng trước trên máy dùng chung; stale detail/loyalty có cùng nhóm rủi ro. Backend ownership đúng không ngăn được dữ liệu đã cache bị render.

**Điểm đọc:** [CustomerAuthProvider.tsx](../frontend/apps/storefront/src/features/customer-auth/CustomerAuthProvider.tsx), dòng 39, 149–160; [App.tsx](../frontend/apps/storefront/src/app/App.tsx), dòng 9; [AccountPages.tsx](../frontend/apps/storefront/src/pages/account/AccountPages.tsx), dòng 325, 356, 510, 583.

**Cải thiện:** key mọi query riêng tư theo customerId; cancel request cũ và remove customer/payment private cache khi identity đổi/logout; xử lý response A đang bay về sau lúc B đăng nhập. Regression phải kiểm tra cả cache fresh và network response đến muộn.

Ảnh: [cross-account-cache.png](../artifacts/frontend/storefront-audit-20261111/cross-account-cache.png).

### SF-02 — P1 — API blog public trả email người bình luận và metadata kiểm duyệt

**Bằng chứng: Source.** GET /api/blog-posts/{slug} là public; approved comments được map bằng BlogCommentDto chứa AuthorEmail, ModeratedAt, ModeratedBy. UI lại ghi “Email (will not be published)”. Không chỉ cần nhìn UI: người gọi API có thể đọc JSON.

**Điểm đọc:** [StorefrontBlogService.cs](../src/WorkspaceEcommerce.Application/Modules/Blogs/StorefrontBlogService.cs), dòng 69–76 và 204–215; [BlogCommentDto.cs](../src/WorkspaceEcommerce.Application/Modules/Blogs/BlogCommentDto.cs), dòng 10–15; [BlogDetailPage.tsx](../frontend/apps/storefront/src/pages/news/BlogDetailPage.tsx), dòng 203.

**Cải thiện:** public comment DTO riêng chỉ có display name/content/date và ID cần thiết. Giữ email/audit ở admin DTO. Test anonymous GET sau approve phải assert các field email/moderator không tồn tại; test moderation hiện có chỉ assert content/visibility.

**Giới hạn:** không tạo comment thật để tái hiện; kết luận từ contract/service/controller hiện tại. Metadata ModeratedBy có giá trị cụ thể gì phụ thuộc dữ liệu nhưng không nên công khai qua DTO này.

### SF-03 — P1 — Frontend hiện tại đang nối image API cũ, làm checkout thiếu capability endpoint

**Bằng chứng: Live + Source.** Image workspace-ecommerce-api:local được build **2026-10-15 01:17 UTC**. Container tạo mới ngày 11/11 không đồng nghĩa image đã được build lại. GET /api/checkout/shipping/capabilities trả 404, trong source controller hiện tại có endpoint này.

**Tác động:** CheckoutPage chỉ cho legacy shipping khi capabilities.isSuccess && !enabled. Với 404, cả quoted flow và legacy flow đều chưa đủ điều kiện; shippingUnavailable giữ nút đặt hàng disabled. API health trả 200 không chứng minh frontend/backend tương thích.

**Điểm đọc:** [ShippingQuotesController.cs](../src/WorkspaceEcommerce.Api/Controllers/ShippingQuotesController.cs), dòng 18; [CheckoutPage.tsx](../frontend/apps/storefront/src/pages/checkout/CheckoutPage.tsx), dòng 152, 608 và 913. Script local .tmp/windows-startup/Start-WorkspaceEcommerce.ps1 khởi động bằng --no-build nên dùng image cache.

**Cải thiện:** xác định candidate source/image/migration cùng phiên bản, build/release image tương ứng và smoke contract. UI cần phân biệt capability unavailable với chưa nhập địa chỉ và có đường retry. Không fallback âm thầm khi chưa biết giá.

**Giới hạn:** không rebuild image hoặc migrate DB local trong lượt review này. Source integration pass là kết quả của source hiện tại, không phải image cũ đang phục vụ port 5080.

### SF-04 — P1 — Pending/RefundPending bị hiển thị thành Payment failed

**Bằng chứng: Fixture + Source.** statusFromPaymentStatus chỉ phân Paid → success và Cancelled → cancelled; mọi trạng thái còn lại → failed. Probe với paymentStatus=1 và =5 đều cho h1 “Payment failed.”.

**Tác động:** khách có thể nghĩ giao dịch thất bại, chuyển tiền lại hoặc liên hệ hỗ trợ sai vấn đề. Trang không có polling riêng cho Pending; callback đến sau không tự được thể hiện ngay nếu không có lần refetch khác.

**Điểm đọc:** [PaymentResultPage.tsx](../frontend/apps/storefront/src/pages/checkout/PaymentResultPage.tsx), dòng 22, 155–176.

**Cải thiện:** phân biệt Pending/Unpaid/Paid/Failed/Cancelled/RefundPending; lấy backend làm chuẩn, có refresh/poll bounded cho trạng thái đang chờ và lời giải thích đúng nghĩa. Không hứa retry thanh toán nếu backend chưa hỗ trợ an toàn.

Ảnh: [payment-pending-390.png](../artifacts/frontend/storefront-audit-20261111/payment-pending-390.png), [payment-refund-pending-390.png](../artifacts/frontend/storefront-audit-20261111/payment-refund-pending-390.png).

### SF-05 — P1 — Quick-add tự chọn cấu hình khác mức giá đang quảng bá

**Bằng chứng: Live GET + Source.** Card Atlas hiển thị “Từ 18.174.000đ”, trong khi variant đầu còn hàng là Black/160cm **19.474.000đ**. useAddProductToCart chọn variant đầu có stock, không hỏi cấu hình. Detail cũng mặc định variant đầu còn hàng.

**Tác động:** khách bấm Add từ card có thể nhận cấu hình/giá cao hơn mà chưa chọn; không phải backend tính sai, nhưng hành vi mua hàng thiếu rõ ràng.

**Điểm đọc:** [useAddProductToCart.ts](../frontend/apps/storefront/src/features/cart/useAddProductToCart.ts), dòng 12; [ProductCard.tsx](../frontend/apps/storefront/src/components/ui/ProductCard.tsx); [ProductDetailPage.tsx](../frontend/apps/storefront/src/pages/product-detail/ProductDetailPage.tsx), selectedVariant.

**Cải thiện:** sản phẩm nhiều biến thể mở selector hoặc chuyển detail bằng CTA “Chọn cấu hình”; chỉ quick-add trực tiếp khi cấu hình/giá đã xác định rõ. Có test min-price variant khác first-in-stock variant.

### SF-06 — P1 — Tìm kiếm không tìm theo tên tiếng Việt đang hiển thị

**Bằng chứng: Live GET + Source.** Backend lọc duy nhất row.Product.Slug.Contains(normalizedSearch). Tìm đúng tên đang có “Atlas Standing Desk” cũng trả 0 vì slug có dấu gạch nối. Với Accept-Language vi, search=ghế trả 0 còn search=chair trả 7 trong dữ liệu local; riêng cặp tiếng Việt/Anh còn chịu ảnh hưởng dữ liệu dịch đã nhập. Test catalog hiện có dùng search=desk trùng slug nên không bắt được vấn đề tìm theo tên.

**Điểm đọc:** [StorefrontCatalogService.cs](../src/WorkspaceEcommerce.Application/Modules/Catalog/Storefront/StorefrontCatalogService.cs), dòng 49–51; header search và product list đều gọi API này.

**Cải thiện:** tìm trên tên theo ngôn ngữ/fallback và slug; xác định chính sách dấu/không dấu, trim/case và SKU có được hỗ trợ hay không. Chứng minh EF translation/index bằng PostgreSQL, không chỉ filter client.

### SF-07 — P1 — “Request return” đang đồng nghĩa kết thúc trả toàn bộ đơn

**Bằng chứng: Source + UI fixture.** Customer RequestReturnAsync chỉ cần Completed rồi đổi thẳng Returned, không kiểm cửa sổ 7 ngày, không có duyệt, nhận/kiểm hàng, refund hoặc item quantities. UI cho mọi Completed order bấm request. Đây là giới hạn nghiệp vụ đã được docs ghi nhận, không phải mọi side effect đã tự chạy.

**Điểm đọc:** [CustomerOrderService.cs](../src/WorkspaceEcommerce.Application/Modules/Customers/Orders/CustomerOrderService.cs), dòng 254–292; [AccountPages.tsx](../frontend/apps/storefront/src/pages/account/AccountPages.tsx), dòng 942, 1016–1049; [SupportPages.tsx](../frontend/apps/storefront/src/pages/support/SupportPages.tsx), chính sách 7 ngày.

**Cải thiện:** case Requested/Reviewed/Received/Resolved theo item, giữ trạng thái đơn cho tới khi có bằng chứng xử lý; kiểm hàng bán được/hỏng, hoàn tiền và hậu mãi theo đúng bước. Chưa hoàn thiện thì cần kênh hỗ trợ rõ, không để request tự biến thành return hoàn tất.

## 4. Phát hiện UI/UX và discoverability còn lại

### SF-08 — P2 — Không đi tới đơn/bảo hành sau 20 bản ghi

**Bằng chứng: Fixture + Source.** AccountOrdersPage cố định pageNumber=1/pageSize=20, hiển thị totalCount nhưng không có pagination/load more. Fixture tổng 21 đơn chỉ thấy 20. AccountWarrantiesPage có cùng mẫu; loyalty transactions đã có phân trang nên không gộp tất cả account pages thành lỗi này.

**Điểm đọc:** [AccountPages.tsx](../frontend/apps/storefront/src/pages/account/AccountPages.tsx), dòng 323–350, 581–608.

**Cải thiện:** page state/URL + Next/Previous hoặc load more có trạng thái; test 21/41 records, trang rỗng sau thay đổi và Back/Forward.

### SF-09 — P2 — Contrast chưa đạt ở chữ phụ và sale badge

**Bằng chứng: axe + ảnh.** Ví dụ chữ slate-400 trên trắng **2.63:1**, slate-500 trên nền FAQ #f3f3f3 **4.29:1**, chữ trắng trên accent #e52b1f **4.47:1** ở kích thước chữ nhỏ. Các tỷ lệ này thấp hơn ngưỡng 4.5:1 mà checklist dự án đặt cho normal text.

Desktop: home 12 nodes, catalog 16, detail 9, lookup 5, news 6, blog detail 10; nhiều node là footer dùng chung, không cộng thành số lỗi độc lập. Các trang có 0 axe violations ở một trạng thái không đồng nghĩa đã đạt mọi tiêu chí accessibility.

**Điểm đọc:** [ProductCard.tsx](../frontend/apps/storefront/src/components/ui/ProductCard.tsx), [ProductDetailPage.tsx](../frontend/apps/storefront/src/pages/product-detail/ProductDetailPage.tsx), [StorefrontFooter.tsx](../frontend/apps/storefront/src/components/layout/StorefrontFooter.tsx), tokens/styles. Selector và tỷ lệ cụ thể có trong audit.json.

**Cải thiện:** điều chỉnh semantic tokens/cặp màu thực tế, kiểm lại trên mọi nền và trạng thái; tránh chỉ tăng font-weight hoặc đổi từng trang rời rạc.

### SF-10 — P2 — Ngôn ngữ Việt/Anh không nhất quán ở các trang quan trọng

**Bằng chứng: Live/Fixture + Source.** Khi language=vi, header/catalog đã Việt hóa nhưng login, checkout, account, payment result, phần lớn support và nội dung bài viết vẫn có text tiếng Anh hard-code. Không coi tên sản phẩm từ catalog là lỗi i18n nếu đó là dữ liệu đã nhập; vấn đề ở nhãn/feedback của ứng dụng.

**Điểm đọc:** [LoginPage.tsx](../frontend/apps/storefront/src/pages/login/LoginPage.tsx), [CheckoutPage.tsx](../frontend/apps/storefront/src/pages/checkout/CheckoutPage.tsx), [AccountPages.tsx](../frontend/apps/storefront/src/pages/account/AccountPages.tsx), [SupportPages.tsx](../frontend/apps/storefront/src/pages/support/SupportPages.tsx).

**Cải thiện:** dịch nhãn/validation/status và nội dung chính sách theo namespace; dùng cùng locale date/money. LanguageSwitcher đã invalidate queries khi đổi ngôn ngữ, vì vậy không báo nhầm là thiếu refetch toàn bộ dữ liệu.

### SF-11 — P2 — Form checkout thiếu nhãn nhìn thấy sau khi đã nhập và liên kết lỗi

**Bằng chứng: Fixture + Source.** FormInput/FormSelect có accessible label nhưng label là sr-only; placeholder biến mất khi nhập, nên khách chỉ còn nhìn các giá trị mà khó biết ý nghĩa từng ô. FieldError là p không có id/aria-describedby/aria-invalid. Input coupon chỉ có placeholder, thiếu accessible name riêng.

**Điểm đọc:** [CheckoutPage.tsx](../frontend/apps/storefront/src/pages/checkout/CheckoutPage.tsx), dòng 624–695, 798–815. Login/profile cũng cần kiểm lỗi theo cùng tiêu chí nhưng không khẳng định mọi field đều thiếu label.

**Cải thiện:** nhãn luôn nhìn thấy, required/optional rõ, error gắn với field; submit lỗi có summary/focus hữu ích. Giữ input sau lỗi; không thay inline errors bằng toast.

Ảnh: [checkout-auth-390.png](../artifacts/frontend/storefront-audit-20261111/checkout-auth-390.png).

### SF-12 — P2 — Chọn sao review không dùng được bằng bàn phím

**Bằng chứng: Source/DOM fixture.** StarIcon gắn onClick lên svg; 5 sao lựa chọn không có button/radio role, tabIndex và accessible name. Các icon button khác có keyboard support không bù được thao tác này.

**Điểm đọc:** [ProductReviews.tsx](../frontend/apps/storefront/src/pages/product-detail/ProductReviews.tsx), dòng 11–24 và 112–126.

**Cải thiện:** radiogroup/label với giá trị 1–5, Arrow keys, selected state và focus rõ; test keyboard-only, không chỉ mouse. Feedback lỗi/thành công review cũng cần thông báo phù hợp cho assistive technology.

### SF-13 — P2 — Tác vụ/link hiển thị như hoạt động nhưng chưa có đích/kết quả

**Bằng chứng: Source + rendered links.** Newsletter submit chỉ preventDefault, không lưu/gửi và không cho kết quả. Tuyển dụng/Cửa hàng ở footer trỏ #; mục showroom trên header không có route thực.

**Điểm đọc:** [StorefrontFooter.tsx](../frontend/apps/storefront/src/components/layout/StorefrontFooter.tsx), dòng 171–181; [StorefrontLayout.tsx](../frontend/apps/storefront/src/components/layout/StorefrontLayout.tsx), dòng 24.

**Cải thiện:** cung cấp đích thực hoặc ẩn/ghi rõ chưa có. Chỉ bật newsletter khi có consent, xử lý subscribe và feedback thật; không tạo thành công giả.

### SF-14 — P2 — URL không tồn tại hiển thị trang lỗi dành cho developer

**Bằng chứng: Live.** /not-a-real-page ở cả desktop/mobile hiện “Unexpected Application Error! 404 Not Found” và hướng dẫn developer của React Router, không có storefront recovery flow.

**Điểm đọc:** [router.tsx](../frontend/apps/storefront/src/app/router.tsx), không có catch-all/errorElement tùy chỉnh.

**Cải thiện:** trang 404/error boundary với về trang chủ/tìm sản phẩm, giữ navigation; phân biệt not found với API/network error. Đây là lỗi xử lý UI, không phải bằng chứng có exception backend.

### SF-15 — P2 — Danh mục cha có sản phẩm ở con nhưng click ra kết quả trống

**Bằng chứng: Live GET + Source.** Office Furniture có 3 children nhưng /api/products?categorySlug=office-furniture trả 0. Filter chỉ row.Category.Slug == categorySlug, trong khi home/menu cho phép click danh mục cha.

**Điểm đọc:** [StorefrontCatalogService.cs](../src/WorkspaceEcommerce.Application/Modules/Catalog/Storefront/StorefrontCatalogService.cs), dòng 42–46; [HomePage.tsx](../frontend/apps/storefront/src/pages/home/HomePage.tsx), CategoryCard.

**Cải thiện:** chọn rõ sản phẩm của subtree hoặc landing danh mục cha có đường vào con. Không đưa khách tới empty state gây hiểu rằng shop không có hàng; test nested category và inactive descendants.

### SF-16 — P2 — Card có thể ghép giá bán và giá gạch từ hai variant khác nhau

**Bằng chứng: Source.** minPrice là Min(Price), compareAtPrice là Max(CompareAtPrice) toàn bộ variants. Hai giá không bảo đảm thuộc cùng cấu hình. Label “Từ” làm rõ giá thấp nhất nhưng sale/giá gạch vẫn có thể gây hiểu sai mức giảm của cấu hình đó.

**Điểm đọc:** [StorefrontCatalogService.cs](../src/WorkspaceEcommerce.Application/Modules/Catalog/Storefront/StorefrontCatalogService.cs), dòng 232–241; blog related-products có cùng kiểu tính.

**Cải thiện:** compare-at đi cùng variant đại diện hoặc hiển thị khoảng giá/offer có nghĩa rõ; test cheapest variant không sale nhưng variant đắt có compare-at.

### SF-17 — P2 — “Sản phẩm mới” đang là trang đầu sắp tên

**Bằng chứng: Source + Live order.** Home gọi getProducts(pageNumber:1,pageSize:10) không sortBy; backend default name-asc. Vì vậy nhãn new arrivals không được chứng minh bởi tiêu chí mới nhất.

**Điểm đọc:** [HomePage.tsx](../frontend/apps/storefront/src/pages/home/HomePage.tsx), dòng 165–166; StorefrontCatalogService.ApplyProductSorting.

**Cải thiện:** thêm/query sort mới nhất có contract/test hoặc đổi tên section thành tập sản phẩm được chọn; không suy mới từ vị trí alphabet.

### SF-18 — P2 — Nội dung/policy và quyền lợi chưa khớp khả năng thật

**Bằng chứng: Source + Fixture.** Support warranty dùng nhận diện HyperWork và thông tin liên hệ hard-code; loyalty hiển thị Tier discount/Free shipping enabled trong khi business rules hiện tại chưa áp chúng vào checkout; trang chuyển khoản chỉ có mã đơn/nội dung/số tiền, thiếu ngân hàng/tài khoản/chủ tài khoản.

**Điểm đọc:** [SupportPages.tsx](../frontend/apps/storefront/src/pages/support/SupportPages.tsx), dòng 194–246; [AccountPages.tsx](../frontend/apps/storefront/src/pages/account/AccountPages.tsx), dòng 704–705; [CheckoutSuccessPage.tsx](../frontend/apps/storefront/src/pages/checkout/CheckoutSuccessPage.tsx), ManualTransferPanel.

**Cải thiện:** owner xác minh nội dung shop; chỉ công bố quyền lợi đã thực thi; trả thông tin nhận chuyển khoản có cấu hình server và trạng thái xác minh rõ. Không tự thay bằng tài khoản/địa chỉ giả.

### SF-19 — P3 — Ưu tiên nội dung và layout checkout cần gọn hơn

**Bằng chứng: ảnh + Recommendation.** Home có nhiều tầng banner trước catalog; checkout mobile vẫn kèm toàn bộ FAQ/service/footer dài. Summary/CTA có thể ở sâu sau form, khiến rà soát tổng tiền và quay lại sửa lâu.

**Cải thiện:** đánh giá bố cục checkout riêng, summary thu gọn và hỗ trợ ngắn; đưa sản phẩm/đường mua sớm hơn khi cần. Giữ nhận diện hiện tại, kiểm bằng nhiệm vụ mua hàng và dữ liệu thật; chưa kết luận ảnh hưởng conversion hoặc Core Web Vitals.

## 5. Backend: các ngoại lệ đã đối chiếu lại vẫn còn

Các mục dưới đây là Source/Gap/Risk, không phải sự cố đã được tạo trên dữ liệu thật trong lần review này. Chi tiết và task đã có trong [kế hoạch Ecommerce](task_ecommerce_business_readiness.md).

| ID | Mức/loại | Trường hợp và kết quả sai/thiếu | Điểm source | Hướng xử lý / regression còn cần |
| --- | --- | --- | --- | --- |
| BE-01 | P1, Source | Hủy pending VNPay đánh dấu attempt terminal; callback success đến sau bị trả sớm trước GetPaymentOutcome; IPN vẫn acknowledge success | PaymentService.cs:186–202; AdminOrderService.CancelOrOpenRefundWorkflow | Giữ khoản thu muộn và case hoàn/đối soát, không hồi sinh fulfillment; cancel-first + delayed-IPN regression, EC-11 |
| BE-02 | P1, Source | Provider Cancelled đổi Order trực tiếp, không đi qua hoàn tồn/payment side effects của luồng hủy commerce | ShipmentOrderStatusTransitioner.cs:35–55; ShipmentWebhookService.cs:156; OrderShipmentService.cs:559 | Một nghiệp vụ hủy có custody, chống lặp, không tự nhập kho hàng còn carrier; EC-10 |
| BE-03 | P1, Risk | Admin sửa tồn bằng form cũ; import dùng tracked variants đọc trước transaction; legacy VNPay preview trước FOR UPDATE không reload | AdminProductVariantService.cs:101; AdminOrderService.cs:314,362,433; AppDbContext.cs:560–572 | Khóa/version và đọc mới ở mọi writer; PostgreSQL interleaving mới chưa viết/chạy; EC-12/13/14 |
| BE-04 | P1, Risk | Refresh provider chậm ghi lên order/shipment đã đọc trước HTTP, có thể ghi đè hủy hoặc webhook mới | OrderShipmentService.cs:92–113,528–560 | HTTP ngoài transaction, khóa/reload authoritative rồi apply; EC-15 |
| BE-05 | P2, Source | Sau DeliveryFailed, sự kiện Delivering/Delivered không có đường tự tiến Order ra FailedDelivery | ShipmentOrderStatusTransitioner.cs:61–68 | Test sequence giao lại + skipped-event + COD guards; WorkspaceEC retry đã có, EC-16 |
| BE-06 | P1, Gap | Hủy Paid dừng ở RefundPending; chưa có chứng từ/hoàn thành refund. Đơn bỏ thanh toán giữ stock đến khi hủy thủ công | AdminOrderService.cs:223–226; PaymentStatus.cs; CheckoutOrderPlacer/QuotedCheckoutService; PaymentService failed branch | Manual refund có audit trước; deadline/expiry/callback race; không coi email “queued for refund” là có worker thực, EC-21/23 |
| BE-07 | P1, Source | COD đã thu, sau đó Returned/Cancelled thì ConfirmAsync chặn luôn việc remit; tiền thực vẫn cần nộp shop | OrderPaymentService.cs:48,92 | Tách remittance khỏi open-order eligibility; collected→return→remit test, EC-18 |
| BE-08 | P2, Risk | Hai khách review đồng thời có thể đều tính ratingSummary cũ và ghi đè AverageRating/ReviewCount; cùng khách gửi hai request cạnh nhau có unique DB nhưng service không xử lý conflict chuyên biệt | ReviewService.cs:64–97; ReviewConfiguration unique ProductId/CustomerId; AppDbContext.SaveChangesAsync | Lock/recompute/atomic update và translate unique violation; assert 2 reviews nhưng count/average đúng. Chưa tái hiện DB mới |

**Review eligibility cần quyết định riêng:** hiện review cho phép khi Shipping hoặc Completed. Nếu shop chỉ muốn người đã nhận hàng đánh giá, đây là thay đổi policy cần chốt và test; không tự gọi trạng thái Shipping hiện tại là lỗi vì source đang chủ ý cho phép.

**Những gì đã hoạt động và không nên xây lại:** signature/amount validation, ownership theo customer, guest order code + phone proof, locked checkout/coupon, snapshot VND, outbox/inbox, exact manual transfer confirmation và tách COD collected/remitted cho shipping-selection orders. Kết quả integration hiện có bảo vệ một phần đáng kể các hành vi này.

## 6. Phạm vi từng nhóm trang

| Nhóm trang | Đã kiểm tra | Điểm chính |
| --- | --- | --- |
| Header/menu/search/home | Live desktop/mobile; fixture keyboard/modal/carousel | Bố cục gọn, controls/focus phần chính đã có; search tiếng Việt, quick-add, category cha, new arrivals cần sửa |
| Product list/detail | Live + fixture auth/review, axe và variant keyboard regression | Giá/stock/CTA rõ; contrast, chọn cấu hình và review keyboard còn lỗi |
| Cart/drawer | Empty live; item/nested-login bằng regression fixtures | Có pending/error, Escape/focus restore; /cart thực tế là trang mở drawer, không phải trang giỏ full. Quantity nhập từng thay đổi gửi mutation ngay nên nên thử multi-digit/slow network thêm |
| Login/register/2FA | Login render live; account switch fixture; register/2FA trace source/backend tests | Login UI sạch; i18n/error announcements cần nâng. Chưa hoàn tất Google/TOTP bằng tài khoản thật |
| Checkout/success | Auth fixtures, source trace, read-only live capability probe | Missing capability từ image cũ chặn flow; labels/copy/ngân hàng/summary cần hoàn thiện |
| Payment result | Pending/RefundPending fixture; source và backend callback tests | Phân loại trạng thái sai; chưa thực hiện giao dịch VNPay thật |
| Lookup/receipt/tracking | Lookup form live, detail source/API integration | Proof có kiểm tra; contrast/copy/state cần nâng. Không tải receipt đơn thật hoặc gọi tracking provider thật |
| Account overview/profile/orders/detail | Fixtures; source ownership và API tests | Cache identity, pagination, return workflow là trọng tâm; chưa sửa hồ sơ thật |
| Loyalty/warranties | Fixtures + source/API tests | Quyền lợi chưa khớp checkout, warranty flag/capability cần truyền đạt rõ; warranty pagination thiếu |
| News/detail/comment | Live render + source/public DTO và moderation tests | Comment privacy cần sửa; ngày/labels/nội dung i18n chưa thống nhất |
| About/contact/terms/privacy/shipping/returns/warranty-policy | Live/fixture render và shared component/source | Nội dung policy/brand/liên hệ cần owner xác minh; ảnh/tên brand mẫu không phải bằng chứng shop thật |
| Email verification/password reset | Live trang mặc định, source và backend lifecycle tests | Không dùng token thật, không gửi email/reset password. Lỗi/success copy và thông báo accessibility cần kiểm theo từng state |
| Unknown URL | Live cả hai viewport | Default developer 404 cần thay |

**Lựa chọn hiện tại không báo nhầm là bug:** storefront yêu cầu login tại /checkout và /checkout/success, còn backend hỗ trợ guest checkout. docs/frontend/README.md ghi rõ điều này. Có thể đánh giá guest checkout như đề xuất giảm friction, nhưng không tự sửa auth boundary hoặc gọi đây là lỗi ownership.

## 7. Thứ tự xử lý đề xuất sau review

1. **Riêng tư và phiên bản:** SF-01, SF-02; xác minh lại source/image/API contract ở SF-03 trước khi tin kết quả test local end-to-end.
2. **Giao dịch và khả năng mua:** SF-04/05/06/07, BE-01/02/03/04/06/07; ưu tiên transaction/guard đúng trước chỉnh wording để che triệu chứng.
3. **Khả năng tìm/tra cứu và accessibility:** SF-08 đến SF-18; backend test cho nhánh mới cùng keyboard/screen-reader/state coverage.
4. **Tối ưu:** SF-19, metadata/route-specific title và đo performance trên bản build đại diện. Không dùng dev-server timeout làm bằng chứng production chậm.

Kế hoạch EC đã có vẫn là roadmap nghiệp vụ. Các phát hiện mới về cache identity, public comment DTO, search, payment-result mapping và account pagination nên được bổ sung thành task riêng khi bắt đầu sửa, không đánh dấu task cũ Done chỉ vì suite hiện tại xanh.

## 8. Bàn giao

- Chỉ tạo báo cáo này và các probe/artifact kiểm tra trong .tmp/ và artifacts/ bị Git ignore.
- Không sửa source/test code, routes, migrations, provider protocols, auth boundaries hay dữ liệu người dùng local.
- Không kết luận “không còn lỗi”: cần regression mới cho các findings chưa có test, payment/provider sandbox UAT, browser auth/2FA thật và kiểm thử accessibility thủ công trước mở bán.
