# Frontend

Frontend là pnpm workspace với React, TypeScript, Vite, Tailwind CSS, React Router, TanStack Query, React Hook Form/Zod và i18next. Storefront và admin build độc lập, dùng chung transport contracts và utilities.

## Định hướng UI/UX

[UI/UX style](ui-ux-style.md) phân tích tham khảo Hyperwork và đề xuất phong cách tối giản, đặt sản phẩm ở trung tâm: nền trung tính, ảnh lớn, phân cấp typography rõ và CTA dễ thao tác. Tài liệu sở hữu tokens, bố cục theo màn hình, các điểm cải thiện từ source và tiêu chí review responsive/accessibility/performance. Đây là định hướng cho task cải thiện Frontend; các phần dưới mô tả implementation hiện tại.

[Dashboard và UI/UX quản trị](admin-ui-ux.md) sở hữu đánh giá từng module admin, tokens riêng, component/library, trạng thái tương tác, giới hạn dữ liệu và UI regression. Admin ưu tiên đọc dữ liệu và thao tác vận hành.

[Banner trang chủ](banner-layout.md) mô tả bố cục nhiều tầng tham khảo Hyperwork, title tùy chọn, thứ tự hiển thị và kiểm tra màu ảnh.

## Cấu trúc và ranh giới

| Đường dẫn dưới `frontend/` | Trách nhiệm |
| --- | --- |
| `apps/storefront` | Catalog/content, cart/checkout, order lookup, account/loyalty/warranty và notifications |
| `apps/admin` | Dashboard, catalog/content/coupon/order/review/warranty management |
| `packages/api-types` | DTO, enums, envelope và paging types dùng chung |
| `packages/api-client` | HTTP, bearer/language headers, credentials, envelope/error và tải file |
| `packages/shared-utils` | Định dạng tiền/ngày/trạng thái và download helpers |
| `shared/styles` | Design tokens dùng chung cho storefront/admin |

Các app không import lẫn nhau. Shared packages không chứa app routes, auth state hay feature UI. Backend quyết định giá, tồn kho, quyền truy cập và trạng thái thanh toán; frontend hiển thị kết quả và hỗ trợ nhập liệu.

## Điểm đọc source

| Cần hiểu | Storefront | Admin |
| --- | --- | --- |
| Routes và route guards | [app/router.tsx](../../frontend/apps/storefront/src/app/router.tsx) | [app/router.tsx](../../frontend/apps/admin/src/app/router.tsx) |
| QueryClient và providers | [app/App.tsx](../../frontend/apps/storefront/src/app/App.tsx) | [app/App.tsx](../../frontend/apps/admin/src/app/App.tsx) |
| API facade và session storage | [storefrontApi.ts](../../frontend/apps/storefront/src/services/api/storefrontApi.ts) | [adminApi.ts](../../frontend/apps/admin/src/services/api/adminApi.ts) |
| Authentication | [features/customer-auth](../../frontend/apps/storefront/src/features/customer-auth) | [features/auth](../../frontend/apps/admin/src/features/auth) |
| Screen theo route | [pages](../../frontend/apps/storefront/src/pages) | [pages](../../frontend/apps/admin/src/pages) |

`pages/<route>/` chứa page và component/hook chỉ dùng cho route đó. `features/` chứa chức năng dùng qua nhiều route; `components/` chứa UI dùng chung trong app. Admin query hooks nằm tại [hooks/queries](../../frontend/apps/admin/src/hooks/queries); page nên dùng các hook này thay vì tạo query trùng lặp. Storefront còn có [cart provider](../../frontend/apps/storefront/src/features/cart) dùng chung cho layout và thao tác giỏ.

## Routing và session hiện tại

- Storefront: catalog, news, support, cart, order lookup, payment-result và account-action links có route công khai. `/checkout`, `/checkout/success` và `/account/*` dùng `CustomerProtectedRoute`; API vẫn hỗ trợ guest checkout.
- Customer access token/profile nằm trong `sessionStorage` của tab. `CustomerAuthProvider` khôi phục session qua refresh cookie khi cần và renew trước expiry. Refresh token là `HttpOnly`, `SameSite=Strict` cookie do server quản lý; không lưu nó vào JavaScript storage.
- Admin: `/login` công khai, các trang còn lại qua `ProtectedRoute`. Access session nằm trong `localStorage`; provider xóa session khi expiry/401. Admin không dùng customer refresh flow.
- Anonymous cart dùng session ID trong `localStorage`, khởi tạo từ `VITE_CART_SESSION_ID` hoặc demo fallback hiện tại; [cartSession.ts](../../frontend/apps/storefront/src/services/cartSession.ts) quản lý tạo/reset ID. Đây là cart identifier, không phải access token.

