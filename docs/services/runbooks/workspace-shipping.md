# WorkspaceEC shipping operations and rollout

Current implementation: [shipping architecture/API](../../backend/architecture/shipping.md).
Validation: [shipping matrix](../../backend/development/shipping-validation.md).
This runbook does not approve real rates or enable a deployed environment.

## Educational acceptance

For this learning project, the owner approved automatic synthetic acceptance on
2026-11-04. [The acceptance report](../../backend/runbooks/shipping-educational-acceptance.md)
records the checks and known limitations. Synthetic prices/coverage in an isolated
test database can satisfy SHIP-14 for this educational scope; real tariffs, a staging
deployment and human business/QA signatures are not required for that scope.
The production checklist below remains applicable only if the project is later
deployed. Educational acceptance neither enables production intake nor marks the
general production-release gates as passed.

## Configure and review rates

1. In admin `/shipping`, inspect catalog version and paged weight-readiness report.
   Correct active sellable SKU weights in kg. No default 0.5 kg exists for local prices.
2. Create a draft with origin province and region mappings. Each catalog province
   must belong to exactly one region. Enter whole VND base fee, positive base/step
   grams, nonnegative step fee and any explicit maximum weight.
3. Validate complete coverage. Preview representative destination/SKU/quantity
   cases at weight boundaries; compare with a business-approved fee sheet.
   Preview clearly identifies draft versus Published rate revision.
4. Review and publish with reason. A stale revision/active-plan conflict requires
   reload and review. Do not repeatedly submit a changed payload. Published plans
   cannot be edited; clone a draft for replacement. Review the actor/time/history.
5. Retire only when replacement/intake suspension has been reviewed. Retirement
   stops new local quotes but does not alter accepted orders or existing valid quotes.

No application startup seed publishes production prices. Demo/E2E synthetic tariffs
are test-only. Business must approve real fees, coverage and weight limits.

## Configuration and deployment sequence

| Setting | Default | Meaning |
| --- | --- | --- |
| `ShippingCheckout__Enabled` | false | Storefront capability for additive quoted checkout |
| `ShippingCheckout__WorkspaceEnabled` | true | Intake of new local quotes |
| `ShippingCheckout__ExternalEnabled` | true | Intake of new partner quotes |
| `ShippingCheckout__QuoteLifetimeMinutes` | 10 | New quote lifetime |
| `ShippingCheckout__UnusedQuoteRetentionDays` | 30 | Retention of expired, unused quotes only |
| `ShippingCheckout__ExternalQuoteTimeoutSeconds` | 12 | Bounded carrier quote deadline |

The development Compose API forwards all six settings from `.env`; omitted values
use the defaults above. `.env.example` documents them without enabling intake.
After editing flags, recreate the API container (`docker compose --env-file .env up
-d --no-deps api`) and check `GET /api/checkout/shipping/capabilities`; restart/recreate
each replica through its deployment platform. Configuration is loaded at startup,
not a live admin toggle. Compose is development-only and still requires its existing
provider/payment placeholders to be replaced even for local-only experiments.

`./scripts/verify-shipping-compose.ps1` renders defaults, local-only intake and
paused-method configurations using synthetic `.env.example` values. It neither
starts containers nor reads your local `.env`, and never prints resolved secrets.

1. Back up and run the one-shot migration job (including
   `20261101113222_AddWorkspaceDeliveryOperations`). API replicas never migrate.
2. Deploy compatible API and **all worker replicas**, then verify readiness. Never
   run an old worker against new orders it cannot distinguish by persisted method.
3. Keep quoted intake off while reviewing credentials, current catalog, weights,
   real rate draft and coverage. MiniLogistics credentials are needed only if that
   integration is used; local operations resolve without them.
4. Business reviews/publishes real rates. QA signs the checklist on the exact commit,
   migration version, config and rate-plan revision. Keep sign-off evidence outside Git.
5. Smoke the candidate with approved test accounts: quote/total, payment, dispatch,
   tracking, receipt, cancellation and pending legacy outbox. Local-only smoke must
   work while the partner is unavailable. Use provider sandbox for a deployed smoke;
   the isolated runner below never contacts a real provider.
6. Enable quoted checkout and only the reviewed methods. Monitor before expansion.

**Pause new quotes:** keep `Enabled=true` and set the affected method flag false.
This leaves the storefront in the quoted flow and explicitly shows unavailability.
Setting `Enabled=false` returns the storefront to the legacy flow; it is not an
emergency stop of all orders. The legacy endpoint remains for compatibility.
Flags do not revoke valid quotes, stop accepted work, or rewrite money. To withdraw
a particular unused quote, use the admin revoke endpoint with a recorded reason.
Accepted quotes cannot be revoked. Apply config consistently to every replica.

## Delivery and exceptions

- WorkspaceEC: confirm order; confirm full prepaid payment if required; assign an
  operational team reference; review dispatch. Record delivered or failed with a
  reason. A failed delivery can retry through permitted actions. Cancellation uses
  the order workflow with current shipment revision, retaining stock/refund guards.
