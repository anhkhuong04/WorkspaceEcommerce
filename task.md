# Project Task Board

Ngay cap nhat: 2026-07-10

## Task Active - Order -> Checkout -> Payment VNPay

Ngay bat dau: 2026-07-10

## Muc Tieu

Hoan thien flow Order -> Checkout -> Payment cho demo portfolio:

1. Giu flow COD va Manual Bank Transfer hien co.
2. Them thanh toan online VNPay Sandbox.
3. Tach trang thai thanh toan khoi trang thai xu ly/giao hang cua order.
4. Dam bao VNPay return/IPN verify secure hash va idempotent.
5. Chi tao shipment cho VNPay sau khi payment thanh cong.
6. Cap nhat storefront checkout de redirect sang VNPay va hien ket qua thanh toan.

## Quyet Dinh MVP

- Them `PaymentMethod.VNPay = 2`.
- Them `PaymentStatus` tren `Order`: `Unpaid`, `Pending`, `Paid`, `Failed`, `Cancelled`.
- COD va Manual Bank Transfer tao order nhu hien tai, payment status ban dau:
  - COD: `Unpaid`.
  - Manual Bank Transfer: `Pending`.
- VNPay checkout tao order + payment transaction + payment url, frontend redirect sang VNPay.
- VNPay success moi mark `PaymentStatus.Paid`.
- VNPay failed/cancel mark `PaymentStatus.Failed` hoac `Cancelled`, khong auto restore stock trong v1.
- Shipment:
  - COD/Manual: giu flow tao shipment sau checkout nhu hien tai.
  - VNPay: tao shipment sau khi payment success.
- Khong lam refund VNPay trong task nay.
- Khong lam reconciliation/background job trong v1.
- Khong them outbox pattern trong v1, nhung payment handling phai idempotent.
- Tat ca VNPay config doc tu options/env, khong hardcode secret.

## Phan Tich Flow Hien Tai

- `CheckoutService.CheckoutAsync` dang tao order, reserve coupon, tru stock, xoa cart trong transaction.
- Sau transaction, service goi MiniLogistics de tinh shipping fee va tao shipment.
- `Order` hien chi co `OrderStatus`, chua co `PaymentStatus`.
- `PaymentMethod` hien chi co `Cod` va `ManualBankTransfer`.
- `CheckoutResponse` hien chi tra `OrderDto`, chua co `PaymentUrl`/payment action.
- Frontend checkout submit xong luon navigate `/checkout/success`, chua co branch redirect gateway.
- MiniLogistics webhook co the chuyen order den `Completed`; loyalty earn dang trigger khi admin update order sang `Completed`, chua trigger tu webhook.

## Phase 0: Spec Alignment & Design Guardrails

- [x] 0.1 Cap nhat `task.md` voi plan VNPay MVP va cac quyet dinh tren.
- [x] 0.2 Neu co file spec payment/order checkout rieng, tao/cap nhat spec ngan trong `docs/features`.
- [x] 0.3 Xac nhan convention VNPay amount: gateway amount = VND amount * 100.
- [x] 0.4 Chot return URL va IPN URL:
  - Return URL cho browser redirect ve storefront/backend.
  - IPN URL cho server-to-server callback.
- [x] 0.5 Chot frontend URL sau payment:
  - Success: `/checkout/payment-result?status=success&orderCode=...`
  - Failed/cancel: `/checkout/payment-result?status=failed&orderCode=...`

## Phase 1: Domain Model

- [x] 1.1 Them enum `PaymentStatus` vao domain ordering/payment.
- [x] 1.2 Mo rong `PaymentMethod` them `VNPay = 2`.
- [x] 1.3 Them property payment vao `Order`:
  - `PaymentStatus`.
  - `PaidAt` nullable neu can hien thi/audit.
- [x] 1.4 Them domain methods tren `Order`:
  - `MarkPaymentPending()`.
  - `MarkPaymentPaid(paidAt)`.
  - `MarkPaymentFailed()`.
  - `MarkPaymentCancelled()`.
