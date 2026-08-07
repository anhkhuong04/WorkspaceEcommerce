# Test Summary Report - Workspace E-Commerce

> **Portfolio sample | Synthetic results | Not a production release report**

## 1. Executive summary

Manual test cycle `WEC-MANUAL-CYCLE-01` covered the highest-risk customer and integration flows: authentication, catalog, cart, checkout, order lookup, JWT/RBAC and shipment webhook processing. Of 36 planned cases, 34 were executed and 2 were blocked by unavailable external sandbox behavior. Initial execution produced 27 passes and 7 failures.

Seven synthetic defects were logged. Four were marked closed after simulated retest. One High severity stock-validation defect remains open; therefore the release recommendation for this portfolio candidate is **NO-GO until BUG-002 is fixed and regression-tested**.

## 2. Environment

| Component | Configuration |
|---|---|
| API | ASP.NET Core Web API on local HTTP endpoint |
| Database | PostgreSQL local container with synthetic seed data |
| Frontend | Storefront and Admin React apps on local dev servers |
| API client | Postman / Swagger OpenAPI |
| External services | VNPay demo and MiniLogistics sandbox (partially simulated) |
| Data policy | Synthetic users/orders only; secrets injected at runtime |

## 3. Execution metrics

| Metric | Count | Rate |
|---|---:|---:|
| Planned | 36 | 100.0% |
| Executed | 34 | 94.4% of planned |
| Pass - initial | 27 | 79.4% of executed |
| Fail - initial | 7 | 20.6% of executed |
| Blocked | 2 | 5.6% of planned |
| Closed after retest | 4 defects | 57.1% of defects |

### Results by module

| Module | Planned | Pass | Fail | Blocked |
|---|---:|---:|---:|---:|
| Authentication / authorization | 6 | 5 | 1 | 0 |
| Catalog | 5 | 5 | 0 | 0 |
| Cart | 7 | 5 | 2 | 0 |
| Checkout / coupon / payment | 8 | 5 | 2 | 1 |
| Order / shipment / webhook | 7 | 5 | 1 | 1 |
| Admin order operations / UI | 3 | 2 | 1 | 0 |
| **Total** | **36** | **27** | **7** | **2** |

## 4. Defect summary

| Severity | Raised | Closed | Open | Deferred |
|---|---:|---:|---:|---:|
| Critical | 0 | 0 | 0 | 0 |
| High | 2 | 1 | 1 | 0 |
| Medium | 4 | 3 | 0 | 1 |
| Low | 1 | 0 | 1 | 0 |
| **Total** | **7** | **4** | **2** | **1** |

Open/deferred items:

- **BUG-002 (High/P1, Open):** cart accepts quantity greater than stock.
- **BUG-007 (Low/P3, Open):** admin list filters are not preserved.
- **BUG-005 (Medium/P2, Deferred):** coupon expiry precision requires product-rule clarification.

## 5. Coverage assessment

### Covered well

- Positive and negative API validation.
- Quantity and expiry boundary values.
- Anonymous/customer/admin authorization paths.
- COD checkout and duplicate-submission risk.
- Guest order privacy check using order code + phone.
- Webhook missing/invalid security headers and duplicate event behavior.
- PostgreSQL checks for order, shipment and event idempotency.

### Coverage gaps

- Full VNPay callback/IPN reconciliation was blocked by the external sandbox.
- Provider-outage refresh recovery was not observed end-to-end.
- No load, soak, formal penetration, accessibility or complete browser/device matrix.
- Synthetic test cycle did not validate production monitoring, backup or disaster recovery.

## 6. Key risks and recommendation

| Risk | Assessment | Mitigation / exit condition |
|---|---|---|
| Overselling stock | High - core commerce integrity | Fix BUG-002; retest quantity 0/1/stock/stock+1 and checkout concurrency |
| Duplicate checkout | Reduced after retest | Run focused idempotency regression and DB assertion before release |
| Third-party callback uncertainty | Medium | Execute VNPay sandbox and provider outage tests when environment is available |
| Coupon expiry ambiguity | Medium/low | Product owner confirms inclusivity and UTC precision; update acceptance criteria |
| Admin filter reset | Low | Accept temporarily or fix in normal UI backlog |

**Recommendation:** NO-GO for unrestricted release while BUG-002 remains open. A new candidate can be considered after stock validation passes at cart update and checkout, no High/Critical defects remain open, and the blocked payment/tracking tests have either passed or have an approved mitigation.

## 7. Exit criteria for the next cycle

- 100% of Critical/High test cases executed.
- No open Critical or High defects.
- BUG-002 retest passes with database verification.
- Regression passes for cart totals, checkout idempotency and webhook duplicate handling.
- VNPay and provider-outage cases are executed or explicitly accepted by the release owner.
- Evidence is redacted and stored outside the public repository if it contains environment details.

## 8. Integrity note for interview use

This report is an independently prepared portfolio exercise based on the repository's documented behavior and API surface. The execution dates, build labels, actual results and defect lifecycle are intentionally synthetic. The value of the artifact is the tester's reasoning, coverage, traceability and reporting quality - not a claim that these exact defects were found in production.

