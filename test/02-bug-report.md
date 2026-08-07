# Bug Report - Workspace E-Commerce

> **All defects below are synthetic portfolio examples.** They demonstrate defect-writing and triage skills; they do not assert that the current repository contains these defects.

## 1. Severity and priority scale

| Level | Severity meaning | Priority meaning |
|---|---|---|
| Critical / P0 | Data loss, severe security impact, system unavailable | Immediate fix; release cannot proceed |
| High / P1 | Core flow blocked or materially incorrect; limited workaround | Fix before release candidate approval |
| Medium / P2 | Important behavior incorrect; workaround available | Plan in current/next sprint |
| Low / P3 | Minor usability, presentation or low-risk inconsistency | Fix when capacity permits |

## 2. Defect index

| ID | Title | Module | Severity | Priority | Status | Linked TC |
|---|---|---|---|---|---|---|
| BUG-001 | Email whitespace is stored inconsistently between registration and login | Auth | Medium | P2 | Closed | AUTH-006 |
| BUG-002 | Cart accepts quantity greater than available stock | Cart | High | P1 | Open | CART-006 |
| BUG-003 | Cart total is stale immediately after quantity update | Cart | Medium | P2 | Closed | CART-007 |
| BUG-004 | Rapid retry of checkout creates duplicate orders | Checkout | High | P1 | Closed | CHK-006 |
| BUG-005 | Coupon is accepted briefly after expiration boundary | Coupon | Medium | P2 | Deferred | CHK-007 |
| BUG-006 | Replayed webhook event inserts a duplicate shipment timeline entry | Shipment | Medium | P1 | Closed | TRK-006 |
| BUG-007 | Admin order list loses filters after returning from detail | Admin UI | Low | P3 | Open | ADMIN-003 |

## 3. Detailed reports

### BUG-001 - Email whitespace is stored inconsistently between registration and login

| Field | Detail |
|---|---|
| Environment | Local API `portfolio-candidate-1`; PostgreSQL local |
| Severity / Priority | Medium / P2 |
| Status | Closed - retest passed |
| Reproducibility | 3/3 |

**Precondition:** `khuong.qa+trim@example.com` is not registered.

**Steps:**

1. Call `POST /api/customer/auth/register` with email `" khuong.qa+trim@example.com "` and otherwise valid data.
2. Observe `201`.
3. Call `POST /api/customer/auth/login` with `khuong.qa+trim@example.com` and the same password.

**Expected:** Email is trimmed/normalized consistently; login succeeds.

**Actual (synthetic):** Registration succeeds, but login returns `401` because the stored value includes whitespace.

**Impact:** A customer can complete registration but cannot log in using the visible normalized email.

**Retest:** Passed on synthetic build `portfolio-candidate-2`; registration stores normalized email and duplicate check uses the same normalization.

---

### BUG-002 - Cart accepts quantity greater than available stock

| Field | Detail |
|---|---|
| Environment | Local API; PRODUCT-01 stock = 5 |
| Severity / Priority | High / P1 |
| Status | Open |
| Reproducibility | 5/5 |

**Steps:**

1. Create/open cart `qa-cart-session-001`.
2. Add PRODUCT-01 variant with quantity 5 and confirm success.
3. Update the same item to quantity 6 using `PUT /api/cart/items/{id}`.
4. Read the cart again.

**Expected:** API returns `409 Conflict` (or approved validation response); persisted quantity remains 5.

**Actual (synthetic):** API returns `200`; persisted quantity becomes 6 even though available stock is 5.

**Impact:** Checkout may oversell stock or fail later, causing poor customer experience and manual correction.

**Workaround:** Prevent checkout when final stock validation fails. This is not sufficient for release approval because the cart remains misleading.

**Suggested evidence:** Redacted request/response plus DB query showing variant stock and cart quantity.

---

### BUG-003 - Cart total is stale immediately after quantity update

| Field | Detail |
|---|---|
| Environment | Storefront + local API |
| Severity / Priority | Medium / P2 |
| Status | Closed - retest passed |
| Reproducibility | 4/4 |

**Steps:**

1. Add one unit of PRODUCT-01 to the cart.
2. Change quantity from 1 to 2.
3. Observe the returned cart and displayed total without refreshing the page.

