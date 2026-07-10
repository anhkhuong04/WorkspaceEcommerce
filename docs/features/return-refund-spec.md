# Feature Spec: Return/Refund (Xử lý trả hàng & Hoàn tiền)

> **Đối tượng đọc:** Coding agent triển khai trên dự án E-commerce (ASP.NET Core Web API + PostgreSQL + React/TypeScript, Clean Architecture). Thanh toán online qua VNPay Sandbox.
> **Phụ thuộc:** Feature này tích hợp với module Order (đơn hàng), Payment (VNPay), Inventory (kho), và Loyalty (đã có spec riêng — xem `loyalty-program-spec.md`). Đọc kỹ phần 9 trước khi động vào code Loyalty.
> **Độ phức tạp:** Cao hơn Loyalty — có state machine đa tác nhân, webhook bất đồng bộ, và tiền thật (sandbox) chảy ngược. Không rút gọn các bước concurrency/idempotency dù có vẻ "chỉ là đồ án".

---

## 1. Business Context

Xử lý toàn bộ vòng đời: khách yêu cầu trả hàng → admin/CS duyệt → khách gửi hàng → kho kiểm tra → hoàn tiền → đóng luồng. Rủi ro chính cần thiết kế để phòng tránh: hoàn tiền trùng (double refund), hoàn tiền sai số tiền, restock hàng hỏng vào kho, và mất dấu vết đối soát tài chính.

---

## 2. Scope

### In scope
- Yêu cầu trả hàng một phần hoặc toàn bộ đơn hàng, trong window cho phép sau khi giao
- Luồng duyệt thủ công bởi admin/CS (approve/reject)
- Ghi nhận tình trạng hàng khi nhận lại (kiểm hàng)
- Hoàn tiền qua VNPay Refund API (đơn thanh toán online) hoặc đánh dấu hoàn thủ công (đơn COD)
- Idempotent webhook handling cho callback hoàn tiền từ VNPay
- Điều chỉnh điểm loyalty tương ứng phần hàng bị trả (gọi qua Loyalty module)
- Restock inventory khi hàng trả về còn tốt

### Out of scope
- Trả hàng đổi hàng khác (exchange) — chỉ hỗ trợ hoàn tiền, không hỗ trợ đổi sản phẩm
- Tích hợp shipper thật để lấy hàng trả về (mô phỏng thủ công, không nối với dự án Logistics ở giai đoạn này)
- Refund một phần theo % (chỉ hoàn theo item cụ thể, không có "hoàn 50% giá trị đơn")
- Multi-currency

---

## 3. State Machine

### 3.1 Trạng thái

```csharp
public enum ReturnRequestStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    AwaitingReturn = 3,   // đã approve, chờ khách gửi hàng về
    Received = 4,         // kho đã nhận được hàng
    Inspected = 5,         // kho đã kiểm tra, xác định ItemCondition
    RefundInitiated = 6,  // đã gọi VNPay refund API, chờ kết quả
    Refunded = 7,          // hoàn tiền thành công — trạng thái cuối
    RefundFailed = 8,      // hoàn tiền thất bại — cần can thiệp thủ công
    Cancelled = 9           // khách tự huỷ yêu cầu trước khi Approved
}
```

### 3.2 Bảng chuyển trạng thái hợp lệ

| Từ | Đến | Điều kiện / Actor |
|---|---|---|
| `Requested` | `Approved` | Admin duyệt |
| `Requested` | `Rejected` | Admin từ chối |
| `Requested` | `Cancelled` | Khách tự huỷ (chỉ khi chưa duyệt) |
| `Approved` | `AwaitingReturn` | Tự động ngay sau Approve |
| `AwaitingReturn` | `Received` | Kho xác nhận đã nhận hàng |
| `Received` | `Inspected` | Kho nhập kết quả kiểm tra từng item |
| `Inspected` | `RefundInitiated` | Hệ thống tự động trigger sau Inspected |
| `RefundInitiated` | `Refunded` | VNPay IPN callback báo thành công |
| `RefundInitiated` | `RefundFailed` | VNPay IPN callback báo thất bại, hoặc timeout không nhận được callback sau N phút |
| `RefundFailed` | `RefundInitiated` | Admin retry thủ công |