- [x] 1.5 Validate domain rules:
  - Paid khong duoc chuyen lai Pending.
  - Failed/Cancelled khong duoc overwrite Paid.
  - COD/Manual/VNPay initial status duoc set dung tai checkout.
- [x] 1.6 Tao entity `PaymentTransaction`.
- [x] 1.7 Tao enum:
  - `PaymentProvider` (`VNPay`).
  - `PaymentTransactionStatus` (`Pending`, `Success`, `Failed`, `Cancelled`).
- [x] 1.8 `PaymentTransaction` fields MVP:
  - `Id`, `OrderId`, `Provider`, `Status`.
  - `Amount`, `CurrencyCode`.
  - `TxnRef` unique.
  - `GatewayTransactionNo` nullable.
  - `GatewayResponseCode` nullable.
  - `GatewayResponseMessage` nullable.
  - `SecureHash` nullable.
  - `RawResponse` nullable.
  - `CreatedAt`, `ProcessedAt`.
- [x] 1.9 Domain tests cho payment status va transaction state changes.

## Phase 2: Persistence & Migration

- [x] 2.1 Them `PaymentTransactions` vao `IAppDbContext` va `AppDbContext`.
- [x] 2.2 Cap nhat `OrderConfiguration` map `PaymentStatus`, `PaidAt`.
- [x] 2.3 Tao `PaymentTransactionConfiguration`.
- [x] 2.4 Dat payment table trong schema `payments`.
- [x] 2.5 Cau hinh FK `payment_transactions.order_id -> ordering.orders.id`.
- [x] 2.6 Them unique index:
  - `ux_payment_transactions_txn_ref`.
  - optional `ix_payment_transactions_order_id`.
- [x] 2.7 Tao migration `AddPaymentSchema`.
- [x] 2.8 Cap nhat model configuration tests.

## Phase 3: VNPay Infrastructure

- [x] 3.1 Tao abstraction `IVNPayPaymentService` hoac `IPaymentGatewayClient`.
- [x] 3.2 Tao `VNPayOptions`:
  - `TmnCode`.
  - `HashSecret`.
  - `PaymentUrl`.
  - `ReturnUrl`.
  - `IpnUrl`.
  - `Version`.
  - `Command`.
  - `Locale`.
  - `CurrCode`.
- [x] 3.3 Dang ky options va DI trong Infrastructure/API.
- [x] 3.4 Implement VNPay payment URL builder.
- [x] 3.5 Implement secure hash generation HMAC-SHA512 theo sorted query params.
- [x] 3.6 Implement secure hash verification cho return/IPN.
- [x] 3.7 Them helper map VNPay response code:
  - `00` success.
  - user cancel/failed codes -> failed/cancelled theo mapping MVP.
- [x] 3.8 Unit tests cho:
  - URL builder co required params.
  - amount nhan 100.
  - secure hash stable.
  - verify hash reject tampered params.

## Phase 4: Checkout Application Changes

- [x] 4.1 Mo rong `CheckoutRequestValidator` chap nhan `PaymentMethod.VNPay`.
- [x] 4.2 Mo rong `CheckoutResponse`:
  - `Order`.
  - `PaymentUrl` nullable.
  - `PaymentRequired` bool hoac `NextAction`.
- [x] 4.3 Refactor `CheckoutService` tach cac buoc:
  - Build snapshots.
  - Evaluate coupon.
  - Create order.
  - Reserve stock/coupon.
  - Create shipment.
- [x] 4.4 Trong transaction checkout:
  - COD: `PaymentStatus.Unpaid`.
  - Manual: `PaymentStatus.Pending`.
  - VNPay: `PaymentStatus.Pending`, tao `PaymentTransaction.Pending`.