**Expected:** Line subtotal and cart total immediately equal `unit price x 2`.

**Actual (synthetic):** Quantity is 2, but total still reflects quantity 1 until a full cart reload.

**Impact:** Customer sees an inconsistent amount and may abandon checkout.

**Retest:** Passed on `portfolio-candidate-2`; response DTO and UI state both use recalculated totals.

---

### BUG-004 - Rapid retry of checkout creates duplicate orders

| Field | Detail |
|---|---|
| Environment | Local API; COD; valid cart |
| Severity / Priority | High / P1 |
| Status | Closed - retest passed |
| Reproducibility | 3/3 with requests sent within 500 ms |

**Steps:**

1. Prepare a valid cart and COD checkout payload.
2. Send two identical `POST /api/checkout` requests within 500 ms (simulate double-click/network retry).
3. Query orders by the synthetic phone and creation window.

**Expected:** Only one order is created; the duplicate is rejected or returns the original result via an idempotency mechanism.

**Actual (synthetic):** Both requests return `201` with different order codes; two orders are stored.

**Impact:** Duplicate fulfillment/shipment, inventory reservation and customer support workload.

**Retest:** Passed on `portfolio-candidate-2` using a client request key; one order is stored and retry returns the same logical result.

---

### BUG-005 - Coupon is accepted briefly after expiration boundary

| Field | Detail |
|---|---|
| Environment | Local API; COUPON-02 expires at fixed UTC time |
| Severity / Priority | Medium / P2 |
| Status | Deferred - accepted risk for portfolio cycle |
| Reproducibility | 3/5 near the minute boundary |

**Steps:**

1. Configure COUPON-02 to expire at a known UTC timestamp.
2. At or immediately after the timestamp, call `POST /api/checkout/coupons/validate`.
3. Repeat at +30 and +60 seconds.

**Expected:** Requests at/after expiry are rejected consistently based on server UTC time.

**Actual (synthetic):** Coupon may be accepted until the next minute boundary.

**Impact:** Small unintended discount window; no data corruption.

**Triage note:** Clarify whether expiry is minute-inclusive. Document the rule and use one time precision across validation and persistence.

---

### BUG-006 - Replayed webhook event inserts a duplicate shipment timeline entry

| Field | Detail |
|---|---|
| Environment | Local API; valid MiniLogistics HMAC generated with a sandbox-only secret |
| Severity / Priority | Medium / P1 |
| Status | Closed - retest passed |
| Reproducibility | 5/5 |

**Steps:**

1. Send a valid `shipment.status_changed` webhook with event ID `evt-qa-delivered-001`.
2. Confirm `200` and one timeline entry.
3. Re-send the identical signed event.
4. Query shipment timeline and event inbox.

**Expected:** Second request is acknowledged as duplicate; one inbox record, one timeline entry and one loyalty award.

**Actual (synthetic):** Second request adds another timeline entry (loyalty remains unchanged).

**Impact:** Misleading tracking history and noisy operational data; could become financial impact if other side effects are not independently idempotent.

**Retest:** Passed on `portfolio-candidate-2`; duplicate response is acknowledged and counts remain unchanged.

---

### BUG-007 - Admin order list loses filters after returning from detail

| Field | Detail |
|---|---|
| Environment | Admin UI; Chromium desktop viewport |
| Severity / Priority | Low / P3 |
| Status | Open |
| Reproducibility | 5/5 |

**Steps:**

1. Open the admin order list.
2. Set status = `Processing`, search a synthetic order and navigate to page 2.
3. Open one order detail.
4. Use the in-app Back control.

**Expected:** Search, status and page are restored.

**Actual (synthetic):** List returns to default filters and page 1.

**Impact:** Extra work for admin users processing a large queue; no data integrity impact.

## 4. Evidence/redaction checklist

- Replace bearer tokens with `<REDACTED_TOKEN>`.
- Remove Cookie, Authorization, API key, webhook signature and connection string values.
- Use only local/sandbox order codes, emails and phone numbers.
- Crop unrelated browser tabs, notifications and machine usernames.
- Keep request URL, method, status, timestamp, relevant response body and DB assertion visible.

