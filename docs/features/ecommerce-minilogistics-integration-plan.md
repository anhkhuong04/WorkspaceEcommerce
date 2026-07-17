# E-commerce <-> MiniLogistics Shipping Integration Plan

Tai lieu nay danh gia luong lien ket van chuyen hien co trong WorkspaceEcommerce va cac viec can lam de trien khai end-to-end voi MiniLogistics.

Pham vi da kiem tra trong repo hien tai:

- E-commerce backend: `.NET WebAPI + PostgreSQL`.
- Frontend Storefront/Admin co type va UI co ban cho `trackingCode`/`shipmentId`.
- Partner API contract trong `docs/partner`.

Pham vi chua kiem tra duoc tu repo nay:

- Source code thuc te cua MiniLogistics API/Blazor UI/SQL Server.
- Cau hinh shop, API key, webhook registration va status lifecycle trong MiniLogistics runtime.

## 1. Readiness Summary

Ket luan ngan: da co nen tang de demo local, nhung chua nen coi la production-ready.

Co the demo ngay neu:

- MiniLogistics Partner API dang chay o `MiniLogistics__BaseUrl`.
- API key trong `MiniLogistics__ApiKey` hop le.
- MiniLogistics da cau hinh webhook ve `POST /api/webhooks/minilogistics`.
- `MiniLogistics__WebhookSecret` o E-commerce trung voi secret dung de ky webhook.
- Network giua hai app thong nhau. Neu E-commerce API chay Docker va MiniLogistics chay tren host, dung `http://host.docker.internal:{port}/api/v1/partner`.

Chua hoan chinh cho portfolio demo neu muon the hien luong Shopee/GHN/SPX day du:

- Chua co cancel shipment khi khach/admin huy don.
- Chua co retry/outbox cho tao shipment that bai.
- Chua co endpoint xem tracking timeline live tu MiniLogistics.
- Chua co contract/e2e test goi HTTP that toi MiniLogistics.
- Webhook chua co replay protection bang `EventId`/timestamp window.
- Webhook cap nhat `Completed` chua kich hoat loyalty points.
- Chua luu lich su shipment rieng; hien chi luu `OrderStatusHistory`.

## 2. Current Implementation

### 2.1 Partner API client

E-commerce da co abstraction `IShipmentService` voi 3 operation:

- `GetShippingQuoteAsync`
- `CreateShipmentAsync`
- `GetTrackingAsync`

Implementation hien tai la `MiniLogisticsClient`.

HTTP contract dang duoc goi:

- `POST shipping/quote`
- `POST shipments`
- `GET shipments/{trackingCode}`

Headers:

- `Authorization: Bearer {MiniLogistics__ApiKey}`
- `Idempotency-Key: {order.OrderCode}` khi tao shipment

Payload da map cac field chinh:

- `externalOrderId`
- `receiver`
- `deliveryAddress`
- `parcel`
- `goodsValueAmount`
- `codAmount`
- `currency`
- `note`

Ghi chu:

- `sender` va `pickupAddress` dang gui `null`, ky vong MiniLogistics dung shop default.
- `GetTrackingAsync` da implement nhung chua duoc dung boi API endpoint nao.
- Chua co cancel operation trong `IShipmentService`, mac du Partner API docs co `POST /shipments/{trackingCode}/cancel`.

### 2.2 Checkout and payment orchestration

COD va ManualBankTransfer:

1. Checkout tao `Order`.
2. Giam stock.
3. Luu order va remove cart.
4. Sau transaction, E-commerce goi shipping quote va create shipment.
5. Neu tao shipment thanh cong, luu `ShipmentId` va `TrackingCode`.
6. Neu tao shipment loi HTTP, order van duoc dat thanh cong, shipment de xu ly thu cong.

VNPay:

1. Truoc khi tao payment URL, E-commerce goi shipping quote de tinh shipping fee.
2. Checkout tao `Order` va `PaymentTransaction` o trang thai pending.
3. Chua tao shipment tai checkout.
4. Khi VNPay return/IPN success, mark payment paid.
5. Sau khi payment paid, goi create shipment mot lan.
6. Callback lap lai khong tao duplicate shipment neu transaction da terminal.
7. Neu shipment creation fail sau payment success, payment van giu paid va order khong co `ShipmentId`.

Dieu nay dung voi spec hien tai: shipment chi duoc tao sau khi VNPay thanh cong.

### 2.3 Stored order data

`Order` da co:

- `ShippingStreet`
- `ShippingWard`
- `ShippingProvince`
- `ShippingFee`
- `ShipmentId`
- `TrackingCode`
- `PaymentStatus`
- `OrderStatusHistory`

Admin order detail, customer order detail va guest order lookup deu tra ve `trackingCode`/`shipmentId`.

### 2.4 Webhook receiver

Endpoint hien co:

```http
POST /api/webhooks/minilogistics
```

Security hien co:

- Yeu cau `X-MiniLogistics-Signature`.
- Yeu cau `X-MiniLogistics-Timestamp`.
- Verify HMAC-SHA256 theo input `timestamp + "." + raw_body`.

Supported event:

- `webhook.test`
- `shipment.status_changed`

Status mapping hien tai:

| MiniLogistics status | E-commerce status |
| --- | --- |
| `PendingPickup` | `Confirmed` |
| `InTransit` | `Shipping` |
| `OutForDelivery` | `Shipping` |
| `Delivered` | `Completed` |
| `FailedDelivery` | `FailedDelivery` |
| `Returned` | `FailedDelivery` |
| `Cancelled` | `Cancelled` |

Webhook co logic step-by-step de di tu `Pending -> Confirmed -> Processing -> Shipping -> Completed` khi logistics gui status xa hon order status hien tai.

## 3. Gaps and Risks

### P0 - can fix truoc khi demo end-to-end nghiem tuc

1. Chua co e2e test voi MiniLogistics that
   - Test hien tai override `IShipmentService` bang fake.
   - Can co test/smoke script goi Partner API that: quote -> create shipment -> webhook -> order status update.

2. Webhook `Completed` khong kich hoat loyalty
   - Admin update status sang `Completed` co goi `ILoyaltyService.EarnForCompletedOrderAsync`.
   - Webhook update sang `Completed` hien chi change status va save.
   - Ket qua: order giao thanh cong boi MiniLogistics co the khong cong diem loyalty.

3. Chua co retry/manual retry cho shipment creation failed
   - COD/Manual/VNPay success neu MiniLogistics down thi order van ton tai nhung khong co shipment.
   - Hien khong co admin endpoint `create/retry shipment`.
   - Portfolio demo nen co nut "Create shipment" hoac background retry de the hien kha nang recover.

4. Chua dong bo cancel tu E-commerce sang MiniLogistics
   - Customer cancel order chi update order va restore stock.
   - Admin cancel order chi update order status.
   - Neu shipment da tao, MiniLogistics khong nhan lenh cancel.

5. Chua validate webhook replay/timestamp
   - Signature dung, nhung chua reject timestamp qua cu/qua xa.
   - Chua luu `EventId` da xu ly de tranh replay duplicate.

### P1 - nen lam de luong portfolio ro rang

1. Them API lay tracking live
   - `IShipmentService.GetTrackingAsync` da co nhung chua expose.
   - Nen them endpoint customer/admin: `GET /api/orders/{orderCode}/tracking` hoac include timeline trong order detail.

2. Luu shipment state rieng trong E-commerce
   - Hien order chi co `ShipmentId`/`TrackingCode` va order status.
   - Nen them bang `OrderShipments` hoac `ShipmentEvents` de luu:
     - provider shipment id
     - tracking code
     - provider status
     - last event id
     - last synced at
     - raw status/note

3. Kiem tra tracking code trong webhook
   - Hien lookup order theo `ExternalOrderId`.
   - Nen verify `payload.TrackingCode == order.TrackingCode` khi order da co tracking code.

4. Xu ly webhook unknown order
   - Hien tra `404`, co the lam MiniLogistics retry mai.
   - Nen can quyet dinh:
     - `404` neu muon retry khi eventual consistency.
     - `200` + log/dead-letter neu order chac chan khong ton tai.

5. Them config validation
   - `MiniLogistics__BaseUrl` empty co the crash khi DI tao `Uri`.
   - Nen validate options startup va fail fast voi message ro.

6. UI Admin/Storefront
   - Admin list chua hien tracking code.
   - Customer/order lookup chi thay tracking code, chua co timeline.
   - Nen them "Shipment" panel: tracking code, provider status, timeline, retry/cancel action.

### P2 - hardening sau demo

1. Outbox/inbox pattern
   - Outbox cho create shipment, cancel shipment.
   - Inbox cho webhook event idempotency.

2. Timeout/retry/circuit breaker
   - Hien `HttpClient.Timeout = 30s`.
   - Nen them Polly retry ngan cho transient errors va circuit breaker neu MiniLogistics down.

