# Test Case Document - Workspace E-Commerce

> **Portfolio sample | Synthetic execution data | Local/sandbox only**

## 1. Test cycle information

| Field | Value |
|---|---|
| Test cycle | WEC-MANUAL-CYCLE-01 |
| Build label | `portfolio-candidate-1` (giả lập) |
| Test level | System / API / lightweight UI validation |
| Base URL | `http://localhost:5080` |
| Database | PostgreSQL local container |
| Tools | Postman, Swagger/OpenAPI, pgAdmin/psql, browser DevTools |
| Tester | Ly Dinh Anh Khuong |
| Execution dates | 2026-07-27 to 2026-07-29 (giả lập) |

## 2. Scope and approach

### In scope

- Customer register/login and JWT authorization.
- Product catalog and product detail.
- Guest/customer cart and stock boundaries.
- Coupon, shipping quote, COD/VNPay demo checkout.
- Guest order lookup and shipment tracking.
- MiniLogistics webhook validation and idempotency.
- Admin order authorization and basic UI state.

### Out of scope

- Real bank settlement, real logistics delivery and production load/security penetration testing.
- Email/SMS delivery, cross-browser matrix and mobile device laboratory.
- Validation of third-party systems beyond their sandbox contract.

### Techniques

- Equivalence Partitioning (valid/invalid/missing input groups).
- Boundary Value Analysis (quantity 0, 1, stock, stock + 1; coupon expiry boundary).
- State Transition (order/shipment status).
- Decision Table thinking (payment method x shipment creation x provider availability).
- Authorization matrix (anonymous/customer/admin).

## 3. Synthetic test data

| Data ID | Value | Purpose |
|---|---|---|
| USER-01 | `khuong.qa+customer@example.com` | Valid customer account |
| USER-02 | `khuong.qa+duplicate@example.com` | Duplicate registration scenario |
| PHONE-01 | `0900000001` | Guest lookup verification |
| SESSION-01 | `qa-cart-session-001` | Anonymous cart |
| PRODUCT-01 | `ergonomic-chair-demo` | Active product, variant stock = 5 |
| PRODUCT-02 | `archived-desk-demo` | Inactive/unavailable product |
| COUPON-01 | `QA10ACTIVE` | Active 10% coupon |
| COUPON-02 | `QA10EXPIRED` | Expired coupon |
| ORDER-01 | `ORD-QA-0001` | Synthetic COD order |
| TRACK-01 | `ML-QA-0001` | Synthetic tracking code |

Passwords, JWTs, API keys and webhook signatures must be injected at runtime and must not be committed.

## 4. Test cases

### A. Authentication and authorization

| ID | Scenario / technique | Preconditions | Steps / request | Expected result | Actual result (synthetic) | Status | Defect |
|---|---|---|---|---|---|---|---|
| AUTH-001 | Register with valid data - Positive | Email not registered | `POST /api/customer/auth/register` with valid name, email, phone, password | `201`; customer created; token returned; password not exposed | Matched expected | Pass | - |
| AUTH-002 | Register duplicate email - EP negative | USER-02 exists | Register again with same normalized email | `409`; clear business error; no duplicate DB row | Matched expected | Pass | - |
| AUTH-003 | Invalid email format - EP negative | None | Register with `khuong@` | `400`; field validation; no customer created | Matched expected | Pass | - |
| AUTH-004 | Login with wrong password - Negative | USER-01 exists | `POST /api/customer/auth/login` with wrong password | `401`; generic message; no token | Matched expected | Pass | - |
| AUTH-005 | Customer token calls admin API - Authorization matrix | Valid customer JWT | `GET /api/admin/orders` with customer token | `403`; no order data returned | Matched expected | Pass | - |
| AUTH-006 | Registration trims email whitespace - Data normalization | Email not registered | Register with `" khuong.qa+trim@example.com "`, then login with trimmed value | Input normalized consistently; login succeeds | Registration succeeds but later login cannot find trimmed email | Fail | BUG-001 |

### B. Catalog

