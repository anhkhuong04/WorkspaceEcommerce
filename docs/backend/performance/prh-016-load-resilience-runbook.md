# Load and resilience test runbook

This runbook produces candidate-specific k6 evidence. It does not define a
universal production SLO and must never be aimed at an unapproved production or
third-party environment.

## Safety boundary

- Default target is local. A non-local target requires
  `-AllowNonLocalTarget`, an approved isolated environment, and immutable
  `image@sha256:...` candidate identity.
- Admin, media, SignalR, and signed-webhook suites require
  `K6_TEST_ENVIRONMENT=isolated-staging`.
- Write tests require explicit `K6_ALLOW_*` flags and synthetic data. Checkout
  can call external providers, so keep quote/checkout disabled unless an
  isolated sandbox and cleanup owner are confirmed.
- Never put credentials in command text, committed files, k6 tags, or raw sample
  output. Raw samples are disabled for every secret-bearing/write suite.
- Stop on unexpected customer data, real provider traffic, error growth, queue
  growth, or loss of recovery. Record the candidate, topology, dataset, and stop
  decision outside Git.

## Suites

| Suite | Purpose | Important opt-in/configuration |
| --- | --- | --- |
| `PublicRead` | Catalog/blog/banner latency; smoke/baseline/peak/soak profiles | Optional `K6_PRODUCT_SLUG` |
| `AuthenticatedRead` | Customer reads and optional refresh rotation | `K6_ALLOW_AUTH_FLOW=true`; access token or synthetic email/password |
| `AdminRead` | Back-office read paths | `K6_ALLOW_ADMIN_READS=true`, `K6_ADMIN_ACCESS_TOKEN` |
| `MediaRead` | Durable media/CDN read | `K6_ALLOW_MEDIA_READS=true`, `K6_MEDIA_READ_URL` |
| `SignalRConnectivity` | Authenticated hub connection | `K6_ALLOW_SIGNALR_CONNECTIVITY=true`, `K6_SIGNALR_ACCESS_TOKEN` |
| `SignedWebhook` | Authenticated duplicate webhook behavior | `K6_ALLOW_SIGNED_WEBHOOK_TEST=true`, sandbox webhook secret |
| `Commerce` | Cart/coupon; optional quote/checkout | `K6_ALLOW_WRITE_TESTS=true`, synthetic variant; additional explicit flags for provider calls/checkout |
| `Resilience` | Read behavior while an operator injects/recovers a dependency fault | Optional `K6_EXPECT_RECOVERY=true` |

The k6 scripts fail with the exact missing variable when a suite needs more
input. Inspect `scripts/performance/k6/<suite>.js`; do not duplicate secrets into
this document.

## Run

Install Grafana k6, start the intended candidate, seed only synthetic data, and
establish a quiet baseline. Then run from the repository root:

```powershell
# Safe local smoke
./scripts/performance/run-prh-016-k6.ps1 -Suite PublicRead -Profile Smoke

# Example approved non-local read run
./scripts/performance/run-prh-016-k6.ps1 `
  -Suite PublicRead `
  -Profile Baseline `
  -BaseUrl https://staging.example.test `
  -CandidateIdentity registry.example/app@sha256:<64-hex> `
  -AllowNonLocalTarget
```

For suites that need credentials, set process-scoped environment variables,
run the wrapper, then remove them from the process. Do not paste tokens into the
command or retain them in shell history.

Results are written under ignored `artifacts/performance/` with metadata and a
k6 summary. Preserve the entire directory in the external release/test record.

## Observe and evaluate

Capture client latency/error thresholds together with server-side HTTP,
PostgreSQL, dependency, outbox, webhook, and resource signals. For multi-replica
runs, identify every replica and use maximum aggregation for queue gauges.

Before approval, compare with the same suite/dataset/topology baseline and
answer:

1. Did k6 thresholds pass without unexpected functional errors?
2. Did p95/p99, dependency latency, database saturation, or queue age regress?
3. Did duplicate payment/webhook/outbox invariants remain intact?
4. During a resilience run, did the service fail safely and recover without
   manual data edits or unbounded backlog?
5. Are the target, image digest, config revision, test window, and operator
   reproducible without recording secrets or customer data?

Thresholds embedded in scripts are initial guardrails. Product/SRE owners must
approve service SLOs and capacity targets for the actual deployment.

## Cleanup

Remove synthetic carts/orders/uploads, revoke test tokens and temporary access,
verify queues return to baseline, and delete local raw samples under the
retention policy. Never commit generated load evidence.