3. Observability
   - Log da co nhung can correlation id:
     - `OrderCode`
     - `TrackingCode`
     - `ShipmentId`
     - `EventId`
     - `Idempotency-Key`

4. Contract versioning
   - Chua co `X-API-Version` hoac versioned DTO changelog giua hai service.

## 4. Target Flow

### 4.1 Quote

Storefront:

```text
Checkout page
-> E-commerce POST /api/checkout/shipping-quote
-> MiniLogistics POST /api/v1/partner/shipping/quote
-> E-commerce returns fee breakdown to Storefront
```

Acceptance:

- Cart empty tra validation.
- Product inactive/out of stock tra validation/conflict.
- MiniLogistics down tra message "Could not calculate shipping fee".
- Fee duoc dung trong checkout total.

### 4.2 Create shipment for COD/Manual

```text
Storefront POST /api/checkout
-> E-commerce creates order
-> E-commerce calls MiniLogistics Create Shipment
-> MiniLogistics returns shipmentId/trackingCode/status
-> E-commerce saves ShipmentId/TrackingCode
-> Storefront/Admin can display tracking code
```

Decision can confirm:

- ManualBankTransfer hien dang tao shipment ngay khi checkout, du payment status la `Pending`.
- Neu business muon "chi tao shipment sau khi admin xac nhan da nhan tien", can doi flow: ManualBankTransfer khong tao shipment tai checkout, tao khi admin mark payment/order confirmed.

### 4.3 Create shipment for VNPay

```text
Storefront POST /api/checkout
-> E-commerce creates order + payment transaction
-> Customer pays in VNPay
-> VNPay return/IPN success
-> E-commerce marks payment paid
-> E-commerce calls MiniLogistics Create Shipment
-> E-commerce saves ShipmentId/TrackingCode
```

Acceptance:

- Success callback tao shipment mot lan.
- Duplicate callback khong tao shipment trung.
- Failed/cancelled payment khong tao shipment.
- Shipment fail khong rollback payment paid.

### 4.4 Status webhook

```text
MiniLogistics operator/shipper changes shipment status
-> MiniLogistics POST /api/webhooks/minilogistics
-> E-commerce verifies signature
-> E-commerce maps logistics status to order status
-> Admin/customer see updated order status
```

Acceptance:

- Invalid signature -> `401`.
- Missing security headers -> `400`.
- Unsupported event/status -> `200` + log.
- Delivered -> order `Completed` and loyalty points awarded.
- Duplicate event -> no duplicate status history.

### 4.5 Cancel shipment

Target flow chua implement:

```text
Customer/Admin cancels order
-> E-commerce validates order can be cancelled
-> If TrackingCode exists, E-commerce calls MiniLogistics cancel API
-> E-commerce updates order Cancelled
-> MiniLogistics sends/can send cancellation webhook
```

Can chon mot trong hai policy:

- Strict: cancel shipment thanh cong truoc, sau do cancel order.
- Lenient: cancel order truoc, enqueue cancel shipment retry.

Cho portfolio, lenient + outbox/retry se the hien system design tot hon.

## 5. Implementation Checklist

### Step 1 - Confirm MiniLogistics contract

- [ ] Xac nhan base URL thuc te: `http://localhost:5221/api/v1/partner`.
- [ ] Xac nhan auth header: `Authorization: Bearer {api_key}`.
- [ ] Xac nhan DTO field casing camelCase.
- [ ] Xac nhan status enum MiniLogistics dung dung cac value dang map.
- [ ] Xac nhan create shipment response co `shipmentId`, `trackingCode`, `status`, `shippingFeeAmount`.
- [ ] Xac nhan webhook signature input la `timestamp + "." + raw_payload_json`.
- [ ] Xac nhan webhook payload co `eventId`, `event`, `trackingCode`, `externalOrderId`, `status`, `changedAtUtc`.

### Step 2 - Add missing app config

- [ ] Them vao `.env.example`:
  - `MiniLogistics__BaseUrl`
  - `MiniLogistics__ApiKey`
  - `MiniLogistics__WebhookSecret`
- [ ] Them startup validation cho `MiniLogisticsOptions`.
- [ ] Document Docker networking:
  - E-commerce Docker -> MiniLogistics host: `http://host.docker.internal:5221/api/v1/partner`.
  - E-commerce host -> MiniLogistics host: `http://localhost:5221/api/v1/partner`.

### Step 3 - Harden webhook

