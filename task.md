# Production Readiness and Release Gate

> Updated 2026-08-11. This is the active pre-deployment checklist. Detailed PRH-001--009 analysis is retained in Git history and the linked ADRs/runbooks; completed work is summarized here to keep the release decision readable.

## Current decision

**NO-GO for deployment.** Repository hardening is substantially implemented, but the release still needs production-owned evidence: credential rotation, shared multi-replica services, deployed ingress/telemetry checks, load/recovery rehearsals, and formal sign-off.

### Status legend

- **Done (repo):** code, local automation, and/or documentation exists and has been reviewed locally.
- **Open (external):** requires Security, Platform/SRE, cloud/provider access, or an approved staging environment.
- **Blocked (environment):** cannot be run on this workstation until Docker or another named dependency is available.

## Delivered work summary

| PRH | Status | Repository deliverable |
| --- | --- | --- |
| PRH-001 | Done | Baseline/security inventory, value-safe history scan, rotation inventory. |
| PRH-002 | Done (repo); Open (external) | Tracked credentials removed, placeholder-only examples and secret scanner; exposed values still require rotation/revocation. |
| PRH-003 | Done | Google ID-token audience/issuer trust is server-controlled. |
| PRH-004 | Done | Real TOTP enrollment, challenge, recovery, replay protection, and protected persistence. |
| PRH-005 | Done | Email/account-token outbox, verification/reset, refresh rotation/revocation, browser session flows. |
| PRH-006 | Done | Pending/approved/rejected blog-comment moderation with audit data and rate limits. |
| PRH-007 | Done (repo); Open (production scanner) | Validated durable media metadata/S3 boundary and fail-closed NoOp exception policy. |
| PRH-008 | Done | PostgreSQL-side filtering/projection/paging, bounded query tests, and evidence-backed indexes. |
| PRH-009 | Done (local); Open (production) | Regression/migration/backup scripts, runbooks, security/dependency checks. |
| PRH-010 | Done (repo); Open (hosted CI) | Locked builds, CI workflows, artifacts, container smoke, immutable-digest release evidence workflow. |
| PRH-011 | Done (repo guards); Open (rotation/platform) | Production configuration validation, matrix and rotation runbook. |
| PRH-012 | Done (repo claims); Open (scale-out) | PostgreSQL outbox leases, retries/dead letters, cleanup leadership, metrics and operations endpoints. |
| PRH-013 | Done (repo); Open (ingress/platform) | Non-root pinned container, runtime limits, proxy validation, health semantics. |
| PRH-014 | Done (repo); Open (observability) | Telemetry redaction processor, metric/SLI contract, alert runbook. |
| PRH-015 | In progress | Vitest API/session coverage, isolated Playwright storefront smoke, OpenAPI/authorization tests, media-policy tests. |
| PRH-016 | Done (harness); Open (execution) | Safe k6 wrapper/suites, load/fault runbook, evidence template. |
| PRH-017 | Done (materials); Open (rehearsal) | Disaster-recovery and forward-fix runbooks, evidence template, local restore checks. |
| PRH-018 | Done (validator); Open (approval) | Immutable candidate/evidence-manifest validator and release-gate guidance. |

## Active release gates

### PRH-010 - Immutable baseline and CI

- [x] Locked backend/frontend dependencies, build/test/audit/migration/container workflows, SBOM artifacts, and digest-only evidence workflow are in source.
- [ ] **Platform/Repository owner:** run the workflows on the final candidate, protect `main` with required checks/reviews, push one immutable image digest, retain attestations/SBOM, and attach hosted artifacts.

### PRH-011 - Credential and configuration authority

- [x] Production validators reject placeholders/unsafe defaults; configuration matrix and rotation runbook are present.
- [ ] **Security + Platform:** assign owners; rotate/revoke every historically exposed PostgreSQL/admin/JWT/VNPay/MiniLogistics value; prove old values fail; use a secret manager and least-privilege workload identities.
- [ ] **Platform:** provide encrypted, persistent, shared, backed-up Data Protection keys; rehearse JWT/key rotation and record session impact.

### PRH-012 - Multi-replica correctness

- [x] Customer-email and shipment commands use database-time atomic claims, lease-token guarded completion, bounded retry/dead-letter states, stable shipment idempotency, and audited replay paths.
- [ ] **Architecture owner:** select Azure SignalR Service or Redis backplane and add the validated deployment configuration.
- [ ] **Platform:** configure distributed/edge rate limits and run two-replica tests for refresh reuse, SignalR delivery/reconnect, outboxes, webhooks, media, cleanup, worker kill, and rolling restart.

### PRH-013 - Runtime and ingress

- [x] Image hardening, trusted forwarded headers, request limits, liveness/readiness, non-root identity, and development-only local media are enforced in source.
- [ ] **Platform:** verify real ingress TLS/HSTS/CORS/cookies/WebSocket behavior, read-only root filesystem/mounts, CPU/memory/connection budgets, and graceful rolling deployment.

### PRH-014 - Telemetry and alerts

- [x] Application Insights redaction tests cover request/query/header/cookie/token/email/signature/body/exception paths while retaining safe correlation data.
- [ ] **Observability:** configure sampling/retention, dashboards, alerts/escalations, and staging marker searches proving sensitive values never reach sinks; exercise each alert and record acknowledgement/recovery time.

### PRH-015 - Functional, contract, and security regressions

