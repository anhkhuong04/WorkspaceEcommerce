# Production Release Runbook

## Scope and release decision

This is the operational gate for PRH-009. A release manager owns the final decision; the application team supplies the local evidence and the platform/SRE owner supplies deployed-topology evidence. A missing evidence item is a **no-go**, not an implicit pass.

Never put passwords, JWTs, refresh cookies, TOTP recovery codes, OAuth ID tokens, webhook bodies, or object-store credentials in tickets, logs, dashboards, or this document. Rotate an exposed value through the [credential rotation runbook](credential-rotation.md).

## Pre-deploy local evidence

Build the exact candidate once, then run the commands below from the repository root:

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --disable-build-servers -m:1
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1
./scripts/verify-prh-009-regressions.ps1
./scripts/verify-prh-009-migrations.ps1
./scripts/verify-prh-009-backup-restore.ps1
./scripts/scan-tracked-runtime-secrets.ps1
dotnet list WorkspaceEcommerce.slnx package --vulnerable --include-transitive --format json | Set-Content artifacts/nuget-vulnerabilities.json
./scripts/assert-no-nuget-vulnerabilities.ps1 -ReportPath artifacts/nuget-vulnerabilities.json
Push-Location frontend
corepack pnpm audit --prod --audit-level=high
New-Item -ItemType Directory -Force ../artifacts | Out-Null
corepack pnpm licenses list --prod --json | Set-Content ../artifacts/frontend-production-licenses.json
corepack pnpm typecheck
corepack pnpm build
Pop-Location
docker build --file src/WorkspaceEcommerce.Api/Dockerfile --target final --tag workspace-ecommerce-api:ci .
docker build --file src/WorkspaceEcommerce.Api/Dockerfile --target migrate --tag workspace-ecommerce-api-migrate:ci .
./scripts/verify-prh-010-container.ps1
```

The focused regression gate covers two concurrent checkout attempts for the final stock unit, coupon redemption/use limits, VNPay duplicate IPN processing, loyalty earning/redemption, shipment command outbox behavior, and signed/idempotent shipment webhooks. The migration script validates both an empty database and an upgrade from `20260802034719_AddShipmentIntegration`. The backup script restores a real `content.media_assets` metadata sentinel into a separate PostgreSQL instance. Its backup is synthetic-only and removed by default.

`Continuous integration` publishes the corresponding backend, frontend and container
evidence artifacts for each pull request/main commit. It builds a development-only
container candidate, runs the migration image against an isolated PostgreSQL container,
then probes `/health/live` and `/health/ready`. A registry image digest is only release
evidence after the release workflow pushes that exact CI-tested candidate; local image
IDs and mutable tags are not deployment identities.

Use the manual `Release candidate evidence` workflow only with a fully-qualified
`image@sha256:...` reference. It deliberately rejects tags, pulls/scans that exact
digest, and never calls `docker build`. The platform release workflow must push the
candidate once, retain its digest/SBOM, and pass the same digest to deployment; it must
not rebuild the application in a deployment environment.

The license review completed for direct packages introduced by PRH-003 through PRH-007: `AWSSDK.S3`, `Google.Apis.Auth`, and `Magick.NET-Q8-AnyCPU` are Apache-2.0; `Otp.NET` and `Microsoft.ApplicationInsights.AspNetCore` are MIT. `Otp.NET` was checked from its package `LICENSE.txt`. The production frontend license export is retained as a release artifact and must be reviewed for policy exceptions before approval. The account email sender uses .NET's built-in SMTP client; no third-party email package was added.

## Migration deployment and forward-fix

Take and validate a PostgreSQL backup before deploying a migration. Deploy application code that reads both the old and new shape where that is possible, run migrations once using the migration job, then roll out API and workers. Do not run `dotnet ef database update` concurrently from every API replica.

| Migration | Operational recovery | Do not do |
| --- | --- | --- |
| `20260809132326_AddCustomerTwoFactorAuthentication` | If the new flow fails, disable 2FA enrollment, retain the protected security state, repair configuration/key-ring access, then ask affected customers to enroll again. Rotate the Data Protection key ring only through the platform procedure. | Do not delete or expose TOTP secrets/recovery-code hashes, and do not replace the key ring with an empty directory. |
| `20260809140013_AddCustomerAccountLifecycle` | Forward-fix by revoking affected refresh-token families, invalidating pending account tokens, correcting the deployment, and reissuing verification/reset emails. | Do not restore plaintext tokens or roll back to a build that cannot understand revocation state. |
| `20260809144638_AddBlogCommentModerationAndDurableMedia` | Forward-fix comment status with a reviewed administrative action; for media, mark unavailable metadata, restore the object from versioned storage, then re-publish only after checksum/access checks. | Do not blindly run the destructive down migration in production: it drops durable-media metadata and discards moderation audit columns. |
| `20260809151744_OptimizeReadPathIndexes` | Index rollback is isolated: remove only the identified expensive/new index in a scheduled forward-fix after comparing PostgreSQL plans. | Do not combine its remediation with security/data migrations. |
| `20260810011708_AddOutboxLeaseMetadata` | Pause affected workers only when needed, preserve completed/dead-letter rows, allow expired leases to recover, then forward-fix the worker/configuration issue and replay via the audited operations endpoint. | Do not run `Down` in production: it discards lease/dead-letter status used to prevent stale workers and duplicate provider work. |

The rollback of application artifacts never restores an exposed credential. Use the credential-rotation runbook and a forward fix instead. Record the migration name, backup identifier, operator, timestamp, deployment artifact digest, and validation result in the completion report.

## Required production configuration and topology verification

Before exposing traffic, the platform owner must verify these with the actual deployment URL and store the output/links in the completion report:

1. Set exact HTTPS frontend origins in `Cors__AllowedOrigins__<n>`. An allowed origin must receive one concrete `Access-Control-Allow-Origin` value and `Access-Control-Allow-Credentials: true`; an unlisted origin must receive neither. Wildcards are forbidden when cookies are enabled.
2. Set `ForwardedHeaders__KnownProxies__<n>` to the immediate ingress/proxy IPs only. Confirm the trusted proxy supplies `X-Forwarded-For` and `X-Forwarded-Proto`; confirm a direct request cannot forge client IP or HTTPS by adding those headers. The application uses `RemoteIpAddress` only after this trusted middleware, so raw forwarding headers never choose a rate-limit partition.
3. Confirm production startup rejects absent CORS origins, Data Protection key-ring path, Application Insights connection string, SMTP configuration, or S3 media configuration. Confirm the shared key ring is writable by the application identity and unavailable to other tenants.
4. Confirm HTTP redirects to HTTPS after forwarded headers are processed, HSTS is present only on HTTPS production responses, and `/health/live` is live-process only while `/health/ready` includes PostgreSQL readiness. Put liveness and readiness on the correct load-balancer probes and restrict public access as required by the platform.
5. Rate limits in this application are per process. For more than one API replica, configure and test an edge/WAF or distributed limiter with equivalent auth, 2FA, comment, transaction, catalog, and default partitions; the per-process limiter alone is not a cluster-wide control.
6. The candidate image runs as fixed UID `10001`; CI verifies that identity and its media/Data Protection write paths. Keep the workload non-root, retain `no-new-privileges`/dropped capabilities (or platform equivalents), set resource limits, and provide only the required persistent writable mounts. The repository's compose file is a development topology, not a production deployment manifest.

## Telemetry and log redaction verification

Production startup requires an Application Insights connection string. Keep automatic request/dependency telemetry, but do not enable HTTP request-body collection or unrestricted header collection. Explicitly exclude `Authorization`, `Cookie`, `Set-Cookie`, client secrets, database connection strings, API keys, webhook signatures/bodies, TOTP values, recovery codes, email recipients, and token query parameters from collectors and log processors.

Perform a deployment smoke using synthetic identifiers only. Search the structured-log sink and Application Insights for the synthetic token marker, password marker, recipient, and webhook body marker; the search must return zero content matches. It is acceptable to find a trace ID, order code, status, exception type, or hashed/non-sensitive correlation value. Preserve the query/link and time window as evidence.

## Multi-instance and external-service smoke

Run this only in a production-like environment with at least two API replicas. It is not satisfied by two browser tabs pointed at one container.

1. Sign in on replica A, rotate a refresh cookie, retry the old cookie against replica B, and verify the old family is rejected and the new cookie works on both replicas.
2. Connect two authenticated SignalR clients through different replicas; publish a notification and verify both receive it. The current in-process SignalR registration has no shared backplane, so this test is **blocked** until the platform supplies/configures an Azure SignalR or Redis backplane (or an equivalent explicitly approved architecture).
3. Upload an image through replica A; read it and its variants through replica B; confirm S3 metadata and object access are identical and no local-disk URL is emitted.
4. Create/retry/cancel shipment commands from different replicas; verify one stable provider idempotency key, a single outbox completion, and a single signed webhook timeline entry. Verify a worker hand-off does not duplicate a provider command.

Because the shared SignalR backplane and the deployed two-replica environment are not part of this repository, the release remains blocked until the platform/SRE owner attaches this smoke evidence.

## Backup, recovery, retention, and alerts

The automated rehearsal covers PostgreSQL schema/data and durable-media metadata; it cannot restore S3 object bytes without a real object-store account. The storage owner must demonstrate all of the following for the production bucket before go-live:

1. Versioning is enabled, retention/lifecycle policy is documented, accidental deletes can be restored within the agreed RPO/RTO, and encryption/key ownership is recorded.
2. A backup restore into an isolated account/database restores both a sampled media object and its `content.media_assets`/variant metadata; checksum and public-access policy match the source.
3. Restore credentials are least-privilege, the backup is encrypted, and the backup job/retention alert is monitored.

Configure actionable alerts with named responders for: readiness failure, sustained 5xx/latency, authentication/2FA rate-limit spikes, failed customer-email outbox retries, shipment command outbox retries/dead letters, invalid webhook signatures, VNPay callback failures, database connection exhaustion, backup job/restore failure, object-store access failures, and Application Insights ingestion failure. Link the alert routes and an incident ticket/on-call rotation in the completion report.

## Final go/no-go checklist

- All local commands pass on the candidate artifact.
- No critical/high secret, dependency, or application-security finding exists without a named owner, accepted risk, and remediation date.
- The release manager has migration, backup, deployment, and forward-fix evidence.
- The platform/SRE owner has CORS, proxy, HTTPS/HSTS, health, rate-limit, telemetry-redaction, object-store, alerts, and two-replica smoke evidence.
- The completion report is complete and signed by the application, release, and platform/SRE owners.