- [x] 4.5 VNPay checkout tra `PaymentUrl` sau khi order/payment transaction save thanh cong.
- [x] 4.6 Chi tao shipment ngay cho COD/Manual.
- [x] 4.7 Khong tao shipment trong `CheckoutAsync` cho VNPay truoc khi payment success.
- [x] 4.8 Dam bao coupon reservation/stock tru van duoc thuc hien o checkout de tranh oversell trong demo v1.
- [x] 4.9 Application tests:
  - COD checkout khong co payment URL.
  - Manual checkout khong co payment URL.
  - VNPay checkout co payment URL va payment transaction pending.
  - VNPay checkout khong tao shipment truoc payment success.

## Phase 5: Payment Application Service

- [x] 5.1 Tao module `Modules/Payments`.
- [x] 5.2 Tao DTO/request:
  - `VNPayReturnRequest` hoac parse query model.
  - `PaymentResultDto`.
  - `PaymentTransactionDto` neu can.
- [x] 5.3 Tao `IPaymentService` voi:
  - `HandleVNPayReturnAsync`.
  - `HandleVNPayIpnAsync`.
  - `GetPaymentResultAsync(orderCode, phone?)` neu can cho frontend.
- [x] 5.4 Implement return/IPN handling:
  - Verify secure hash truoc.
  - Tim transaction theo `vnp_TxnRef`.
  - Neu transaction da terminal -> tra success idempotent.
  - Neu success -> mark transaction success, order paid.
  - Neu failed/cancel -> mark transaction failed/cancelled, order failed/cancelled payment.
- [x] 5.5 Sau VNPay success, tao shipment cho order neu chua co `ShipmentId`.
- [x] 5.6 Payment success khong auto chuyen `OrderStatus` sang `Confirmed` trong MVP, tru khi can chot lai.
- [x] 5.7 Catch shipment failure sau payment success: payment van paid, log warning, order giu pending shipment.
- [x] 5.8 Tests:
  - Success return mark paid.
  - Duplicate success return khong tao shipment lan 2.
  - Failed return mark failed.
  - Tampered hash bi reject.
  - Unknown txn ref not found.

## Phase 6: API Endpoints

- [x] 6.1 Tao `PaymentsController`.
- [x] 6.2 Implement `GET /api/payments/vnpay/return`.
- [x] 6.3 Implement `GET` hoac `POST /api/payments/vnpay/ipn` theo VNPay sandbox yeu cau.
- [x] 6.4 Return endpoint:
  - Xu ly payment.
  - Redirect ve storefront payment result page hoac tra API response, chot theo Phase 0.
- [x] 6.5 IPN endpoint:
  - Tra response format VNPay mong doi.
  - Idempotent voi duplicate callback.
- [x] 6.6 Them response wrapper/HTTP status phu hop voi pattern hien co.
- [x] 6.7 API integration tests cho return/IPN.

## Phase 7: Frontend Shared Types & Client

- [x] 7.1 Cap nhat `PaymentMethod` type thanh `0 | 1 | 2`.
- [x] 7.2 Them `PaymentStatus` type.
- [x] 7.3 Cap nhat `OrderDto` co `paymentStatus`, `paidAt`.
- [x] 7.4 Cap nhat `CheckoutResponse` co `paymentUrl`/`paymentRequired`.
- [x] 7.5 Cap nhat `formatPaymentMethod`, them label VNPay.
- [x] 7.6 Them `formatPaymentStatus` neu can hien thi.
- [x] 7.7 Cap nhat api-client neu them endpoint payment result.
- [x] 7.8 Chay typecheck shared packages.

## Phase 8: Storefront UI

- [x] 8.1 Them payment option VNPay tren checkout page.
- [x] 8.2 Khi checkout response co `paymentUrl`, redirect browser sang VNPay.
- [x] 8.3 Giu COD/Manual flow di thang `/checkout/success`.
- [x] 8.4 Tao `PaymentResultPage` hoac mo rong checkout success page:
  - Loading/lookup payment result.
  - Success paid.
  - Failed/cancelled.
  - Link order lookup.
- [x] 8.5 Hien `PaymentStatus` trong checkout success/order lookup/account orders.
- [x] 8.6 Dam bao manual transfer panel chi hien voi `ManualBankTransfer`.
- [x] 8.7 Chay storefront typecheck/build.

