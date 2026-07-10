# Feature Spec: Customer Loyalty Program

> Doi tuong doc: coding agent trien khai tren du an WorkspaceEcommerce.
> Muc tieu tai lieu: khoa cac quyet dinh nghiep vu cot loi truoc khi implement loyalty module.

---

## 1. Business Context

Tinh nang Khach hang than thiet khuyen khich customer da dang nhap quay lai mua hang bang cach:

- Tich diem tu don hang da hoan tat.
- Tu dong nang hang thanh vien dua tren tong diem da tich luy.
- Cho customer doi diem lay voucher giam gia.
- Cho customer xem so du diem, hang hien tai va lich su giao dich diem.

Mo hinh v1: point-based ket hop tier-based, khong lam missions, badges, referral hoac point expiration.

---

## 2. Scope

### In scope

- Tich diem tu dong khi order co `CustomerId` chuyen sang `Completed`.
- Tu dong tao loyalty account khi customer co earn event dau tien.
- Tu dong danh gia va nang hang sau moi lan earn.
- Doi diem lay voucher giam gia customer-scoped.
- Xem so du diem, tong diem da earn, hang hien tai, quyen loi hang va lich su giao dich diem.
- Audit trail cho moi thay doi diem.
- Idempotency cho earn theo order.
- Concurrency control cho redeem.

### Out of scope in v1

- Guest order earn points.
- Point expiration.
- Referral program.
- Birthday reward.
- Missions/badges.
- Doi diem lay san pham vat ly.
- Admin UI quan ly tiers.
- Tu dong ap dung tier discount/free shipping vao checkout.
- Tu dong tru diem khi order bi return/refund sau khi da earn.

---

## 3. MVP Decisions

- Guest order khong earn points trong v1. Order phai co `CustomerId` moi duoc tinh diem.
- Formula earn points:

```text
points = floor(((order.Subtotal - order.DiscountAmount) * order.ExchangeRate) / Loyalty:MoneyPerPoint)
```

- `order.Subtotal - order.DiscountAmount` la gia tri hang hoa da thanh toan sau coupon.
- `order.ShippingFee` khong tinh diem.
- `order.ExchangeRate` dung de normalize ve VND-equivalent khi order co currency khac VND.
- Default `Loyalty:MoneyPerPoint = 10000`.
- Neu formula ra `0`, khong tao earn transaction.
- `CurrentPoints` la so du kha dung.
- `TotalPointsEarned` chi tang khi earn, khong giam khi redeem.
- Tier benefits trong v1 chi dung de hien thi. `DiscountPercent` va `FreeShippingEnabled` chua anh huong gia checkout.
- Redeem diem tao voucher tren coupon system hien co trong schema `promotions`.
- Loyalty voucher la customer-scoped fixed-amount coupon, gioi han 1 lan dung, het han sau 30 ngay.
- Default redeem conversion:

```text
discountAmount = points * Loyalty:VoucherAmountPerPoint
Loyalty:VoucherAmountPerPoint = 1000
```

- `AdminOrderService` se trigger loyalty earn khi status update thanh `Completed`. V1 khong them domain event/outbox full.

---

## 4. Domain Model

### 4.1 Entities

```csharp
public sealed class CustomerLoyaltyAccount
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public int CurrentPoints { get; private set; }
    public int TotalPointsEarned { get; private set; }
    public LoyaltyTierType CurrentTier { get; private set; }
    public DateTimeOffset TierUpdatedAt { get; private set; }

    // Configure optimistic concurrency in EF, preferably PostgreSQL xmin.
    public uint Version { get; private set; }

    private readonly List<LoyaltyTransaction> _transactions = [];
    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions;

    public LoyaltyTransaction EarnPoints(int points, Guid orderId, string description);
    public LoyaltyTransaction RedeemPoints(int points, Guid voucherId, string description);
    public bool TryEvaluateTierUpgrade(IEnumerable<LoyaltyTier> tierDefinitions);
}

public sealed class LoyaltyTransaction
{
    public Guid Id { get; private set; }
    public Guid CustomerLoyaltyAccountId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? VoucherId { get; private set; }
    public LoyaltyTransactionType Type { get; private set; }
    public int Points { get; private set; }
    public int BalanceAfter { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class LoyaltyTier
{
    public Guid Id { get; private set; }
    public LoyaltyTierType Type { get; private set; }
    public int MinTotalPointsEarned { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public bool FreeShippingEnabled { get; private set; }
}

public enum LoyaltyTierType
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    Platinum = 3
}

public enum LoyaltyTransactionType
{
    Earn = 0,
    Redeem = 1,
    Adjust = 2
}
```

