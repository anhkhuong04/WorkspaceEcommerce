# PRH-016 load and resilience runbook

## Purpose and safety boundary

This runbook makes PRH-016 repeatable; it does not authorize a production load test,
fault injection, or deployment. The default command runs a 30-second, read-only smoke
against `http://localhost:5080`. A non-local target is refused unless the operator
supplies both `-AllowNonLocalTarget` and an immutable
`image@sha256:<digest>` candidate identity.

Run peak, soak, checkout, external-provider, rolling-restart, and fault-injection
tests only in an approved isolated staging environment. Use synthetic accounts,
addresses, emails, orders, coupons, and media. Never put passwords, access tokens,
refresh cookies, API keys, object-store credentials, webhook payloads, or real
customer data in shell history, evidence, raw k6 output, tickets, or this document.

The provisional release gates below must be replaced by business/SRE-approved values
before a release decision:

| Measure | Provisional gate |
| --- | --- |
| Read API latency | p95 <= 500 ms; p99 <= 1 s |
| Application-controlled checkout latency | p95 <= 2 s, excluding an approved external-provider budget |
| HTTP 5xx | < 1% for steady-state runs |
| Commerce integrity | Zero unexplained stock, coupon, payment, loyalty, webhook, outbox, or shipment inconsistencies |
| Peak | 100 concurrent virtual users for at least 30 minutes after ramp-up |
| Soak | Lower approved traffic for 8 hours after ramp-up |

## Prerequisites

1. Use the exact release candidate image/digest and record its Git commit. Do not
   rebuild in the test environment.
2. Prepare representative PostgreSQL cardinality and S3-compatible original/variant
   media data. Retain the generator version and compare query plans with the
   [PRH-008 query-plan runbook](prh-008-query-plan-runbook.md).
3. Install [Grafana k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) on a
   controlled load-generator host. `scripts/performance/run-prh-016-k6.ps1` fails
   clearly if `k6` is not on `PATH`.
4. Baseline the target: live/ready health is green, dashboards are available, DB pool
   and queue metrics are visible, and the SRE owner has scheduled the test window.
5. For authenticated reads and SignalR, create dedicated synthetic, non-TOTP customer
   accounts with short-lived access tokens. For admin reads, provision a least-privilege
   synthetic admin token. For media reads, choose one existing, non-sensitive public
   test object URL without a query string. For the signed webhook test, obtain the
   isolated MiniLogistics webhook secret through the load-generator secret mechanism.
   For commerce flows, create a high-stock synthetic variant and, if needed, unique
   non-production coupons. Checkout tests require a sandbox shipment/payment path and
   an environment that can be reset afterward.

The runner writes `run-metadata.json` and `k6-summary.json` under
`artifacts/performance/` (ignored by Git). Raw samples are deliberately opt-in because
an 8-hour test can create a very large file; use `-CaptureRawSamples` only when the
storage and retention decision has been made. The runner permits them only for the
public-read and resilience suites; every suite carrying credentials, a signature, or
synthetic customer data refuses raw sample output.

## Repeatable suites

All commands below run from the repository root. The wrapper never writes secret
environment variables to its metadata file.

### 1. Read-only storefront traffic

This covers catalog list/search/detail, categories, banners, published blogs, product
reviews, and a sparse readiness probe. It discovers an active product from the target,
or accepts `K6_PRODUCT_SLUG` when the target catalog is intentionally fixed.