- [x] Vitest covers persisted/expired session state plus 2FA request transport, lifecycle endpoints, coupon conflicts, and comment-moderation acknowledgement (8 tests).
- [x] Isolated Chromium catalog-to-cart smoke, loopback safety checks, API operation/authorization matrix tests, SignalR query-token boundary test, and NoOp scanner fail-closed tests exist in CI/source.
- [ ] Add component coverage for 2FA setup/challenge UI, verification/reset UX, checkout error UI, media upload UX, and moderation UI.
- [ ] Add isolated S3-compatible media E2E, customer/guest checkout, concurrent-stock, payment, loyalty, refresh/logout, admin, shipment/outbox, and cross-replica notification scenarios.
- [ ] Run staging DAST and accessibility/browser-viewport smoke; triage every finding with owner/date.

### PRH-016 - Load, resilience, and soak

- [x] Versioned k6 suites enforce safe targets, immutable candidate identity for non-local runs, explicit write flags, and no raw secret-bearing samples.
- [ ] **SRE + application:** approve SLO/traffic model, generate representative PostgreSQL/S3 data, run ramp/30-minute peak/8-hour soak, reconcile commerce state, execute failure injection and rolling restart, and attach metric/query-plan evidence.
- [ ] **Blocked (environment):** Grafana k6 is not installed on this workstation; no approved isolated staging target or candidate digest is attached.

### PRH-017 - Backup, restore, and incident recovery

- [x] Forward-fix boundaries for post-shipment migrations, disaster-recovery procedures, evidence template, and local synthetic migration/metadata restore checks are documented.
- [ ] **Business + Platform:** approve RPO/RTO; prove encrypted PostgreSQL PITR and object versioning/retention/access logging; restore sampled original and variant media with metadata into isolation; rehearse deletion, corrupted metadata, key loss, credential rotation, migration failure, rollback/forward-fix.
- [ ] Record a non-author operator rehearsal with candidate digest, timestamps, achieved RPO/RTO, discrepancies, and owners.

### PRH-018 - Candidate freeze and go/no-go

- [x] Validator requires a clean checkout, full commit SHA, immutable `image@sha256`, twelve evidence gates, non-placeholder evidence, and critical/high finding policy.
- [ ] Freeze a candidate, deploy it through the one-shot migration job to two-replica staging, complete PRH-010--017 external evidence, update release documents to that exact digest, and obtain Application, QA/Security, Release, and Platform/SRE sign-off.

## Latest verification record

| Date | Check | Result |
| --- | --- | --- |
| 2026-08-11 | `dotnet build WorkspaceEcommerce.slnx --no-restore --configuration Release --disable-build-servers -m:1` | Passed: 0 warnings, 0 errors. |
| 2026-08-11 | Application and Infrastructure test projects | Passed: 304/304 and 186/186. |
| 2026-08-10 | Docker-backed full backend suite after outbox-test correction | Passed: 575/575 (304 Application, 186 Infrastructure, 85 API integration). |
| 2026-08-11 | Storefront Vitest | Passed: 8/8. |
| 2026-08-11 | Storefront typecheck | Passed. |
| 2026-08-11 | Storefront lint/build/E2E safety guard | Passed. |
| 2026-08-11 | Tracked runtime secret scanner | Passed: 827 tracked files; values were not emitted. Scanner was corrected to run on Windows PowerShell 5.1. |
| 2026-08-10 | EF pending-model check | Passed: no pending model changes. Current rerun is blocked because restoring the local `dotnet-ef` tool requires unavailable NuGet network access. |
| 2026-08-11 | Current API integration rerun | **Blocked:** Docker daemon/`npipe://./pipe/docker_engine` unavailable; 71 Testcontainers-backed tests cannot start PostgreSQL. This is an environment failure, not a product test result. |

## Required final verification

Run these on the exact clean candidate before filling the PRH-018 manifest:

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --configuration Release --disable-build-servers -m:1
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --configuration Release --disable-build-servers -m:1
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/WorkspaceEcommerce.Infrastructure --startup-project src/WorkspaceEcommerce.Api --context AppDbContext --configuration Release --no-build
./scripts/scan-tracked-runtime-secrets.ps1
./scripts/verify-prh-009-regressions.ps1
./scripts/verify-prh-009-migrations.ps1
./scripts/verify-prh-009-backup-restore.ps1
./scripts/verify-prh-010-container.ps1

Push-Location frontend
corepack pnpm install --frozen-lockfile
corepack pnpm lint
corepack pnpm test
corepack pnpm typecheck
corepack pnpm build
corepack pnpm test:e2e:safety
Pop-Location
```

Then run the isolated browser E2E, approved PRH-016/017 staging exercises, and the final manifest validator from the exact clean checkout. Do not use a mutable image tag or make a release decision with `-AllowDirtyWorktree`.

## Release evidence locations

- [Production release runbook](docs/runbooks/production-release.md)
- [Configuration matrix](docs/runbooks/configuration-matrix.md)
- [Credential rotation](docs/runbooks/credential-rotation.md)
- [Observability and alerting](docs/runbooks/observability-and-alerting.md)
- [Media scanning policy](docs/runbooks/media-malware-scanning.md)
- [Load/resilience](docs/performance/prh-016-load-resilience-runbook.md)
- [Disaster recovery](docs/runbooks/disaster-recovery.md)
- [PRH-018 candidate gate](docs/reports/prh-018-release-candidate-gate.md)