UI guard không bảo đảm ownership. Contract auth/lookup phía server nằm tại [API conventions](../backend/api/conventions.md); các giới hạn sản phẩm tại [Known limitations](../backend/development/known-limitations.md).

## API, cache và localization

Luồng đọc/ghi: page hoặc feature hook → app API facade → [ApiClient](../../frontend/packages/api-client/src/httpClient.ts) → backend. Client gửi `Authorization` khi có access token, `Accept-Language`, `credentials: include`; lỗi envelope được chuyển thành `ApiClientError` có status/errors/traceId. PDF đi qua `getBlob`, upload qua `FormData`.

TanStack Query quản lý server state; form/modal state ở component hoặc hook gần nhất. Mutation phải refresh/invalidate query liên quan. [CustomerOrderNotifications](../../frontend/apps/storefront/src/components/notifications/CustomerOrderNotifications.tsx) nhận `order_status_changed` từ `/hubs/notifications` và invalidate order list/detail.

Hai app có `i18n.ts` và `locales/vi.json`, `en.json`. Ngôn ngữ UI không đổi currency; [shared formatMoney](../../frontend/packages/shared-utils/src/formatMoney.ts) chỉ định dạng VND theo [quy ước tiền](../backend/currency-and-money.md).

## Cấu hình và phát triển

Lệnh install/dev/lint/test/typecheck/build nằm tại [Development guide](../backend/development/guide.md) và [Testing and quality](../backend/development/testing.md). Storefront chạy port 5173, admin 5174; các file `.env.example` trong app liệt kê cấu hình mẫu.

| Biến | Phạm vi và ý nghĩa |
| --- | --- |
| `VITE_API_BASE_URL` | Cả hai app; API origin, hoặc rỗng để dùng same-origin `/api` |
| `VITE_GOOGLE_CLIENT_ID` | Storefront; public Google client ID, server phải bật Google và cho phép audience tương ứng |
| `VITE_CART_SESSION_ID` | Storefront; giá trị khởi tạo giỏ demo khi chưa có ID trong storage |
| `VITE_API_PROXY_TARGET` | Storefront Vite dev server; đọc từ process environment, mặc định `http://localhost:5080` |

`VITE_*` của client được đưa vào bundle lúc build; không chứa secrets. Vite proxy hiện chỉ cấu hình `/api`, admin target cố định `http://localhost:5080`; nếu dùng same-origin cho SignalR, reverse proxy cần định tuyến `/hubs` riêng. API origin trực tiếp cần CORS/cookie topology phù hợp.

Khi đổi contract, cập nhật `api-types`, API facade, query consumers và các trạng thái loading/empty/error/auth expiry. Storefront có Vitest và Playwright smoke; admin có lint/typecheck/build và [Playwright UI regression](admin-ui-ux.md#kiểm-tra-và-bảo-trì), chưa có unit-test script. Dùng các checks tương ứng trong [Testing](../backend/development/testing.md).

## Shipping checkout and operations

Storefront capabilities select the additive shipping flow. Customers explicitly select a method and confirm an unexpired server price; changed address/cart/payment invalidates it. Unknown price is distinct from zero. An uncertain order response retains its exact request for retry. Existing login and guest-proof boundaries remain. vi/en shipping strings use their own namespace. Admin `/shipping` manages versioned fees; order detail handles reviewed payments/local delivery with revisions. Lists, detail, lookup, payment result and PDF read accepted shipping snapshots. See [shipping contracts](../backend/architecture/shipping.md), [validation](../backend/development/shipping-validation.md) and [runbook](../services/runbooks/workspace-shipping.md).

## Storefront merchant configuration

Public storefront merchant details use `VITE_SHOP_SUPPORT_EMAIL`, `VITE_SHOP_SUPPORT_PHONE`, `VITE_SHOP_BANK_NAME`, `VITE_SHOP_BANK_ACCOUNT_NAME`, and `VITE_SHOP_BANK_ACCOUNT_NUMBER` (see the storefront `.env.example`). They are bundled public information, never secrets. Missing support details are stated explicitly; all three receiving-bank fields are required before bank transfer can be selected in this storefront. Changes require a frontend rebuild/restart. The backend continues to support audited manual-transfer orders from other authorized channels.

New arrivals request `created-desc`, render at most ten items, and use five columns on desktop. The third banner tier is manual and slides horizontally; its grey arrow/dot controls have no autoplay. See `task/implementation_storefront_remediation_2026-11-11.md` for that storefront scope. The subsequent [Admin remediation](../../task/implementation_admin_remediation_2026-11-12.md) adds search, product filters and the [item return workflow](../backend/api/order-returns.md).