> **Ràng buộc bắt buộc khi code:** Mọi transition PHẢI đi qua một domain method duy nhất (ví dụ `ReturnRequest.TransitionTo(newStatus, actor)`), không cho phép set trực tiếp property `Status` từ Application layer. Method này validate bảng transition ở trên — transition không hợp lệ phải throw domain exception, không âm thầm bỏ qua.

---

## 4. Domain Model

```csharp
public class ReturnRequest
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public ReturnRequestStatus Status { get; private set; }
    public string Reason { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }         // AdminId
    public string? ReviewNote { get; private set; }
    public uint RowVersion { get; private set; }

    private readonly List<ReturnRequestItem> _items = new();
    public IReadOnlyCollection<ReturnRequestItem> Items => _items.AsReadOnly();

    public void TransitionTo(ReturnRequestStatus newStatus, Guid? actorId, string? note);
    public decimal CalculateRefundAmount();  // tổng theo UnitPriceAtOrderTime * Quantity của items được approve
}

public class ReturnRequestItem
{
    public Guid Id { get; private set; }
    public Guid ReturnRequestId { get; private set; }
    public Guid OrderItemId { get; private set; }          // FK về OrderItem gốc — bắt buộc để lấy đúng giá lúc mua
    public int Quantity { get; private set; }
    public ItemCondition? Condition { get; private set; }  // null cho tới khi kiểm hàng
}

public enum ItemCondition { Good = 0, Damaged = 1, Missing = 2 }

public class RefundTransaction
{
    public Guid Id { get; private set; }
    public Guid ReturnRequestId { get; private set; }
    public Guid OriginalPaymentTransactionId { get; private set; }  // BẮT BUỘC — trỏ về giao dịch thanh toán gốc
    public decimal Amount { get; private set; }
    public RefundMethod Method { get; private set; }               // VNPayGateway | ManualCOD
    public string? GatewayReferenceCode { get; private set; }       // vnp_TransactionNo trả về từ VNPay
    public RefundTransactionStatus Status { get; private set; }     // Pending | Success | Failed
    public DateTime? ProcessedAt { get; private set; }
    public string IdempotencyKey { get; private set; }              // = ReturnRequestId, dùng chặn gọi API refund trùng
}

public enum RefundMethod { VNPayGateway, ManualCOD }
public enum RefundTransactionStatus { Pending, Success, Failed }
```

---

## 5. Database Schema (PostgreSQL DDL)

```sql
CREATE TABLE return_requests (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id        UUID NOT NULL REFERENCES orders(id),
    customer_id     UUID NOT NULL REFERENCES customers(id),
    status          SMALLINT NOT NULL DEFAULT 0,
    reason          TEXT NOT NULL,
    requested_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    reviewed_at     TIMESTAMPTZ NULL,
    reviewed_by     UUID NULL REFERENCES admins(id),
    review_note     TEXT NULL,
    row_version     BIGINT NOT NULL DEFAULT 0
);

CREATE TABLE return_request_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    return_request_id   UUID NOT NULL REFERENCES return_requests(id),
    order_item_id        UUID NOT NULL REFERENCES order_items(id),
    quantity             INT NOT NULL CHECK (quantity > 0),
    condition             SMALLINT NULL,

    -- Chặn 1 OrderItem bị đưa vào 2 return request đang active cùng lúc
    CONSTRAINT uq_order_item_active_return UNIQUE (order_item_id)
);

CREATE TABLE refund_transactions (
    id                                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    return_request_id                 UUID NOT NULL REFERENCES return_requests(id),
    original_payment_transaction_id   UUID NOT NULL REFERENCES payment_transactions(id),
    amount                             NUMERIC(12,2) NOT NULL CHECK (amount > 0),
    method                              SMALLINT NOT NULL,
    gateway_reference_code             TEXT NULL,
    status                              SMALLINT NOT NULL DEFAULT 0,
    processed_at                        TIMESTAMPTZ NULL,
    idempotency_key                    TEXT NOT NULL UNIQUE   -- = return_request_id.ToString()
);
```