```powershell
# Safe local smoke: one VU, 30 seconds, read-only.
./scripts/performance/run-prh-016-k6.ps1 -Suite PublicRead

# Approved staging peak: 5-minute ramp, 30 minutes at 100 VUs, 5-minute ramp down.
./scripts/performance/run-prh-016-k6.ps1 `
  -Suite PublicRead `
  -Profile Peak `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 100 `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget

# Approved eight-hour soak. Choose the approved lower traffic level explicitly.
./scripts/performance/run-prh-016-k6.ps1 `
  -Suite PublicRead `
  -Profile Soak `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 20 `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

`Smoke`, `Baseline`, `Peak`, and `Soak` are the only valid `-Profile` values for
`PublicRead`. The peak and soak profiles encode the required steady-state durations;
the candidate identity is tagged in k6 output and recorded in metadata.

### 2. Authenticated read and refresh rotation traffic

The authenticated suite performs customer profile/order/loyalty reads. It can use a
short-lived synthetic access token or log in with a synthetic account. With
`K6_TEST_REFRESH_ROTATION=true`, a single VU logs in and exercises refresh-token
rotation through the HttpOnly cookie jar. Do not use a real customer or a TOTP-enabled
account.

Supply credential values through the load-generator secret mechanism or process
environment; do not put them on the command line:

```powershell
$env:K6_ALLOW_AUTH_FLOW = 'true'
$env:K6_CUSTOMER_EMAIL = '<synthetic-account-email>'
$env:K6_CUSTOMER_PASSWORD = '<synthetic-account-password>'
$env:K6_TEST_REFRESH_ROTATION = 'true'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite AuthenticatedRead `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 1 `
  -Duration '5m' `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

Keep login/refresh volume below the approved authentication rate-limit budget. The
high-volume read mix must use service-approved synthetic credentials/tokens and be
interpreted with the rate-limit topology from PRH-012.

### 3. Privileged admin list reads

`AdminRead` covers dashboard, product, order, coupon, blog, comment, and review list
reads using a pre-issued, least-privilege synthetic admin access token. It never logs
in, writes data, or emits the token in metadata, metric tags, or raw samples. This
suite is restricted to `K6_TEST_ENVIRONMENT=isolated-staging` even when the API origin
appears local.

```powershell
$env:K6_TEST_ENVIRONMENT = 'isolated-staging'
$env:K6_ALLOW_ADMIN_READS = 'true'
$env:K6_ADMIN_ACCESS_TOKEN = '<short-lived-synthetic-admin-token-from-secret-store>'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite AdminRead `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 1 `
  -Duration '2m' `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

Do not reuse an operator, break-glass, or production admin account. Coordinate the
read volume with the default rate-limit partition and record dashboard aggregate
latency separately from paged list latency.

### 4. Public media delivery reads

`MediaRead` fetches one already-published synthetic media object and checks HTTP 200
and its expected content type. It does not upload, enumerate, delete, or use a signed
URL. The URL must be a plain `http(s)` URL with no credentials, query string, or
fragment; use an object on the isolated test media host only.

```powershell
$env:K6_TEST_ENVIRONMENT = 'isolated-staging'
$env:K6_ALLOW_MEDIA_READS = 'true'
$env:K6_MEDIA_READ_URL = 'https://staging-assets.example.invalid/media/load-test/original.webp'
$env:K6_MEDIA_EXPECTED_CONTENT_TYPE = 'image/webp'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite MediaRead `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 5 `
  -Duration '2m' `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

Measure media-origin/CDN latency and bytes separately from API latency. A media read
is a delivery check, not an object-store retention, restore, malware-scanning, or
authorization test.

### 5. SignalR notification connectivity

`SignalRConnectivity` performs the authenticated hub negotiation, WebSocket upgrade,
and SignalR JSON-protocol handshake using a short-lived synthetic customer token. The
token stays in the `Authorization` header rather than a URL. This measures connection
continuity only; it does not claim that a business notification was delivered. Verify
end-to-end delivery and reconnect behavior during the controlled replica/failure
exercises below.

```powershell
$env:K6_TEST_ENVIRONMENT = 'isolated-staging'
$env:K6_ALLOW_SIGNALR_CONNECTIVITY = 'true'
$env:K6_SIGNALR_ACCESS_TOKEN = '<short-lived-synthetic-customer-token-from-secret-store>'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite SignalRConnectivity `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 1 `
  -Duration '2m' `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

Keep the connection count within the approved hub and load-generator budget. Attach
negotiate, upgrade, handshake, disconnect, reconnect, replica, and backplane evidence
to the run; a successful handshake alone does not validate multi-instance routing.

### 6. Signed MiniLogistics webhook callback

`SignedWebhook` creates a fresh timestamp and HMAC signature for the controller's
safe `webhook.test` event. That event is acknowledged before shipment processing, so
it creates no order, shipment, timeline, inbox, or provider side effect. It requires
an isolated staging environment, an explicit flag, and the isolated webhook secret
from a secret store. The secret, signature, and raw callback samples are never written
to runner metadata or k6 output.

```powershell
$env:K6_TEST_ENVIRONMENT = 'isolated-staging'
$env:K6_ALLOW_SIGNED_WEBHOOK_TEST = 'true'
$env:K6_MINILOGISTICS_WEBHOOK_SECRET = '<isolated-webhook-secret-from-secret-store>'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite SignedWebhook `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 1 `
  -Iterations 1 `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

Do not substitute a shipment-created/status-changed payload for `webhook.test` in a
load suite. Exercise real shipment webhook idempotency only through a separate,
approved, reconciled integration test with a bounded event inventory.

### 7. Cart, coupon, shipping quote, and checkout traffic

`Commerce` is intentionally disabled until all explicit flags are present. By default
it adds and removes a test-cart item; this is the only automatic cleanup it performs.
Shipping quote and checkout call external sandbox services and therefore require an
isolated staging target, an explicit provider-call flag, and named test data.

```powershell
$env:K6_ALLOW_WRITE_TESTS = 'true'
$env:K6_TEST_VARIANT_ID = '<high-stock-synthetic-variant-guid>'

