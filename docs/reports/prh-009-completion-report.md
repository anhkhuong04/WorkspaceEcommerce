# PRH-009 Completion Report

Status on 2026-08-09: **local release gates passed; production release is blocked pending platform-owned evidence.** This report intentionally distinguishes executable repository evidence from checks that require the deployed topology.

## Local evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Focused critical-path regression gate | Pass | 30 tests: 22 API integration and 8 application tests. Includes concurrent last-unit checkout, coupon use, VNPay duplicate callback, loyalty, shipment outbox, and idempotent webhook coverage. |
| Full backend suite on the PRH-009 candidate | Pass | 535 tests: 296 application, 172 infrastructure, 67 integration. |
| Migration clean-create and shipment-schema upgrade | Pass | `scripts/verify-prh-009-migrations.ps1` reached `20260809151744_OptimizeReadPathIndexes` from both an empty database and `20260802034719_AddShipmentIntegration`. |
| PostgreSQL + media metadata backup/restore | Pass | `scripts/verify-prh-009-backup-restore.ps1` restored a `content.media_assets` sentinel into an isolated PostgreSQL instance. |
| Backend dependency vulnerability audit | Pass | `dotnet list WorkspaceEcommerce.slnx package --vulnerable --include-transitive` reported no known vulnerable packages. |
| Frontend dependency vulnerability audit | Pass after upgrade | `react-router-dom` upgraded to 7.18.2 and Vite to 8.2.1; `pnpm audit --prod --audit-level=high` reported no known vulnerabilities. |
| Frontend typecheck/build | Pass | All 5 workspaces passed. Build emitted only bundle-size warnings for minified chunks: admin about 552 KB and storefront about 797 KB; no release failure. |
| Direct package license review | Pass | Apache-2.0: AWSSDK.S3, Google.Apis.Auth, Magick.NET-Q8-AnyCPU. MIT: Otp.NET, Microsoft.ApplicationInsights.AspNetCore. Production frontend license export still needs to be attached to the release record. |
| Secret hygiene scan | Pass | `scripts/scan-tracked-runtime-secrets.ps1` completed without reporting a tracked runtime secret. Re-run after every release-branch change and attach the result. |

## Production evidence still required (release blockers)

| Gate | Owner | Required evidence |
| --- | --- | --- |
| CORS, forwarded headers, HTTPS/HSTS, health checks | Platform/SRE | Deployed URL probe output and ingress configuration showing explicit origins and trusted proxy IPs. |
| Cluster-wide rate limiting | Platform/SRE | Edge/WAF or distributed limiter configuration and test from two replicas. |
| Telemetry/log redaction | Platform/SRE + application | Application Insights/log-sink query for synthetic sensitive markers returning zero content matches. |
| Multi-instance smoke | Platform/SRE + application | Two-replica refresh rotation, SignalR, media, outbox, and shipment-command results. SignalR additionally needs a shared backplane. |
| Object-store restore and retention | Storage owner | Versioning, lifecycle/retention, sampled object+metadata restore, encryption, and RPO/RTO evidence. |
| Operational alerts | SRE/on-call owner | Alert rule links, named responders, test alert, and incident runbook links. |
| Final full-suite/release artifact | Release manager | Candidate commit/image digest, full backend/frontend counts, final migration result, license export, audit outputs, and approvals. |

See [the production release runbook](../runbooks/production-release.md) for commands, rollback/forward-fix boundaries, and pass criteria.