> **Lưu ý về `uq_order_item_active_return`:** constraint này ở mức đơn giản hoá (1 OrderItem chỉ từng nằm trong 1 return request duy nhất, kể cả bị Rejected). Nếu muốn cho phép request lại sau khi bị Rejected, cần đổi thành partial unique index chỉ áp dụng khi `status NOT IN (Rejected, Cancelled)` — ghi rõ quyết định này trong PR description khi agent implement, đừng âm thầm chọn 1 trong 2 hướng.

---

## 6. Business Rules

| # | Rule | Ghi chú |
|---|---|---|
| BR-1 | Chỉ tạo `ReturnRequest` khi `Order.Status = Delivered` và trong vòng 7 ngày kể từ `DeliveredAt` | Số ngày lấy từ config, không hardcode |
| BR-2 | `Quantity` trả ≤ `OrderItem.Quantity`, và một `OrderItemId` không được xuất hiện ở 2 return request đang active | Enforce ở DB (xem mục 5) lẫn application layer |
| BR-3 | `CalculateRefundAmount()` dùng `OrderItem.UnitPriceAtOrderTime`, KHÔNG query giá hiện tại của sản phẩm | Giá sản phẩm có thể đổi theo thời gian |
| BR-4 | Restock inventory chỉ khi `ItemCondition = Good` | Hàng `Damaged`/`Missing` không đưa lại kho, ghi log riêng cho báo cáo tổn thất |
| BR-5 | `RefundTransaction.IdempotencyKey` unique theo `ReturnRequestId` — không tạo 2 refund transaction cho cùng 1 return request dù retry bao nhiêu lần | Xem mục 8.3 |
| BR-6 | Đơn COD không gọi VNPay API — set `Method = ManualCOD`, admin tự xác nhận đã hoàn tiền qua kênh khác (chuyển khoản) rồi mới chuyển `Status = Refunded` | Không có gateway callback cho case này |
| BR-7 | Sau khi `Status = Refunded`, hệ thống tự động gọi Loyalty module để trừ lại điểm tương ứng phần hàng trả (nếu đơn gốc đã tích điểm) | Xem mục 9 |

---

## 7. Return Eligibility Service

Tách riêng thành domain service độc lập, test được mà không cần dựng toàn bộ HTTP pipeline:

```csharp
public interface IReturnEligibilityService
{
    Task<EligibilityResult> CheckAsync(Guid orderId, List<(Guid OrderItemId, int Quantity)> requestedItems);
}
```

Logic cần kiểm tra theo đúng thứ tự (fail-fast, trả lý do cụ thể chứ không chỉ `false`):
1. Order tồn tại và thuộc về customer đang request
2. `Order.Status == Delivered`
3. `now - Order.DeliveredAt <= ReturnWindowDays`
4. Từng `OrderItemId` thuộc đúng order này
5. `Quantity` request ≤ `Quantity` đã mua trừ đi số đã nằm trong return request khác đang active
6. Không có return request nào khác của order này đang ở trạng thái chưa kết thúc (`Requested`/`Approved`/`AwaitingReturn`/...) — tuỳ quyết định nghiệp vụ có cho phép nhiều return request song song trên 1 order hay không; **mặc định spec này: KHÔNG cho phép**, mỗi order chỉ có 1 return request active tại một thời điểm.

---

## 8. VNPay Refund Integration (phần khó nhất)

### 8.1 Đặc điểm khác biệt so với luồng thanh toán

Thanh toán ban đầu là **redirect flow** (khách được chuyển tới trang VNPay, quay về qua Return URL). Refund là **server-to-server API call** — hệ thống chủ động gọi VNPay, KHÔNG có bước redirect người dùng. Kết quả trả về theo 2 kênh:
- **Response đồng bộ** của chính API call (thường chỉ xác nhận đã "nhận yêu cầu", chưa chắc đã "xử lý xong")
- **IPN callback bất đồng bộ** gửi tới `Return URL`/`IPN URL` đã đăng ký — đây mới là nguồn xác nhận thực sự đáng tin

> **Quan trọng:** field name chính xác của VNPay Refund API (`vnp_RequestId`, `vnp_TmnCode`, `vnp_TransactionType`, `vnp_TxnRef`, `vnp_Amount`, `vnp_TransactionNo`, `vnp_TransactionDate`, `vnp_CreateBy`, `vnp_SecureHash`...) cần đối chiếu lại với tài liệu VNPay Sandbox tại thời điểm triển khai, vì cổng thanh toán có thể cập nhật spec. Agent nên fetch tài liệu chính thức trước khi code phần request builder, không hardcode field theo trí nhớ.