# One deterministic add/read/remove cart iteration. No checkout or external call.
./scripts/performance/run-prh-016-k6.ps1 `
  -Suite Commerce `
  -Iterations 1

# Isolated staging checkout. Every created order must be reconciled and the test
# environment reset after the run; it is not a production-safe command.
$env:K6_ALLOW_EXTERNAL_PROVIDER_CALLS = 'true'
$env:K6_ALLOW_CHECKOUT = 'true'
$env:K6_ENABLE_CHECKOUT = 'true'
$env:K6_ENABLE_SHIPPING_QUOTE = 'true'
$env:K6_TEST_ENVIRONMENT = 'isolated-staging'
$env:K6_CHECKOUT_CUSTOMER_NAME = 'PRH-016 Load Test'
$env:K6_CHECKOUT_PHONE = '<synthetic-phone>'
$env:K6_CHECKOUT_STREET = '<synthetic-street>'
$env:K6_CHECKOUT_WARD = '<synthetic-ward>'
$env:K6_CHECKOUT_PROVINCE = '<synthetic-province>'
# Optional: $env:K6_TEST_COUPON_CODE = '<unique-synthetic-coupon>'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite Commerce `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 1 `
  -Iterations 1 `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

The test does not cancel or delete orders after checkout. Record the expected order
count, stock movement, coupon use, payment state, loyalty transactions, webhook inbox,
email/shipment outbox state, and shipment commands before resetting the isolated
environment. A coupon must be unique/unlimited for the planned iteration count; never
reuse a business coupon.

### 8. Availability, recovery, and rolling-restart probes

The resilience suite continuously alternates `/health/ready` and catalog probes. It
does **not** create failures itself. An approved platform operator injects one failure
at a time while the script captures availability and three-success-probe recoveries.

```powershell
$env:K6_EXPECT_RECOVERY = 'true'

./scripts/performance/run-prh-016-k6.ps1 `
  -Suite Resilience `
  -BaseUrl 'https://staging-api.example.invalid' `
  -VirtualUsers 10 `
  -Duration '20m' `
  -CandidateIdentity 'registry.example.invalid/workspace-ecommerce-api@sha256:<64-hex-digest>' `
  -AllowNonLocalTarget
```

Schedule and capture each experiment separately:

| Failure/recovery exercise | Operator action | Required evidence |
| --- | --- | --- |
| PostgreSQL interruption | Deny/restart a test DB connection path, then restore it. | Ready transition, pool saturation/recovery, retries, queue drain, no lost/duplicate work. |
| S3 latency/failure | Use a staging fault proxy or provider sandbox policy. | Upload/read behavior, object retry bounds, metadata/object consistency. |
| SMTP failure | Reject mail at the staging relay, then restore it. | Bounded retries/jitter, email outbox drain, no sensitive recipient/body telemetry. |
| MiniLogistics timeout/429/5xx | Use the provider sandbox/fault proxy. | Circuit behavior, bounded retries, one idempotency key and one completed command. |
| Telemetry outage | Block the telemetry export path only. | Application stays healthy; telemetry backlog/drop policy and recovery are measured. |
| API replica or worker kill | Terminate one replica/worker during traffic. | Health routing, SignalR reconnect, refresh behavior, lease recovery, durable work exactly once. |
| Rolling deployment | Roll the exact image digest one replica at a time. | No unexpected 5xx spike, session/SignalR/media continuity, queues drain after rollout. |

Do not use a broad network outage, delete a production object, or kill a database
without the change record, bounded blast radius, rollback plan, and on-call owner.

## Evidence and interpretation

For every run, attach the k6 summary/metadata plus dashboard or query links covering:

- p50/p95/p99 latency, request rate, errors, timeouts, 429s, and recovery windows;
- CPU, memory/GC, DB connections/pool waits, query count/rows read, PostgreSQL plans,
  queue lag/retry/dead-letter counts, external-call latency, and object-store latency;
- expected versus observed cart/order/coupon/stock/payment/loyalty/webhook/outbox/
  shipment-command counts; and
- the candidate digest, Git commit, data-generator version, target topology, and fault
  schedule.

Use [the PRH-016 evidence template](../reports/prh-016-load-resilience-evidence-template.md).
Any threshold failure, unexplained integrity mismatch, unbounded resource trend, or
material regression from PRH-008 is a release blocker until it has an owner and a
retest result.