### 4.2 Domain rules

- `EarnPoints` requires `points > 0` and non-empty `orderId`.
- `RedeemPoints` requires `points > 0`, non-empty `voucherId`, and enough `CurrentPoints`.
- Earn transaction stores positive `Points`.
- Redeem transaction stores negative `Points`.
- `BalanceAfter` is a snapshot after the transaction.
- Tier can only move up in v1. No automatic downgrade.

---

## 5. Database Schema

Tables live in schema `loyalty`.

```sql
CREATE TABLE loyalty.customer_loyalty_accounts (
    id                  UUID PRIMARY KEY,
    customer_id         UUID NOT NULL UNIQUE REFERENCES customers.customers(id),
    current_points      INT NOT NULL DEFAULT 0 CHECK (current_points >= 0),
    total_points_earned INT NOT NULL DEFAULT 0 CHECK (total_points_earned >= 0),
    current_tier        TEXT NOT NULL DEFAULT 'Bronze',
    tier_updated_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE loyalty.loyalty_transactions (
    id                            UUID PRIMARY KEY,
    customer_loyalty_account_id   UUID NOT NULL REFERENCES loyalty.customer_loyalty_accounts(id),
    order_id                      UUID NULL REFERENCES ordering.orders(id),
    voucher_id                    UUID NULL REFERENCES promotions.coupons(id),
    type                          TEXT NOT NULL,
    points                        INT NOT NULL,
    balance_after                 INT NOT NULL CHECK (balance_after >= 0),
    description                   TEXT NOT NULL,
    created_at                    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_loyalty_transactions_earn_order
    ON loyalty.loyalty_transactions (order_id)
    WHERE type = 'Earn' AND order_id IS NOT NULL;

CREATE TABLE loyalty.loyalty_tiers (
    id                       UUID PRIMARY KEY,
    type                     TEXT NOT NULL UNIQUE,
    min_total_points_earned  INT NOT NULL CHECK (min_total_points_earned >= 0),
    discount_percent         NUMERIC(5,2) NOT NULL DEFAULT 0,
    free_shipping_enabled    BOOLEAN NOT NULL DEFAULT false
);
```

Important: do not use `UNIQUE NULLS NOT DISTINCT (order_id, type)` for loyalty transactions. Redeem transactions have null `order_id`, so that design can accidentally block multiple redeem rows. Idempotency must be enforced with the partial unique index above, only for Earn transactions.

Default tiers:

| Tier | MinTotalPointsEarned | DiscountPercent | FreeShippingEnabled |
| --- | ---: | ---: | --- |
| Bronze | 0 | 0 | false |
| Silver | 500 | 3 | false |
| Gold | 2000 | 5 | true |
| Platinum | 5000 | 10 | true |

`DiscountPercent` and `FreeShippingEnabled` are display-only in v1.

---

## 6. Coupon/Voucher Alignment

Loyalty voucher is implemented by extending the existing `Coupon` model instead of creating a separate voucher table.

Required coupon extensions:

- `CustomerId` nullable: if set, only that authenticated customer can use the coupon.
- `Source` enum/string: `Admin` or `Loyalty`.
- Optional `CreatedByLoyaltyTransactionId` or equivalent audit link if implementation can support it cleanly.

Loyalty voucher rules:

- `DiscountType = FixedAmount`.
- `DiscountValue = points * Loyalty:VoucherAmountPerPoint`.
- `UsageLimit = 1`.
- `StartsAt = now`.
- `EndsAt = now + 30 days`.
- `IsActive = true`.
- Code format should be generated, uppercase and unique, for example `LOYAL-{shortId}`.
- Checkout coupon validation must reject customer-scoped coupons when current customer does not match `Coupon.CustomerId`.
- Guest checkout cannot use customer-scoped loyalty vouchers.

---

## 7. Business Rules