### 8.2 Luồng gọi refund

```
1. ReturnRequest chuyển sang Inspected
   ↓
2. Domain event ReturnInspectedEvent { ReturnRequestId }
   ↓
3. Handler tạo RefundTransaction (Status=Pending, IdempotencyKey=ReturnRequestId)
   ↓
4. Build request tới VNPay refund API, ký secure hash bằng HMAC-SHA512 (giống luồng thanh toán đã làm)
   ↓
5. Gửi request → nhận response đồng bộ → ReturnRequest.Status = RefundInitiated
   ↓
6. Chờ IPN callback (async) → verify secure hash → update RefundTransaction.Status
   ↓
7a. Success → ReturnRequest.Status = Refunded → trigger Loyalty adjustment + Inventory restock (nếu Good)
7b. Failed  → ReturnRequest.Status = RefundFailed → alert admin, cho phép retry thủ công
```

### 8.3 Idempotency cho webhook

- Endpoint nhận IPN (`POST /api/payments/vnpay/refund-ipn`) phải:
  1. Verify `vnp_SecureHash` trước khi xử lý bất cứ gì (chặn giả mạo callback)
  2. Tìm `RefundTransaction` theo `IdempotencyKey` (map từ `vnp_TxnRef` hoặc field tương ứng)
  3. Nếu `RefundTransaction.Status` đã là `Success`/`Failed` (không còn `Pending`) → trả về response OK cho VNPay ngay, KHÔNG xử lý lại, KHÔNG update lần 2
  4. Chỉ khi đang `Pending` mới update trạng thái và trigger các side-effect (Loyalty, Inventory)
- Side-effect (trừ điểm loyalty, restock kho) phải nằm trong cùng transaction hoặc dùng outbox pattern để đảm bảo không bị chạy 2 lần nếu bước 3 có race condition nhẹ (2 IPN đến gần như đồng thời) — an toàn nhất là lock theo `RefundTransaction.Id` khi update.

### 8.4 Reconciliation job (bù cho trường hợp IPN không đến)

Callback có thể bị mất do lỗi mạng. Cần 1 background job (chạy định kỳ, ví dụ mỗi 15 phút):
- Query mọi `RefundTransaction` có `Status = Pending` và `CreatedAt` quá X phút
- Với mỗi cái, gọi VNPay Query Transaction API (`querydr`) để chủ động hỏi lại kết quả thực tế, thay vì chờ callback
- Update theo kết quả trả về, dùng đúng cơ chế idempotent ở mục 8.3

---

## 9. Tích hợp với Loyalty Module

Khi `ReturnRequest.Status = Refunded`:
1. Publish `ReturnRefundedEvent { ReturnRequestId, OrderId, CustomerId, RefundedItemsValue }`
2. Loyalty module lắng nghe, tính số điểm cần trừ tương ứng theo cùng công thức đã dùng lúc Earn (`points = floor(RefundedItemsValue / 10000)`)
3. Gọi `CustomerLoyaltyAccount` tạo `LoyaltyTransaction` với `Type = Adjust`, `Points` âm, `OrderId` = order gốc
4. Nếu `CurrentPoints` sau khi trừ sẽ âm (khách đã tiêu hết điểm) → **cho phép âm tạm thời trong trường hợp Adjust** (khác với Redeem — BR-5 ở spec Loyalty chỉ áp dụng cho Redeem chủ động của khách), ghi log cảnh báo để admin biết, không throw exception chặn luồng refund vì tiền là ưu tiên cao hơn điểm thưởng.

> Đây chính là điểm nối giữa 2 spec — nếu agent implement Loyalty trước và đã raise `CurrentPoints >= 0` như một constraint cứng ở DB (`CHECK (current_points >= 0)`), cần sửa lại CHECK constraint đó hoặc xử lý riêng path này trước khi code phần 9.

---

## 10. API Endpoints