| ID | Scenario / technique | Preconditions | Steps / request | Expected result | Actual result (synthetic) | Status | Defect |
|---|---|---|---|---|---|---|---|
| CAT-001 | List active products - Positive | Seed data loaded | `GET /api/products` | `200`; paged list; only storefront-visible products | Matched expected | Pass | - |
| CAT-002 | Filter/search product | Seed data loaded | `GET /api/products?search=chair` | `200`; relevant products; stable paging metadata | Matched expected | Pass | - |
| CAT-003 | Product detail by valid slug | PRODUCT-01 exists | `GET /api/products/ergonomic-chair-demo` | `200`; variants, price and stock fields are consistent | Matched expected | Pass | - |
| CAT-004 | Unknown slug - Negative | None | `GET /api/products/not-found-qa` | `404`; controlled API response | Matched expected | Pass | - |
| CAT-005 | Archived product hidden | PRODUCT-02 inactive | Query list and direct detail | Not visible in list; detail is `404` or unavailable per rule | Matched expected | Pass | - |

### C. Cart

| ID | Scenario / technique | Preconditions | Steps / request | Expected result | Actual result (synthetic) | Status | Defect |
|---|---|---|---|---|---|---|---|
| CART-001 | Add one item - Boundary min valid | SESSION-01; PRODUCT-01 stock 5 | `POST /api/cart/items`, quantity = 1 | `200`; item added; subtotal = unit price | Matched expected | Pass | - |
| CART-002 | Add zero quantity - Boundary invalid | Valid variant | Add quantity = 0 | `400`; no cart mutation | Matched expected | Pass | - |
| CART-003 | Add negative quantity - EP invalid | Valid variant | Add quantity = -1 | `400`; no cart mutation | Matched expected | Pass | - |
| CART-004 | Add exactly available stock - Boundary max valid | Stock = 5 | Add quantity = 5 | `200`; quantity = 5 | Matched expected | Pass | - |
| CART-005 | Add unavailable variant | PRODUCT-02 inactive | Add archived variant | `404` or `409`; cart unchanged | Matched expected | Pass | - |
| CART-006 | Add stock + 1 - Boundary invalid | PRODUCT-01 stock = 5 | Add/update quantity = 6 | `409`; quantity remains previous value | API accepts quantity 6 and returns updated cart | Fail | BUG-002 |
| CART-007 | Recalculate totals after update | Cart has quantity 1 | Update quantity to 2, then `GET /api/cart` | Line subtotal and cart total both reflect quantity 2 | Quantity changes but cart total retains previous value until reload | Fail | BUG-003 |

### D. Checkout, coupon and payment

| ID | Scenario / technique | Preconditions | Steps / request | Expected result | Actual result (synthetic) | Status | Defect |
|---|---|---|---|---|---|---|---|
| CHK-001 | Validate active coupon - Positive | Eligible cart; COUPON-01 | `POST /api/checkout/coupons/validate` | `200`; discount and final total calculated correctly | Matched expected | Pass | - |
| CHK-002 | Unknown coupon - Negative | Eligible cart | Validate `DOESNOTEXIST` | `404`; no discount | Matched expected | Pass | - |
| CHK-003 | Shipping quote valid address | Cart ready | `POST /api/checkout/shipping-quote` with supported province | `200`; positive fee, currency VND | Matched expected | Pass | - |
| CHK-004 | Missing required address | Cart ready | Request quote with blank province/address | `400`; field errors; no provider call when validation fails | Matched expected | Pass | - |
| CHK-005 | COD checkout happy path | Valid cart/address/phone | `POST /api/checkout`, payment = COD | `201`; one order; cart cleared; shipment created/enqueued | Matched expected | Pass | - |
| CHK-006 | Rapid duplicate checkout - Idempotency | Same cart/request | Send identical checkout twice within 500 ms | One order only; duplicate request rejected or same result returned | Two distinct order codes are created | Fail | BUG-004 |
| CHK-007 | Coupon expiry exact boundary - BVA | COUPON-02 expires at current timestamp | Validate exactly at/after expiry | Coupon rejected consistently | Coupon accepted for approximately one minute after expiry | Fail | BUG-005 |
| CHK-008 | VNPay return/IPN sandbox | VNPay sandbox unavailable | Complete VNPay flow and reconcile result | Signed callback updates payment once | External sandbox not available in cycle | Blocked | - |

