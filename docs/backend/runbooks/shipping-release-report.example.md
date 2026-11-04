# Shipping release candidate report template

Copy this template into the deployment team's restricted evidence store. Never put
credentials, connection strings, tokens, private order details or customer addresses
in the report. Blank/Pending fields are a no-go. Automated test results do not
constitute business or QA sign-off. This template is not a completed release report.
For the current learning-project scope, use the separate
[automatic educational acceptance report](shipping-educational-acceptance.md);
this template is retained for a future deployed release.

## Candidate and deployment identity

| Field | Recorded value / retained evidence |
| --- | --- |
| Environment and approved sandbox URL | Pending |
| Full source commit SHA | Pending |
| Registry API image `image@sha256:...` | Pending |
| Compatible migration-job artifact and latest migration | Pending |
| API/worker replica versions (all understand legacy and selected methods) | Pending |
| Backup/restore evidence and recovery owner | Pending |
| Shipping config (six non-secret settings; all replicas agree) | Pending |
| Published rate-plan ID/revision and administrative catalog version | Pending |
| Business-approved real tariff / coverage / weight-readiness evidence | Pending |

Local image IDs and synthetic tariffs are local-test evidence only. They are not
registry/deployment identities or real tariff approval. Follow the general
[production release gates](production-release.md) as well as the shipping gates.

## Required checks on this candidate

For each row record status, timestamp, operator and retained evidence reference.
Record both failure and retry evidence; do not replace a missing check with a pass.

| Check | Status / evidence |
| --- | --- |
| CI backend/frontend/container, current dependency and secret audits | Pending |
| Clean/upgrade migration, historical totals/IDs and pending-model checks | Pending |
| Real-rate preview: representative regions, quantities, weight boundaries/limits | Pending |
| Local-only quote/order/payment/delivery/PDF with partner unavailable | Pending |
| Both enabled methods with provider sandbox and all enabled payment methods | Pending |
| Manual transfer, COD delivery/collection/remittance and duplicate protection | Pending |
| Failure/retry, cancellation and preserved stock/refund/loyalty history | Pending |
| Valid unused quote and accepted backlog continue after method intake is paused | Pending |
| Pending legacy outbox/callback replay after schema and all workers are deployed | Pending |
| Actual-cost discrepancy does not change accepted customer/payment amounts | Pending |
| Ownership/guest proof, public data and telemetry redaction | Pending |
| Quote latency/errors/conflicts, local backlog, partner queue/cost alerts | Pending |
| On-call responder and disable-new-quotes/forward-fix rehearsal | Pending |

## Final decision and signatures

| Responsibility | Name, decision, timestamp, evidence |
| --- | --- |
| Business: real fees, geographic coverage and weight limits | Pending |
| QA: candidate and deployed smoke | Pending |
| Operations: delivery teams, COD reconciliation, alerts and recovery | Pending |
| Release/platform owner: all general deployment gates | Pending |

**Decision:** NO-GO until every required check and signature is recorded. A signed
decision applies only to the candidate/config/rate revision recorded above. After a
material change, record and review a new candidate. Enable intake only through the
[shipping rollout sequence](../../services/runbooks/workspace-shipping.md).
