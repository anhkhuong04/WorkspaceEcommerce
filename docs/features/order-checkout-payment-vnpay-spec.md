# Feature Spec: Order -> Checkout -> Payment VNPay

Ngay cap nhat: 2026-07-10

## 1. Muc Tieu

Hoan thien flow dat hang va thanh toan cho demo portfolio:

- Giu nguyen COD va Manual Bank Transfer.
- Them thanh toan online VNPay Sandbox.
- Tach payment status khoi order fulfillment status.
- Verify secure hash cho VNPay return/IPN.
- Xu ly callback idempotent de tranh mark paid hoac tao shipment nhieu lan.

## 2. Scope MVP

In scope:

- Checkout voi `PaymentMethod.VNPay`.
- Tao payment URL va redirect customer sang VNPay.
- Nhan VNPay browser return.
- Nhan VNPay IPN server-to-server.
- Luu `PaymentTransaction`.
- Mark order payment paid/failed/cancelled.
- Tao shipment sau khi VNPay thanh cong.

Out of scope:

- VNPay refund.
- Reconciliation/querydr background job.
- Outbox pattern.
- Payment provider abstraction cho nhieu gateway.
- Auto restore stock/coupon khi payment failed/cancelled.

## 3. Current Flow Alignment

Flow hien tai:

- `CheckoutService.CheckoutAsync` tao order, reserve coupon, tru stock va xoa cart trong transaction.
- Sau khi order duoc save, checkout goi MiniLogistics de tinh shipping fee va tao shipment.
- `Order` chi co `OrderStatus`; chua co `PaymentStatus`.
- `PaymentMethod` chi co `Cod` va `ManualBankTransfer`.
- `CheckoutResponse` chi tra `OrderDto`; chua co `PaymentUrl`.
- Frontend checkout thanh cong la navigate thang den `/checkout/success`.

Flow moi:

- COD/Manual van tao order va shipment ngay nhu hien tai.
- VNPay tao order va payment transaction, tra payment URL, khong tao shipment ngay.
- VNPay success moi mark paid va tao shipment neu order chua co shipment.

## 4. Domain Decisions

### PaymentMethod

```csharp
public enum PaymentMethod
{
    Cod = 0,
    ManualBankTransfer = 1,
    VNPay = 2
}
```

### PaymentStatus

```csharp
public enum PaymentStatus
{
    Unpaid = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Cancelled = 4
}
```

Initial payment status:

- COD: `Unpaid`.
- Manual Bank Transfer: `Pending`.
- VNPay: `Pending`.

Payment status rules:

- `Paid` is terminal for this MVP.
- `Failed`/`Cancelled` cannot overwrite `Paid`.
- Duplicate VNPay success for an already paid transaction must return idempotent success and skip side effects.

## 5. PaymentTransaction

Create entity `PaymentTransaction` in schema `payments`.

Fields:

- `Id`
- `OrderId`
- `Provider` (`VNPay`)
- `Status` (`Pending`, `Success`, `Failed`, `Cancelled`)
- `Amount`
- `CurrencyCode`
- `TxnRef`
- `GatewayTransactionNo`
- `GatewayResponseCode`
- `GatewayResponseMessage`
- `SecureHash`
- `RawResponse`
- `CreatedAt`
- `ProcessedAt`

Indexes:

- Unique `TxnRef`.
- Index `OrderId`.

`TxnRef` decision:

- Use a unique merchant reference derived from order code plus short random suffix if needed.
- Store it in `PaymentTransaction.TxnRef`.
- Send it to VNPay as `vnp_TxnRef`.

## 6. VNPay Amount Convention

For VNPay payment URL requests:

- Internal order amount is stored as VND decimal.
- `vnp_Amount` sent to VNPay must be an integer string equal to `order.TotalAmount * 100`.
- Example: `10000 VND` -> `vnp_Amount=1000000`.

Implementation guard:

- Round/convert only at the payment boundary.
- Do not mutate internal order totals for gateway formatting.
- Before production, re-check the current VNPay Merchant Portal/Sandbox document because public detailed docs are not consistently accessible.

## 7. URL Decisions

Backend API base in local dev:

- `http://localhost:5080`

Storefront base in local dev:

- `http://localhost:5173`

VNPay payment request:

- `vnp_ReturnUrl = {BackendBaseUrl}/api/payments/vnpay/return`

VNPay IPN:

- `Payment:VNPay:IpnUrl = {BackendBaseUrl}/api/payments/vnpay/ipn`

Frontend result URLs:

- Success: `{StorefrontBaseUrl}/checkout/payment-result?status=success&orderCode={orderCode}`
- Failed: `{StorefrontBaseUrl}/checkout/payment-result?status=failed&orderCode={orderCode}`
- Cancelled: `{StorefrontBaseUrl}/checkout/payment-result?status=cancelled&orderCode={orderCode}`

Backend return behavior:

- `GET /api/payments/vnpay/return` verifies hash and updates payment.
- Then it redirects browser to the storefront result URL.

Backend IPN behavior:

- `GET /api/payments/vnpay/ipn` handles server-to-server callback.
- It verifies hash, updates payment idempotently and returns VNPay-compatible response.
- If the transaction is already terminal, return success without re-running shipment creation.

## 8. Checkout Response Decision

Extend `CheckoutResponse`:

```csharp
public sealed record CheckoutResponse(
    OrderDto Order,
    bool PaymentRequired,
    string? PaymentUrl);
```

Frontend behavior:

- If `PaymentRequired` and `PaymentUrl` exists, redirect browser to `PaymentUrl`.
- Otherwise navigate to `/checkout/success` as today.

## 9. Shipment Decision

COD/Manual:

- Keep current behavior: create shipment after checkout.

VNPay:

- Do not create shipment during checkout.
- Create shipment after payment success.
- If shipment creation fails after payment success:
  - Keep payment as paid.
  - Log warning.
  - Leave order without `ShipmentId` for manual/admin handling.

## 10. Security & Idempotency

- Generate secure hash using HMAC-SHA512.
- Verify secure hash before trusting any return/IPN parameter.
- Exclude `vnp_SecureHash` and `vnp_SecureHashType` from hash data.
- Sort parameters by key before signing/verifying.
- Lookup transaction by `vnp_TxnRef`.
- Only `Pending` transactions can run state-changing side effects.
- Terminal transaction statuses return idempotent success.

## 11. Response Code Mapping MVP

- `vnp_ResponseCode == "00"` and transaction status success -> `PaymentStatus.Paid`, `PaymentTransactionStatus.Success`.
- Known user cancel code -> `PaymentStatus.Cancelled`, `PaymentTransactionStatus.Cancelled`.
- Other non-success codes -> `PaymentStatus.Failed`, `PaymentTransactionStatus.Failed`.

Exact cancel code mapping must be confirmed during implementation against the VNPay sandbox document/config available to the developer account.

## 12. Acceptance Criteria

1. COD checkout still creates order and shipment without payment URL.
2. Manual transfer checkout still creates order and shipment without payment URL.
3. VNPay checkout creates order, pending payment transaction and returns payment URL.
4. VNPay checkout does not create shipment before payment success.
5. VNPay success return/IPN marks order paid and creates shipment once.
6. Duplicate success callback does not create duplicate shipment or rewrite terminal transaction.
7. Tampered secure hash is rejected and does not update payment.
8. Failed/cancelled payment does not create shipment.