## Phase 9: Admin UI

- [x] 9.1 Cap nhat admin order list/detail hien payment method + payment status.
- [x] 9.2 Them filter payment status neu UI hien co phu hop, khong bat buoc v1.
- [x] 9.3 Manual bank transfer:
  - Giu admin update order status nhu hien tai.
  - Neu can, them action mark payment paid cho manual transfer o phase rieng.
- [x] 9.4 Chay admin typecheck/build.

## Phase 10: Verification

- [x] 10.1 Chay domain/application tests payment + checkout.
- [x] 10.2 Chay infrastructure tests migration/model config.
- [x] 10.3 Chay API integration tests checkout + VNPay return/IPN.
- [x] 10.4 Chay backend build/test solution.
- [x] 10.5 Chay frontend typecheck/build.
- [ ] 10.6 Manual demo flow:
  - COD checkout -> order success.
  - Manual checkout -> transfer info.
  - VNPay checkout -> redirect sandbox.
  - VNPay success -> payment paid -> shipment created.
  - VNPay failed/cancel -> payment failed/cancelled -> no shipment.
  - Duplicate VNPay callback -> khong doi/trung side effect.
  - Ghi chu 2026-07-10: Chua chay manual browser/VNPay sandbox vi can sandbox credentials va runtime config thuc. Automated tests da cover COD checkout, VNPay return/IPN success-failed-cancelled-duplicate va shipment sau payment success.

## Da Hoan Thanh Gan Day

### Customer Loyalty Program

Trang thai: Done

- Tu dong tich diem khi order customer chuyen sang `Completed`.
- Quan ly account diem, tier, transaction history.
- Customer redeem diem thanh loyalty voucher customer-scoped.
- Tich hop checkout coupon validation cho voucher theo customer.
- Them API, frontend storefront account page, shared api types/client.
- Verification da chay cho flow loyalty chinh.

Ghi chu:

- Guest order khong tich diem.
- Diem earn: `floor((Subtotal - DiscountAmount) * ExchangeRate / Loyalty:MoneyPerPoint)`.
- Default `Loyalty:MoneyPerPoint = 10000`.
- Default `Loyalty:VoucherAmountPerPoint = 1000`.
- Tier benefits hien la display-only.
- Order da `Completed` roi bi `Returned` chua tu dong tru diem trong v1.

### MiniLogistics Shipment Integration

Trang thai: Done

- Them dimensions/weight cho product variants.
- Them shipping quote vao checkout.
- Luu tracking code/shipment id tren order.
- Them MiniLogistics client, config va webhook endpoint.
- Cap nhat frontend checkout hien phi ship.

### News/Blogs Feature

Trang thai: Done

- Admin CRUD blog posts, publish/unpublish, related products, comment moderation.
- Storefront list/detail news va comment submission.
- Them schema `content`, API, frontend shared client/types va UI.

## Tam Hoan

### Return/Refund

Ly do tam hoan:

- Spec yeu cau VNPay Refund API va `PaymentTransaction`, nhung code hien tai chua co payment gateway/payment transaction module.
- Spec dung `Delivered`/`DeliveredAt`, trong khi order flow hien tai dung `Completed`.
- Can thay endpoint return hien tai dang doi thang order sang `Returned` bang return request workflow rieng.
- Loyalty hien khong cho balance am, trong khi refund adjustment co the can diem am tam thoi.

Khi quay lai task nay, can chot:

- V1 manual refund truoc hay bat buoc VNPay.
- Dung `Completed` hay them `Delivered`/`DeliveredAt`.
- Co cho partial return va nhieu return request/order khong.
- Refund co tinh shipping/coupon allocation khong.
- Loyalty adjustment co duoc lam balance am khong.

## Known Verification Notes

- Full backend solution tests pass ngay 2026-07-10: Application 251, Infrastructure 147, API integration 46.
- Frontend workspace typecheck/build pass ngay 2026-07-10.
- Con lai manual VNPay sandbox/browser flow can chay khi co sandbox credentials va backend/storefront runtime config that.