- New COD: record Delivered, then full collection with a unique bank/receipt reference.
  Record remittance separately. A duplicate identical confirmation is safe; a
  reused conflicting reference is not. Investigate partial/excess amounts offline.
- MiniLogistics: use existing refresh/retry and [outbox operations](background-outbox-operations.md).
  On timeout, inspect durable command/idempotency state before retry. Never change
  method or publish a new customer charge to repair a carrier error.
- Actual cost discrepancy: compare nullable shipment cost with frozen selection
  charge. Unknown remains unknown. Review supplier billing/margin; never recalculate
  order/payment totals or silently charge the buyer again.
- Quote conflict/expiry: preserve an uncertain accepted request for exact recovery;
  otherwise request a new quote and obtain explicit customer price confirmation.

## Monitoring without customer payloads

Use `shipment.checkout.requests` and `shipment.checkout.duration` from the
`WorkspaceEcommerce.Shipments` meter, with fixed operation (`quote`/`accept`),
method and ResultStatus outcome tags. `shipment.checkout.quote_rejections` counts
expiry, revocation and input mismatch with bounded reason tags. Existing shipment/
outbox meters cover carrier failures, retry/dead-letter and oldest active queue age.
Never use order/quote IDs, phone, address, reference, actor or payload as labels.
Do not claim these in-process meters provide distributed rate limiting or capacity.

Use restricted read-only database aggregation to split operational state by persisted
method: local open/failed stages and oldest age; expired/revoked unaccepted quotes;
accepted selection rate-plan revisions; publish audit; partner queue age and nullable
actual-cost difference. These are operational queries, not public endpoints. Do not
log/export `snapshot_json`, timeline/audit notes or addresses. Review Published plans
and their history through admin; no high-cardinality plan ID metric label is needed.

Example aggregate checks in an authorized read-only PostgreSQL session (bound the
time range to the operational window; no customer columns):

```sql
SELECT provider, provider_status, count(*) AS shipments,
       max(now() - created_at_utc) AS oldest_age
FROM shipping.order_shipments
WHERE provider_status NOT IN ('Delivered', 'Cancelled')
GROUP BY provider, provider_status;

SELECT method, count(*) FILTER (WHERE expires_at_utc <= now()) AS expired_unused,
       count(*) FILTER (WHERE revoked_at_utc IS NOT NULL) AS revoked_unused
FROM shipping.quotes
WHERE order_id IS NULL AND created_at_utc >= now() - interval '1 day'
GROUP BY method;

SELECT s.method, count(*) AS accepted,
       count(p.actual_cost_amount) AS cost_known,
       sum(p.actual_cost_amount - s.customer_charge) AS known_cost_difference
FROM shipping.order_selections s
JOIN shipping.order_shipments p ON p.order_id = s.order_id
WHERE p.created_at_utc >= now() - interval '7 days'
GROUP BY s.method;
```

SQL nulls preserve unknown actual cost. Count `cost_known` before interpreting a
sum; a missing shipment is not a zero-cost shipment.

Alert on sustained quote errors/latency, rising conflicts/expiry, local backlog age,
repeated FailedDelivery, partner dead letters and growing cost discrepancy. Establish
thresholds from measured traffic and delivery SLAs; this change establishes no
production throughput target. Existing telemetry exporter setup is required; see
[observability](observability-and-alerting.md).

## Rollback and recovery

Pause new quotes by method, keep compatible workers running, and forward-fix.
Honor valid quotes and accepted local/partner work. Reconcile payments, stock,
pending provider creates/cancels and COD collection before resuming intake.
Do not delete selections, plans, quotes, shipments, payment receipts or audit; do
not apply destructive Down migrations after accepting new orders. A code rollback
must retain compatibility with all persisted methods. Re-enabling is a new reviewed
smoke on the repaired candidate.

## Isolated validation commands

Run `./scripts/run-prh-015-storefront-e2e.ps1 -ShippingScenario` for a fresh Docker
network/PostgreSQL, migration/seed job, API, browser and synthetic provider. Six cases
cover method × COD/transfer/signed VNPay. The provider returns a different creation
cost from its quote; local cases run with provider quote outage and assert zero calls.
Only the synthetic gateway page is intercepted. Test credentials are generated in
memory; loopback guards prevent targeting a deployed database/API. Containers are
removed by the runner; artifacts are ignored and can contain synthetic access proofs.

Production release remains gated on approved real tariff/coverage, configured
shipping teams and QA/operator sign-off. Automated test success is not that approval.

Release evidence checklist (environment owner completes on the candidate):

Use the [candidate report template](../../backend/runbooks/shipping-release-report.example.md)
to record identities, evidence and signatures in the deployment team's restricted
store. The general production-release gates remain required.

- [ ] Candidate commit/image digest, migration version and all replica versions recorded.
- [ ] Business-approved real tariff revision, nationwide coverage and SKU weights reviewed.
- [ ] Payment/COD collection and delivery teams can operate all enabled methods.
- [ ] Local-only outage, both-method sandbox smoke and legacy backlog verified.
- [ ] Alerts/exporter and on-call recovery/forward-fix owner assigned.
- [ ] QA and business/operator sign-off recorded before enabling intake.