### E. Order lookup, shipment and webhook

| ID | Scenario / technique | Preconditions | Steps / request | Expected result | Actual result (synthetic) | Status | Defect |
|---|---|---|---|---|---|---|---|
| TRK-001 | Guest lookup with correct phone | ORDER-01 exists | `GET /api/orders/lookup?orderCode=ORD-QA-0001&phone=0900000001` | `200`; order summary returned | Matched expected | Pass | - |
| TRK-002 | Guest lookup with wrong phone | ORDER-01 exists | Same order code, different phone | `404`/generic response; no order details leaked | Matched expected | Pass | - |
| TRK-003 | Guest tracking local snapshot | Shipment exists | `GET /api/orders/lookup/tracking` with correct order/phone | `200`; tracking code, provider status and timeline | Matched expected | Pass | - |
| TRK-004 | Webhook missing security headers | None | `POST /api/webhooks/minilogistics` without signature/timestamp | `400`; no state change | Matched expected | Pass | - |
| TRK-005 | Webhook invalid/stale signature | Existing shipment | Send stale timestamp or invalid HMAC | `401`; no timeline/order update | Matched expected | Pass | - |
| TRK-006 | Duplicate webhook event - Idempotency | Valid delivered event already processed | Re-send same `eventId` and payload | `200` acknowledged as duplicate; one timeline row; loyalty awarded once | Second timeline row is inserted | Fail | BUG-006 |
| TRK-007 | Live refresh during provider outage | Local snapshot exists; provider offline | Admin refresh tracking | Controlled error; local snapshot remains readable in normal lookup | Provider sandbox unavailable; recovery behavior not fully observed | Blocked | - |

### F. Admin order operations and UI

| ID | Scenario / technique | Preconditions | Steps / request | Expected result | Actual result (synthetic) | Status | Defect |
|---|---|---|---|---|---|---|---|
| ADMIN-001 | Anonymous calls admin orders | No JWT | `GET /api/admin/orders` | `401`; no data | Matched expected | Pass | - |
| ADMIN-002 | Admin reads and updates valid order | Admin JWT; valid transition | GET detail, then `PUT /api/admin/orders/{id}/status` | `200`; status and history updated once | Matched expected | Pass | - |
| ADMIN-003 | Preserve list filters after detail navigation | Admin UI opened with status/search filters | Open order detail, use Back | Previous search, status and page retained | Filters reset to default | Fail | BUG-007 |

## 5. Database validation samples

Use only against local/sandbox data. Replace placeholders with values captured during the test.

```sql
-- Confirm checkout did not create duplicate orders for the same synthetic test identity/window.
SELECT "OrderCode", "Phone", "TotalAmount", "CreatedAtUtc"
FROM "Orders"
WHERE "Phone" = '0900000001'
ORDER BY "CreatedAtUtc" DESC;

-- Confirm one shipment record and inspect provider state.
SELECT "OrderId", "Provider", "ProviderShipmentId", "TrackingCode",
       "ProviderStatus", "LastSyncedAtUtc"
FROM "OrderShipments"
WHERE "TrackingCode" = 'ML-QA-0001';

-- Confirm duplicate webhook protection by event id.
SELECT "EventId", COUNT(*) AS occurrence_count
FROM "ShipmentEventInbox"
WHERE "EventId" = 'evt-qa-delivered-001'
GROUP BY "EventId";
```

Expected: one logical order for an idempotent checkout, one shipment per order, and `occurrence_count = 1` for a unique webhook event.

## 6. Evidence convention

Suggested sanitized evidence names:

- `AUTH-006-postman-response-redacted.png`
- `CART-006-request-response-redacted.json`
- `CHK-006-db-query-redacted.png`
- `TRK-006-event-inbox-query-redacted.png`

Before sharing, remove Authorization headers, cookies, passwords, API keys, signatures, connection strings, real emails/phones and machine-specific paths.

