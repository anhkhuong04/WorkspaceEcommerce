# Shipping acceptance evidence

Tests use synthetic identities and prices. PostgreSQL integration tests exercise
real EF transactions/locks/constraints; browser UI mocks cover presentation states.
Real browser matrix uses an isolated API/PostgreSQL and HTTP protocol emulator,
not Playwright mocks for commerce. See [runbook](../../services/runbooks/workspace-shipping.md).

| Acceptance group | Executable coverage |
| --- | --- |
| Catalog/coverage/version | Shipping administrative catalog, coverage and rate administration Application/Infrastructure/API tests; generated catalog drift check |
| Weight/whole VND/overflow/boundaries | WorkspaceShippingRateCalculator tests, rate validation and PostgreSQL quoted checkout |
| Draft/publish/revision/immutable history | Shipping rate API concurrency/auth tests; `shipping-admin.spec.ts` |
| Subject/cart/address/payment/expiry/revocation/replay | `ShippingCheckoutIntegrationTests`, independent-scope PostgreSQL race/rollback cases; `shipping-storefront.spec.ts` |
| Method × payment and provider isolation | `shipping-flow.spec.ts`: all six real browser/API/DB cases, signed synthetic VNPay return/IPN, transfer/COD collection; local provider-outage zero calls |
| Fulfillment/cancel/payment races/retry | Workspace delivery and quoted fulfillment PostgreSQL tests; replay, cancellation-in-flight compensation and disabled-intake backlog |
| Frozen charge versus actual cost | Quoted fulfillment API tests and protocol emulator (quote 33,000; create cost 43,000); final money/PDF/lookup reads |
| Ownership/guest proof/private data | `ShippingReadModelIntegrationTests` and existing customer/payment/receipt authorization tests |
| Legacy/import/paid/outbox compatibility | Existing checkout/order import/payment/webhook suites; explicit import-selection rejection; migration clean/upgrade seeds |
| Paging/query budget | Admin list exact three SELECTs for 1 and 20 rows; customer list three; quote batch budget with multiple cart lines |
| UI timing/zero/confirmation/language | Shipping hook/state Vitest, five storefront and five admin mock browser scenarios (including Vietnamese/keyboard confirmation), existing accessibility checks |
| Schema | PostgreSQL clean/upgrade script, EF pending-model check and migration SQL review |

CI runs backend tests, frontend lint/typecheck/build/Vitest, both shipping UI suites
and the isolated six-case shipping matrix. Commands/results and any environmental
blocks are recorded in `task/task_shipment.md`. Generated evidence belongs in
ignored `artifacts/`, never customer data or real credentials in Git.

These tests verify correctness and bounded query count, not maximum capacity.
Production-like load/soak, real carrier sandbox certification, real tariff approval
and human QA sign-off remain environment-specific release gates. Do not infer them
from a test count or a synthetic price sheet.
