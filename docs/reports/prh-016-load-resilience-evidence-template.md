# PRH-016 load and resilience evidence

> Copy this template into the release evidence system for one candidate. Do not record
> passwords, tokens, cookies, API keys, customer data, or webhook bodies.

## Candidate and approval

| Field | Value |
| --- | --- |
| Candidate image digest | `<registry/image@sha256:...>` |
| Git commit | `<commit>` |
| Environment / region | `<isolated staging only>` |
| Topology | `<API replicas, workers, PostgreSQL tier, object store, SignalR/rate-limit architecture>` |
| Data-generator version and cardinality | `<link/version/counts>` |
| Load generator / k6 version | `<host, version>` |
| Window (UTC) | `<start/end>` |
| Approvers | `<application, SRE, release manager>` |

## Approved SLO and traffic model

| Measure | Approved target | Observed | Pass? | Evidence link |
| --- | ---: | ---: | --- | --- |
| Read p50 / p95 / p99 | | | | |
| Admin-list p50 / p95 / p99 | | | | |
| Media-delivery p50 / p95 / p99 / content type | | | | |
| SignalR negotiate / upgrade / handshake success | | | | |
| Signed webhook acknowledgement / reject rate | | | | |
| Checkout p50 / p95 / p99 | | | | |
| HTTP 5xx / timeout / 429 rate | | | | |
| Throughput / concurrent VUs | | | | |
| CPU / memory / GC | | | | |
| PostgreSQL pool / query count / rows read | | | | |
| Queue lag / retry / dead-letter depth | | | | |
| External provider / object-store latency | | | | |

## Run inventory

| Suite | Profile or duration | VUs / iterations | Result | Metadata and summary |
| --- | --- | ---: | --- | --- |
| PublicRead | Smoke / Baseline / Peak / Soak | | | |
| AuthenticatedRead | | | | |
| AdminRead | | | | |
| MediaRead | | | | |
| SignalRConnectivity | | | | |
| SignedWebhook | | | | |
| Commerce | | | | |
| Resilience | | | | |

## Commerce integrity reconciliation

Record expected and observed values before any test-environment reset.

| Domain | Expected | Observed | Reconciled by | Evidence |
| --- | ---: | ---: | --- | --- |
| Cart item add/remove | | | | |
| Orders created | | | | |
| Stock reserved/consumed/restored | | | | |
| Coupon validations/redemptions | | | | |
| Payment transactions/state changes | | | | |
| Loyalty earn/redeem transactions | | | | |
| Webhook inbox/idempotency entries | | | | |
| Customer-email outbox | | | | |
| Shipment command outbox / provider commands | | | | |

## Fault and rolling-restart results

| Injected failure | Start/end UTC | Recovery bound | Observed recovery | Durable-work result | Owner / evidence |
| --- | --- | --- | --- | --- |
| PostgreSQL interruption | | | | | |
| S3 latency/failure | | | | | |
| SMTP failure | | | | | |
| MiniLogistics timeout/429/5xx | | | | | |
| Telemetry outage | | | | | |
| API replica kill | | | | | |
| Worker kill | | | | | |
| Rolling deployment | | | | | |

## Regression comparison and decision

| Comparison | Result | Follow-up owner / date |
| --- | --- | --- |
| PRH-008 query-plan/latency regression review | | |
| Resource growth / connection / queue trend review | | |
| Known limitations or accepted risks | | |
| Final PRH-016 recommendation | `pass / fail / blocked` | |