- [ ] Reject timestamp older/newer than allowed window, e.g. 5 minutes.
- [ ] Add inbox table:
  - `EventId`
  - `Event`
  - `TrackingCode`
  - `ExternalOrderId`
  - `ReceivedAtUtc`
  - `ProcessedAtUtc`
- [ ] Skip duplicate `EventId`.
- [ ] Validate tracking code matches order when order has `TrackingCode`.
- [ ] Award loyalty when webhook transitions order to `Completed`.
- [ ] Add tests for valid/invalid signature, duplicate event, delivered status.

### Step 4 - Shipment retry/manual action

- [ ] Add endpoint:

```http
POST /api/admin/orders/{id}/shipment/retry
```

- [ ] Only allowed when order has no `ShipmentId`.
- [ ] Rebuild shipment request from order/items.
- [ ] Use idempotency key `order.OrderCode`.
- [ ] Save `ShipmentId`/`TrackingCode`.
- [ ] Show action in Admin order detail.

Optional stronger version:

- [ ] Add outbox table and background worker for create shipment command.

### Step 5 - Cancel shipment

- [ ] Extend `IShipmentService`:

```csharp
Task<CancelShipmentResponse> CancelShipmentAsync(
    string trackingCode,
    string reason,
    CancellationToken cancellationToken = default);
```

- [ ] Implement `MiniLogisticsClient` call:

```http
POST /shipments/{trackingCode}/cancel
```

- [ ] Call it from customer/admin cancel flow when tracking code exists.
- [ ] Decide strict vs lenient cancellation policy.
- [ ] Add tests for cancellation success/failure.

### Step 6 - Tracking UI/API

- [ ] Add application service method to fetch tracking by `TrackingCode`.
- [ ] Add customer-safe endpoint requiring order ownership or phone lookup.
- [ ] Add admin endpoint for order detail tracking.
- [ ] Storefront order lookup displays:
  - tracking code
  - provider status
  - timeline
- [ ] Admin order detail displays:
  - shipment id
  - tracking code
  - provider status
  - timeline
  - retry/cancel actions

### Step 7 - E2E smoke script

Create one repeatable script that runs after both services are up:

1. Call E-commerce `POST /api/checkout/shipping-quote`.
2. Call E-commerce `POST /api/checkout` with COD.
3. Assert order response has `trackingCode`.
4. Query MiniLogistics tracking endpoint using returned tracking code.
5. Simulate MiniLogistics status change to `Delivered`.
6. Assert E-commerce order lookup shows `Completed`.

## 6. Local Runbook

### E-commerce

```powershell
docker compose up -d postgres
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm seed-demo
docker compose up -d --build api
```

Frontend:

```powershell
cd frontend
corepack pnpm dev:storefront
corepack pnpm dev:admin
```

### MiniLogistics assumptions

Run MiniLogistics API on host:

```text
http://localhost:5221
```

Set E-commerce `.env` when E-commerce API runs in Docker:

```env
MiniLogistics__BaseUrl=http://host.docker.internal:5221/api/v1/partner
MiniLogistics__ApiKey=<partner-api-key>
MiniLogistics__WebhookSecret=<same-secret-configured-in-minilogistics>
```

Webhook URL registered in MiniLogistics:

```text
http://host.docker.internal:5080/api/webhooks/minilogistics
```

If MiniLogistics runs in Docker too, use a shared Docker network and service DNS name instead of `host.docker.internal`.

## 7. Recommended Portfolio Demo Script

1. Show Storefront checkout calculating shipping fee from MiniLogistics.
2. Place COD order and show returned tracking code.
3. Open Admin order detail in E-commerce and show shipment fields.
4. Open MiniLogistics Blazor UI and show the created shipment.
5. In MiniLogistics, move shipment through:
   - `PendingPickup`
   - `InTransit`
   - `OutForDelivery`
   - `Delivered`
6. Refresh E-commerce customer/admin order page and show status changed to `Completed`.
7. Show webhook logs and order status history.
8. Cancel another order and show E-commerce -> MiniLogistics cancellation after Step 5 is implemented.

## 8. Go/No-Go

Go for local technical demo:

- Yes, after MiniLogistics runtime/API key/webhook secret are configured and manually smoke-tested.

No-go for polished portfolio demo until at least these are done:

- Webhook hardening.
- Manual retry shipment.
- Cancel shipment sync.
- Tracking timeline endpoint/UI.
- One e2e smoke test against real MiniLogistics.

No-go for production-style claim until these are done:

- Outbox/inbox idempotency.
- Retry/circuit breaker.
- Event replay protection.
- Contract tests/versioning.
- Operational dashboards/log correlation.