| Rule | Description |
| --- | --- |
| BR-1 | Earn only when an order with `CustomerId` changes to `Completed`. |
| BR-2 | Guest orders do not earn points in v1. |
| BR-3 | Earn formula uses merchandise paid amount after discount, normalized by `ExchangeRate`; shipping is excluded. |
| BR-4 | One order can earn at most once, enforced by DB partial unique index. |
| BR-5 | `CurrentPoints` cannot go below zero. |
| BR-6 | `TotalPointsEarned` only increases. |
| BR-7 | Tier is evaluated immediately after earn and only upgrades in v1. |
| BR-8 | Tier benefits are display-only in v1. |
| BR-9 | Redeem points creates a customer-scoped coupon and writes a Redeem transaction. |
| BR-10 | Duplicate earn processing should be treated as success/no-op. |

---

## 8. Concurrency & Idempotency

### Earn idempotency

- Before earning, application may check if an Earn transaction already exists for the order.
- The DB partial unique index is the source of truth.
- If insert conflicts because the order already earned, catch the conflict and return success/no-op.

### Redeem concurrency

- Redeem must run inside a transaction.
- The account row must be protected with optimistic concurrency (`xmin`/concurrency token) or a row lock.
- If two redeem requests race, only one can reduce the balance when points are insufficient for both.
- On concurrency conflict, return a conflict result with a clear message.

---

## 9. Order Integration

V1 uses direct application-service integration rather than full domain events:

- `AdminOrderService.UpdateStatusAsync` saves the order status/history first.
- If the target status is `Completed`, it calls `ILoyaltyService.EarnForCompletedOrderAsync(order.Id)`.
- `EarnForCompletedOrderAsync` loads the saved order and applies all loyalty rules.
- Duplicate earn is a success/no-op.
- Guest orders are success/no-op.
- Loyalty earn should not create a second status history entry.

Future versions can replace this with domain events/outbox if the project adopts a consistent event pattern.

---

## 10. API Endpoints

```http
GET  /api/loyalty/me
GET  /api/loyalty/me/transactions?page=1&pageSize=20
POST /api/loyalty/me/redeem
GET  /api/loyalty/tiers
```

### GET /api/loyalty/me

Requires customer authentication.

Response shape:

```json
{
  "currentPoints": 120,
  "totalPointsEarned": 620,
  "currentTier": "Silver",
  "tierBenefits": {
    "discountPercent": 3,
    "freeShippingEnabled": false
  },
  "nextTier": "Gold",
  "pointsToNextTier": 1380
}
```

### GET /api/loyalty/me/transactions

Requires customer authentication. Sorted by `createdAt desc`.

### POST /api/loyalty/me/redeem

Requires customer authentication.

Request:

```json
{
  "points": 100
}
```

Response:

```json
{
  "voucherId": "00000000-0000-0000-0000-000000000000",
  "voucherCode": "LOYAL-ABC123",
  "discountAmount": 100000,
  "remainingPoints": 20
}
```

### GET /api/loyalty/tiers

Public endpoint for displaying membership benefits.

All `/api/loyalty/me/*` endpoints must use `CustomerId` from the authenticated token. Never accept `CustomerId` from body/query.

---

## 11. Edge Cases

- Order has no `CustomerId`: no-op, no points.
- Formula returns zero points: no-op, no transaction.
- Duplicate completed processing: no-op, no second earn.
- Customer has no account: create account during first earn or first authenticated loyalty read/redeem if needed.
- Redeem more than balance: validation error, no transaction, no voucher.
- Order later moves to `Returned`: no automatic point reversal in v1.
- Customer-scoped loyalty coupon used by another customer or guest: validation error.

---

## 12. Acceptance Criteria

1. Customer order with paid merchandise amount 150000 VND changes to `Completed`, customer receives 15 points and one Earn transaction.
2. Guest order changes to `Completed`, no loyalty account or transaction is created.
3. Reprocessing the same completed order does not add points twice and does not fail.
4. Customer with `TotalPointsEarned = 600` is upgraded to `Silver`.
5. Redeem 100 points with only 50 current points returns validation error and creates no coupon/transaction.
6. Two concurrent redeem requests cannot make `CurrentPoints` negative.
7. Redeem 100 points creates a customer-scoped fixed coupon with value `100 * Loyalty:VoucherAmountPerPoint`.
8. Another customer cannot use the generated loyalty coupon.
9. Tier benefits are returned by API but do not change checkout totals in v1.

---

## 13. Future Extensions

- Point expiration.
- Admin point adjustment UI.
- Return/refund point reversal.
- Auto-apply tier discount/free shipping at checkout.
- Referral program.
- Birthday benefits.
- Tier management UI.
- Domain event/outbox integration.