```
POST /api/orders/{orderId}/return-requests
     Body: { reason, items: [{ orderItemId, quantity }] }
     → validate qua IReturnEligibilityService trước khi tạo
     → 201, trả về ReturnRequest

GET  /api/return-requests/{id}
     → chi tiết + items + trạng thái hiện tại

GET  /api/return-requests/me?status=&page=&pageSize=
     → danh sách của khách hàng đang đăng nhập

POST /api/admin/return-requests/{id}/approve
     Body: { note? }
     → chỉ admin, transition Requested → Approved

POST /api/admin/return-requests/{id}/reject
     Body: { note }  // bắt buộc note khi reject
     → transition Requested → Rejected

POST /api/admin/return-requests/{id}/receive
     → transition AwaitingReturn → Received (kho xác nhận đã nhận hàng)

POST /api/admin/return-requests/{id}/inspect
     Body: { items: [{ returnRequestItemId, condition }] }
     → transition Received → Inspected, tự động trigger refund flow ở mục 8.2

POST /api/admin/return-requests/{id}/retry-refund
     → chỉ cho phép khi Status = RefundFailed

POST /api/payments/vnpay/refund-ipn
     → webhook nội bộ nhận từ VNPay, xem mục 8.3 (không expose qua Swagger public)
```

Tất cả endpoint `/api/admin/*` yêu cầu role Admin/CS, không dùng chung policy với endpoint khách hàng.

---

## 11. Edge Cases

- Khách gửi request trả hàng nhưng sau đó `Order` bị xoá/đổi trạng thái bởi luồng khác → dùng optimistic concurrency (`row_version`) khi transition, catch conflict và trả lỗi rõ ràng thay vì stack trace.
- Đơn hàng thanh toán COD nhưng có `PaymentTransaction` ghi nhận thủ công (ví dụ tiền mặt) → `RefundMethod = ManualCOD` vẫn cần `OriginalPaymentTransactionId` hợp lệ, không để null — nếu chưa có payment transaction cho COD, phải bổ sung trước khi làm Refund feature.
- Admin approve rồi reject nhầm (đổi ý) → **không cho phép** transition ngược `Approved → Rejected` theo bảng ở mục 3.2; nếu nghiệp vụ thực sự cần, phải thêm state `Cancelled` từ `Approved` thay vì sửa bảng transition tuỳ tiện.
- Số tiền hoàn vượt quá số tiền đã thanh toán gốc (do bug tính toán) → validate `RefundTransaction.Amount <= OriginalPaymentTransaction.Amount - tổng đã refund trước đó của cùng order` trước khi gọi VNPay API, chặn từ application layer chứ không chỉ tin tưởng domain logic.

---

## 12. Acceptance Criteria

1. Tạo return request cho đơn `Delivered` trong window 7 ngày, với item hợp lệ → status `Requested`, đúng `CalculateRefundAmount()`.
2. Tạo return request cho đơn đã quá 7 ngày → bị từ chối ở `IReturnEligibilityService`, không tạo record.
3. Approve → Reject trên cùng request → phải throw exception vì không nằm trong bảng transition hợp lệ (test trực tiếp domain method, không qua API).
4. Gửi IPN callback trùng 2 lần cho cùng `RefundTransaction` → chỉ có 1 lần `Refunded`, không trigger Loyalty adjustment 2 lần, không restock kho 2 lần.
5. Inspect với `ItemCondition = Damaged` → không có thay đổi inventory, có log riêng ghi nhận tổn thất.
6. Refund thành công → `CustomerLoyaltyAccount` có đúng 1 `LoyaltyTransaction Type=Adjust` với số điểm âm tương ứng.
7. Reconciliation job chạy với 1 `RefundTransaction` đang `Pending` quá hạn → gọi query API, cập nhật đúng trạng thái thực tế từ VNPay.

---

## 13. Non-goals / Future Extensions

- Không cần dashboard thống kê tỷ lệ trả hàng theo sản phẩm (nice-to-have cho version sau).
- Không cần workflow đa cấp duyệt (chỉ 1 admin duyệt, không cần escalation).
- Không cần SLA tracking (đo thời gian xử lý từng bước) ở version này.
- Không tự động hoá quyết định approve/reject bằng rule engine — giữ nguyên tắc con người duyệt thủ công như đã nêu ở mục 1.
