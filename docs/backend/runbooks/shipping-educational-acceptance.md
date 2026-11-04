# SHIP-14 educational acceptance — 2026-11-04

The project owner requested automatic acceptance for a learning project with no
real deployment. The current scope uses synthetic prices/identities and isolated
PostgreSQL/API/provider containers. No real tariff approval, staging URL or human
QA/business signature is required. This records educational verification; it does
not authorize a production deployment or waive the general production gates.

## Candidate and changes

- Runtime/configuration candidate: `1ecbacf5155364e0d7cb867a6caa2a3f6265a8aa`,
  based on the completed SHIP-00–13 source at `bcd222b`.
- Docker Compose now forwards all six `ShippingCheckout` settings. Missing values
  preserve default-off checkout, both method capabilities true, ten-minute quotes,
  thirty-day unused-quote retention and a twelve-second external deadline.
- `.env.example` explains method suspension and legacy fallback. The regression
  script renders three configurations without starting services or reading `.env`.
- `System.Security.Cryptography.Pkcs` is pinned from EF tooling's transitive 9.0.0
  to 9.0.21 with `PrivateAssets=all`; only the API lockfile changes. This addresses
  [GHSA-846f-f764-76mx](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx)
  and [GHSA-2fmv-rv2c-59x7](https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7).
- An empty tracked web root lets a clean checkout start the API integration host;
  the static-assets manifest referenced this directory before application startup.
- The [runbook](../../services/runbooks/workspace-shipping.md) and future deployed
  [candidate template](shipping-release-report.example.md) distinguish educational
  checks from deployment evidence and explain recreate/restart of configured APIs.

## Verification results

The complete command output is in ignored `artifacts/backend/ship14/`; browser
evidence is in `artifacts/frontend/ship14-e2e/`. These artifacts may contain
synthetic access proofs and must not be committed. Results below apply to the
clean worktree, excluding unrelated uncommitted UI/catalog/banner changes.

| Check | Result |
| --- | --- |
| Compose defaults / local-only / pause-both | Passed, six settings in each case; regression first failed before mapping |
| Locked restore and Release build | Passed, zero warnings/errors after PKCS patch |
| Full backend regression | Passed; 754 tests, zero skips (346 Application, 241 Infrastructure, 167 PostgreSQL/API) |
| Frontend lint/typecheck/build and Vitest | Passed; 17 tests |
| Shipping admin and storefront mock browser suites | Passed; 5 + 5 scenarios, including vi/en and keyboard confirmation |
| Real browser/API/PostgreSQL/provider matrix | Passed; 6/6 scenarios on newly built API and migration images, runner evidence `Succeeded=true` |
| Clean and upgrade migration | Passed through `20261101113222_AddWorkspaceDeliveryOperations` |
| EF pending-model / catalog drift | Passed |
| NuGet vulnerability gate | Passed after patch; no vulnerable package entries reported |
| Tracked runtime-secret scan | Passed; 1,009 tracked files, no secret values emitted |

The backend count differs from the earlier 764-test working-tree result because
this candidate excludes unrelated uncommitted tests (eight Application and two
API cases). No tracked shipping tests were removed. Independent review found no
Critical, Important or Minor findings in the configuration/build/documentation diff;
the test results above were verified separately by the implementer.

Synthetic rates are published through the admin API only in the runner's disposable
test database. They map every catalog province to a synthetic region, use whole VND
base/step fees and validate seeded SKU weights. They are not a committed business
tariff. The six-case matrix covers both methods × COD/transfer/signed synthetic VNPay,
delivery/payment completion, lookup/PDF and accepted-fee consistency. Local cases
simulate partner outage and assert zero carrier calls; partner creation cost differs
from its quote without changing customer totals. Integration tests also cover valid
quote/backlog processing after intake suspension, cancellation/replay and legacy orders.

## Known limitations retained for the exercise

- Frontend audit is **not green**: `pnpm audit --prod --audit-level=high` reports
  three High, four Moderate and one Low advisory entries in the existing frontend
  dependency graph (Vite, Zod and source-map-js). No production security pass is
  claimed and no audit gate is suppressed. Their dependency remediation is separate
  from shipping configuration; unrelated working frontend changes were preserved.
- Vite reports existing bundle-size warnings. The isolated storefront also emits
  SignalR negotiation 404s; shipping's asserted HTTP/API flow passes independently.
  This does not verify live notifications or a multi-replica backplane.
- No production migration/enablement, hosted CI execution, real provider/VNPay
  certification, production load/soak or registry vulnerability scan was performed.
  Local image identities are not registry release digests.

## Decision

**Accepted for the educational SHIP-14 scope authorized by the owner.** Automated
checks verify synthetic rates, both delivery methods and the committed candidate;
the frontend audit limitations above remain recorded. For later real deployment, the
[production release](production-release.md) and shipping candidate checklist must
be completed afresh with real pricing, deployment identities and responsible owners.
Committed defaults remain off and no user database/configuration is modified.
