# Implementation Plan - Production Readiness Hardening

> Updated on 2026-08-10. PRH-001 through PRH-008 are implemented and locally verified. PRH-009 local gates pass, but production-owned evidence remains open. The active work below is the stabilization gate that must finish before deployment begins; the prior shipment plan remains available in Git history and feature documentation.

## Active Goal

Produce a reproducible release candidate that remains correct under concurrency, multiple API replicas, dependency failures, rolling restarts, and representative traffic before any production deployment is authorized.

The target outcome:

- Google authentication accepts tokens only for server-configured OAuth clients.
- Customer two-factor authentication is a real TOTP challenge flow, not a profile-only toggle.
- No database password or other reusable secret is committed to source control.
- Customer authentication supports verification, recovery, refresh-token rotation, and revocation.
- Public blog comments enter a moderation workflow instead of being published automatically.
- Media uploads are content-validated and stored in durable, multi-instance-compatible object storage.
- List/search endpoints paginate and project in PostgreSQL rather than loading full tables into application memory.
- The existing checkout, VNPay, loyalty, SignalR, and shipment behavior remains regression-safe.
- Background workers claim work exactly once across replicas, recover expired leases, and expose retry/dead-letter state.
- SignalR, rate limiting, Data Protection, media, refresh-token rotation, and health checks operate correctly in the intended multi-instance topology.
- Build/test/security/migration/container checks run in CI on the exact release candidate.
- Staging load, soak, failure-injection, backup/restore, and rolling-restart rehearsals meet approved SLO, RPO, and RTO targets.

## Current Baseline

The following baseline is derived from the current source and the completed shipment report in this file. It must be re-verified before implementation begins; it is not a substitute for a fresh test run.

- Backend targets .NET 10 with ASP.NET Core, EF Core, and PostgreSQL.
- The solution contains the full storefront/admin API surface and 20 EF Core migrations through `20260809151744_OptimizeReadPathIndexes`.
- Core commerce flows are implemented: catalog, cart, checkout, orders, coupons, loyalty, reviews, blogs, VNPay, and MiniLogistics.
- The current verification is `535/535` passing backend tests (`296` Application, `172` Infrastructure, `67` API integration), frontend typecheck/build passing, EF model aligned with the latest migration, and backend/frontend dependency audits reporting no known vulnerability.
- `src` has no explicit `TODO`, `FIXME`, or `NotImplementedException` markers.
- Known production gaps are behavioral and architectural rather than missing controller/service skeletons.

### Confirmed gaps before deployment

1. Repository secret remediation is complete, but the credentials identified by PRH-001 have not been proven rotated/revoked in every maintained environment.
2. `CustomerEmailOutboxWorker` and shipment command processing select due rows without a database claim/lease or `FOR UPDATE SKIP LOCKED`; multiple replicas can process the same work concurrently. Cleanup workers also need an explicit multi-instance coordination policy.
3. SignalR is in-process only and the application rate limiter is per-process. There is no configured shared SignalR backplane or cluster-wide/edge rate-limit enforcement.
4. Production telemetry redaction, trusted proxy/CORS/HSTS behavior, alerts, and health probes have not been verified in the deployed topology.
5. PostgreSQL metadata restore is rehearsed locally, but production object bytes, bucket versioning/retention, encryption, and RPO/RTO recovery evidence are missing.
6. CI currently enforces only tracked-secret scanning; it does not build, test, audit dependencies, validate migrations, build/scan the container, or verify frontend behavior.
7. There is no automated frontend test runner, browser E2E release suite, formal load/soak test, or dependency-failure/rolling-restart rehearsal.
8. The API container has no explicit non-root runtime user, `AllowedHosts` is wildcarded, and production runtime/request limits plus immutable image-digest policy are not yet evidenced.
9. `docs/Deloys.md`, the active tracker, and some PRH-006 through PRH-008 checklist state are stale relative to implemented code and recorded test evidence.

## Scope and Guardrails

- Preserve the existing layered structure: API, Application, Domain, Infrastructure.
- Keep provider-specific implementations behind application abstractions.
- Do not break existing JWT role claims or authenticated customer ownership checks during migration.
- Use additive database migrations first; destructive cleanup happens only after deployed data has been migrated and verified.
- Never log raw tokens, TOTP secrets, recovery codes, password reset tokens, refresh tokens, provider credentials, or uploaded file bodies.
- Store only hashes for single-use tokens and recovery codes. Encrypt long-lived TOTP secrets at rest.
- Use cryptographically secure randomness for every credential or token.
- Every behavior change requires unit/integration tests and updated environment documentation.
- Keep frontend contract changes explicit; do not silently change existing response shapes.

## Priority Roadmap

### P0 - Security blockers

#### PRH-001 - Establish a fresh baseline and security inventory

- [x] Run the full backend suite and record the actual starting result.
- [x] Run frontend typecheck/build and record existing unrelated failures separately.
- [x] Run `dotnet ef migrations has-pending-model-changes`.
- [x] Inventory every configuration key and classify it as public configuration, credential, signing key, or personal data.
- [x] Search the full Git history for committed connection strings, passwords, signing keys, OAuth secrets, VNPay secrets, and MiniLogistics credentials.
- [x] Create a credential-rotation list for any value that has ever been committed; removing text from the latest revision alone is not considered rotation.
- [x] Add a production-readiness test/result section to this file with date, commands, and counts.

##### Execution record - 2026-08-09

All commands below ran from the repository checkout without modifying application source. The only pre-existing worktree changes were five deleted `docs/screenshots/*` files and this plan document.

| Check                     | Command                                                                                                                                                                                                                         | Result                                                                     |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Backend build             | `dotnet build WorkspaceEcommerce.slnx --no-restore --nologo`                                                                                                                                                                    | Passed: 0 errors, 3 warnings.                                              |
| Backend tests             | `dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --nologo`                                                                                                                                                          | Passed: 476/476 (273 Application, 153 Infrastructure, 50 API integration). |
| EF model state            | `dotnet ef migrations has-pending-model-changes --project src\WorkspaceEcommerce.Infrastructure\WorkspaceEcommerce.Infrastructure.csproj --startup-project src\WorkspaceEcommerce.Api\WorkspaceEcommerce.Api.csproj --no-build` | Passed: no pending model changes.                                          |
| Frontend typecheck        | `cd frontend; corepack pnpm typecheck`                                                                                                                                                                                          | Passed for all 5 participating workspace projects.                         |
| Frontend production build | `cd frontend; corepack pnpm build`                                                                                                                                                                                              | Passed for Admin and Storefront.                                           |

Baseline warnings, recorded as pre-existing technical debt rather than failures:

- `CA2024` in `OrderImportFileParser.cs:76`: `StreamReader.EndOfStream` is used in an async method.
- `CS8602` in `AdminBlogServiceTests.cs:130` and `:135`: possible null dereference in test code.
- Vite emitted bundle-size warnings only: Admin JavaScript is 553.99 kB and Storefront JavaScript is 786.20 kB after minification, each above the 500 kB advisory threshold.

##### Configuration inventory

Values were deliberately not copied into this document or command output. Dotted names below are canonical backend keys; `__` names in Docker and `.env` files map one-to-one to the same hierarchical backend keys.

| Classification                                      | Keys                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Sources and handling                                                                                                                                                                                                                                                                 |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Public/runtime configuration                        | `AllowedHosts`; `Logging.LogLevel.Default`; `Logging.LogLevel.Microsoft.AspNetCore`; `Cors.AllowedOrigins[]`; `Storefront.BaseUrl`; `Loyalty.MoneyPerPoint`; `Loyalty.VoucherAmountPerPoint`; `Loyalty.VoucherValidityDays`; `MiniLogistics.BaseUrl`; `MiniLogistics.WebhookToleranceSeconds`; `MiniLogistics.OperationTimeoutSeconds`; `MiniLogistics.MaxRetryAttempts`; `MiniLogistics.RetryBaseDelayMilliseconds`; `MiniLogistics.CircuitBreakerFailureThreshold`; `MiniLogistics.CircuitBreakerBreakSeconds`; `MiniLogistics.CommandWorkerIntervalSeconds`; `Payment.VNPay.PaymentUrl`; `Payment.VNPay.ReturnUrl`; `Payment.VNPay.IpnUrl`; `Payment.VNPay.Version`; `Payment.VNPay.Command`; `Payment.VNPay.Locale`; `Payment.VNPay.CurrCode`; `Jwt.Issuer`; `Jwt.Audience`; `Jwt.AccessTokenMinutes`; `POSTGRES_DB`; `POSTGRES_PORT`; `API_PORT`; `API_HTTPS_PORT`; `ASPNETCORE_ENVIRONMENT`; `ASPNETCORE_URLS`; certificate path | Tracked API settings, `docker-compose.yml`, and `.env.example`; runtime local overrides are ignored.                                                                                                                                                                                 |
| Credentials, signing material, or bearer secrets    | `ConnectionStrings.DefaultConnection`; `POSTGRES_PASSWORD`; `AdminAuth.Password`; `Jwt.SigningKey`; `MiniLogistics.ApiKey`; `MiniLogistics.WebhookSecret`; `Payment.VNPay.HashSecret`; `ASPNETCORE_HTTPS_CERT_PASSWORD`; `APPLICATIONINSIGHTS_CONNECTION_STRING`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | Must be external at runtime. `appsettings.json` and `.env.example` contain placeholder/external-reference values. The tracked development settings and design-time factory have confirmed populated values; see rotation list.                                                       |
| Identifiers and personal/confidential configuration | `AdminAuth.Email`; `POSTGRES_USER`; `Payment.VNPay.TmnCode`; `VITE_GOOGLE_CLIENT_ID`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Admin email is personal data and an authentication identifier. PostgreSQL user and VNPay merchant code are confidential identifiers but not authenticators by themselves. The Google client ID is public by OAuth design, but the backend must not accept it from callers (PRH-003). |
| Browser build-time configuration                    | `VITE_API_BASE_URL`; `VITE_CART_SESSION_ID`; `VITE_GOOGLE_CLIENT_ID`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Storefront `.env` and Admin `.env` are ignored; their `.env.example` files are tracked. The cart session ID is client state, not a secret, and must never grant account authority.                                                                                                   |

Configuration source status:

- Tracked: `src/WorkspaceEcommerce.Api/appsettings.json`, `appsettings.Development.json`, `.env.example`, `docker-compose.yml`, and frontend `.env.example` files.
- Present but correctly untracked and ignored: root `.env`, API `appsettings.Local.json`, Storefront `.env`, and Admin `.env`.
- Not yet configured because the capability does not exist: server-side Google OAuth allowlist, email delivery, Data Protection/key encryption, object storage, and malware scanning. Their configuration design belongs to PRH-003, PRH-004, PRH-005, and PRH-007.

##### Value-safe secret and history scan

`gitleaks` is not installed in the current environment. A value-safe fallback scanner therefore inspected current tracked files and all Git-history text blobs without printing secret values.

- Scope: 73 commits and 957 unique text blobs reachable from all refs.
- Rules: connection-string password, sensitive environment assignment, sensitive JSON assignment, and C# sensitive string assignment.
- Result: 54 metadata-only candidates. Most are test fixtures, documentation, validation code, or placeholder examples. They are not evidence that a runtime credential is safe and must not be broadly allowlisted in the future CI scanner.
- Confirmed rotation scope: the design-time factory contains a reusable PostgreSQL credential in current source and history; `appsettings.Development.json` contains populated values for the connection string, admin password, JWT signing key, and VNPay hash secret. These five values are treated as exposed regardless of whether an environment is currently reachable.
- Git history confirms the design-time factory was introduced in commits `be1c672` and `1b109a5`. Removing it from the latest revision will not remove it from history or invalidate deployed credentials.
- `.env.example` and Docker Compose sensitive entries are placeholders or external references according to value-state checks. Ignored local configuration files were inventoried by key only and their values were not read into output.

##### Credential rotation list

| Scope                                                                                | Owner                           | Status                                              | Required PRH-002 action                                                                                                                  |
| ------------------------------------------------------------------------------------ | ------------------------------- | --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Design-time PostgreSQL credential in `AppDbContextDesignTimeFactory` and Git history | Backend/platform maintainer     | Exposed; rotation pending                           | Rotate the database password in every maintained environment, remove the source fallback, and move EF tooling to external configuration. |
| Development PostgreSQL connection string in tracked `appsettings.Development.json`   | Backend/platform maintainer     | Potentially exposed; rotation pending               | Replace with placeholders/external configuration and rotate its database credential.                                                     |
| Development admin password in tracked `appsettings.Development.json`                 | Backend/platform maintainer     | Potentially exposed; rotation pending               | Remove from tracked config and rotate any environment using it.                                                                          |
| Development JWT signing key in tracked `appsettings.Development.json`                | Backend/platform maintainer     | Potentially exposed; rotation pending               | Remove from tracked config, rotate the signing key, and invalidate tokens signed by the old key as appropriate.                          |
| Development VNPay hash secret in tracked `appsettings.Development.json`              | Payments/integration maintainer | Potentially exposed; rotation pending               | Replace with external sandbox/production configuration and rotate the provider secret.                                                   |
| Test fixtures, documentation, and placeholder examples flagged by heuristic rules    | Test/documentation maintainer   | Not a production credential finding; review pending | Use unmistakably non-routable test values and narrow CI scanner allowlists by exact path/rule only.                                      |

Acceptance criteria:

- [x] Baseline commands, exact project counts, and expected warnings are recorded for repeat execution after restore/install on a clean checkout.
- [x] Every confirmed or potentially reusable committed credential has an owner, exposure status, and rotation action.
- [x] No baseline command failed; all warnings are documented before feature work begins.

#### PRH-002 - Remove tracked credentials and enforce secret hygiene

- [x] Remove the password-bearing fallback from `AppDbContextDesignTimeFactory`.
- [x] Remove all populated credentials from tracked `appsettings.Development.json`, including the connection string, admin password, JWT signing key, and VNPay hash secret.
- [x] Resolve the design-time connection string from, in order, an explicit environment variable or an untracked local configuration source.
- [x] Fail with an actionable error when design-time configuration is absent; never fall back to a credential embedded in code.
- [x] Keep `.env.example` limited to placeholders and safe local examples.
- [x] Verify `.env`, user secrets, certificates, generated upload content, and local settings remain ignored.
- [ ] Rotate every credential in the PRH-001 rotation list and invalidate/redeploy dependent sessions or integrations where required. **External action required; no Compose database was running and provider/secret-manager credentials are outside this repository.**
- [x] Add automated secret scanning to the repository/CI and make new high-confidence findings fail the check.
- [x] Add tests for missing, placeholder, malformed, and valid design-time connection configuration where practical.
- [x] Update the migration/runbook commands so contributors can run EF tooling without copying a real secret into source.

##### Execution record - 2026-08-09

- Replaced populated `appsettings.Development.json` values with non-sensitive logging settings only. Added ignored-local template `src/WorkspaceEcommerce.Api/appsettings.Local.example.json`.
- API configuration loads the ignored local settings file in Development, then reloads environment variables so deployment/test environment values have the highest precedence.
- EF design-time factory now loads API settings plus the optional ignored local file and environment variables; it produces an actionable error when the connection string is missing or a placeholder.
- Docker Compose requires MiniLogistics and VNPay credential variables; it no longer provides tracked defaults. `.env.example`, README, and partner documentation use placeholders only.
- Added `scripts/scan-tracked-runtime-secrets.ps1` and `.github/workflows/secret-hygiene.yml`. The scanner emits path/rule metadata only and fails on high-confidence embedded runtime credentials.
- Added [credential rotation runbook](docs/runbooks/credential-rotation.md) for the database, admin, JWT, VNPay, and MiniLogistics rotation sequence.

Verification:

- `dotnet build WorkspaceEcommerce.slnx --disable-build-servers -m:1`: passed with 0 errors and 0 warnings.
- `dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1`: passed 476/476.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: passed with no pending model changes.
- Supplying `ConnectionStrings__DefaultConnection=CHANGE_ME` to EF tooling failed with the expected actionable configuration error.
- `docker compose --env-file .env.example config --quiet`: passed; structural verification confirmed no default exists for MiniLogistics/VNPay credentials.
- `./scripts/scan-tracked-runtime-secrets.ps1`: passed, scanning 671 tracked files without emitting values.

Acceptance criteria:

- [x] No reusable password, signing key, or provider secret exists in tracked source or example files.
- [x] EF design-time tooling works with external configuration and fails safely without it.
- [ ] The previously exposed password no longer authenticates in any maintained environment. **Pending external rotation and deployment verification.**
- [x] Secret scanning passes on the current revision and is enforced for future changes.

#### PRH-003 - Make Google OAuth trust server-controlled

- [x] Add validated `GoogleAuthOptions` under a server-owned configuration section.
- [x] Support an explicit allowlist of Google client IDs if storefront environments require more than one client.
- [x] Remove `GoogleClientId` from the public login request contract; the request contains only the Google ID token.
- [x] Validate issuer, audience, expiry, signature, subject, and verified-email status using server configuration.
- [x] Reject startup/configuration validation when Google login is enabled but no valid client ID is configured (therefore also in Production).
- [x] Prevent unsafe account linking when the token email is unverified or conflicts with an already-linked Google subject.
- [x] Return a generic unauthorized response without exposing Google library exception details.
- [x] Update frontend environment handling so the frontend client ID is used only to obtain a credential, never to configure backend validation.
- [x] Update API contracts and documentation for the request contract change.

Required tests:

- [x] Valid token for an allowed server audience succeeds.
- [x] A provider-rejected token for another audience is rejected.
- [x] Expired/invalid-issuer provider failures, missing subject, and unverified email are rejected.
- [x] Existing password account can be linked only under the approved rules.
- [x] Existing Google subject logs into the same customer without creating a duplicate.
- [x] Missing server configuration fails startup/config validation in the intended environments.

Acceptance criteria:

- No caller-provided value influences token audience validation.
- Google login and account linking are deterministic, tested, and do not leak provider errors.
- Existing correctly linked Google customers retain access after migration.

##### Execution record - 2026-08-09

- `GoogleAuth:AllowedClientIds[]` is parsed and validated only from server configuration. `GoogleJsonWebSignature` receives that allowlist directly; no public request value participates in audience validation.
- The new `IGoogleJwtValidator` seam keeps the provider implementation behind infrastructure while testing allowed audiences, provider rejection (including issuer/expiry/signature failures), missing claims, and unverified email without recording tokens.
- Linking is allowed only for a verified identity matching an existing password account with no Google subject. A conflicting email/subject pair gets the same generic `Google authentication failed.` response as an invalid token.
- Storefront Google configuration remains a public `VITE_GOOGLE_CLIENT_ID`, only to obtain a browser credential. The backend request contract is now `{ "idToken": "..." }`.

#### PRH-004 - Replace simulated 2FA with complete TOTP authentication

- [x] Write an ADR for TOTP library choice, encryption approach, recovery-code policy, allowed clock drift, and challenge-token lifetime.
- [x] Extend the customer model with a pending setup state; generating a secret does not enable 2FA immediately.
- [x] Generate TOTP secrets with a cryptographically secure RNG and Base32 encoding.
- [x] Encrypt TOTP secrets at rest with a key that is external to the database and repository.
- [x] Add setup-start API returning an `otpauth://` URI and the minimum data needed for frontend QR rendering.
- [x] Add setup-confirm API; enable 2FA only after a valid code proves authenticator enrollment.
- [x] Generate one-time recovery codes, return them once, and persist only their hashes.
- [x] Change password login to return `RequiresTwoFactor` plus a short-lived, single-purpose challenge instead of an access token when 2FA is enabled.
- [x] Apply the same second-factor policy after Google primary authentication when the matched customer has 2FA enabled.
- [x] Add TOTP verification and recovery-code verification endpoints that issue the normal customer token only after success.
- [x] Prevent reuse of a TOTP time step and document/test the replay policy.
- [x] Require a current TOTP/recovery code before disabling 2FA.
- [x] Rate-limit setup and login challenge verification separately from ordinary API traffic.
- [x] Remove the simulated generator and toggle endpoint after frontend migration.
- [x] Ensure logs and API errors never contain secrets, OTP values, recovery codes, or challenge token contents.

Proposed persistence additions:

- `TwoFactorState` or equivalent pending/enabled state.
- Encrypted pending and active TOTP secret fields.
- Last accepted TOTP time step or replay-prevention metadata.
- Hashed recovery-code records with used timestamp.
- Short-lived two-factor challenge records, stored hashed, with expiry and consumed timestamp.

Required tests:

- [x] Setup start does not enable 2FA.
- [x] Correct setup code enables 2FA; incorrect/expired code does not.
- [x] Password and Google login both stop at the challenge when 2FA is enabled.
- [x] Valid TOTP completes login; invalid, expired, replayed, or rate-limited codes fail safely.
- [x] Recovery code works once and cannot be replayed.
- [x] Disable requires second-factor proof and removes/revokes stored 2FA material.
- [x] Encrypted secrets and hashed codes are not returned by queries, DTOs, logs, or API responses.

Acceptance criteria:

- An enabled 2FA flag always corresponds to a confirmed, usable authenticator setup.
- No access token is issued after primary authentication until the second factor succeeds.
- Recovery and disable flows are secure and covered by integration tests.

##### Execution record - 2026-08-09

- [ADR 002](docs/adr/002-customer-totp-authentication.md) records `Otp.NET` 1.4.1, 20-byte Base32 secrets, 30-second periods, ±1 time-step drift, replay prevention, ten one-time recovery codes, 5-minute hashed login challenges, and external key-ring requirements.
- New persistence is additive: encrypted pending/active secrets, expiry, replay metadata, hashed challenge records, and hashed recovery-code records. Recovery-code `used_at` and TOTP time-step metadata are EF concurrency tokens to prevent concurrent replay. The migration explicitly clears the obsolete simulated state so only confirmed authenticators can be enabled; affected users must re-enroll.
- Production fails before startup if `DataProtection:KeyRingPath` is absent. The key ring must be a persistent, access-controlled mount outside the repository and PostgreSQL, shared by API instances.
- APIs: `POST /api/customer/me/2fa/setup`, `.../confirm`, `.../disable`, `/api/customer/auth/2fa/verify`, and `.../recovery`. Storefront now performs the login challenge and exposes enrollment/recovery-code UX; no toggle endpoint remains.
- Test coverage includes fake-clock unit tests and a PostgreSQL-backed API integration flow covering real TOTP setup/verification, recovery consumption/replay, and secure disable.

Verification:

- `dotnet build WorkspaceEcommerce.slnx --disable-build-servers -m:1 --nologo`: passed with 0 warnings and 0 errors.
- `dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1 --nologo`: passed `499/499` (`285` Application, `163` Infrastructure, `51` API integration).
- `dotnet ef migrations has-pending-model-changes --project src\WorkspaceEcommerce.Infrastructure --startup-project src\WorkspaceEcommerce.Api --no-build`: passed with no pending model changes.
- `corepack pnpm --dir frontend typecheck` and `corepack pnpm --dir frontend build`: passed. Vite retains the pre-existing advisory for bundles above 500 kB.
- `./scripts/scan-tracked-runtime-secrets.ps1` and `git diff --check`: passed.

### P1 - Account lifecycle, moderation, and durable media

#### PRH-005 - Complete the customer account lifecycle

- [x] Introduce an email-delivery abstraction and a durable notification outbox so signup/reset requests are not coupled to provider availability.
- [x] Add email-verification tokens with cryptographically random values, stored as hashes with expiry and consumed timestamp.
- [x] Decide and document whether unverified customers may checkout; enforce the decision consistently.
- [x] Add request-verification and confirm-verification endpoints without leaking whether an arbitrary email exists.
- [x] Add forgot-password and reset-password flows with single-use, hashed, expiring tokens.
- [x] Revoke outstanding reset tokens after successful password change/reset.
- [x] Add refresh-token families with rotation, hashed token storage, expiry, revocation reason, and reuse detection.
- [x] Change customer login/2FA completion responses to issue access and refresh credentials using an explicitly documented browser-storage strategy.
- [x] Add refresh and logout endpoints; logout revokes the active refresh-token family.
- [x] Revoke all refresh-token families after password reset and offer/implement revoke-all-sessions after password change.
- [x] Keep access tokens short-lived and retain current role/customer claims for compatibility.
- [x] Define cleanup jobs and retention periods for expired tokens, challenges, recovery data, and login history.
- [x] Update frontend flows for verify email, forgot/reset password, session refresh, logout, and expired-session recovery.

Required tests:

- [x] Verification/reset requests do not reveal account existence.
- [x] Verification and reset tokens expire, are single-use, and are stored only as hashes.
- [x] Refresh rotates tokens and concurrent/replayed old tokens revoke the affected family.
- [x] Logout prevents subsequent refresh.
- [x] Password reset revokes existing sessions.
- [x] Email-provider outage leaves a retryable outbox item and does not corrupt account state.

Acceptance criteria:

- [x] Customers can verify, recover, refresh, and explicitly end sessions.
- [x] Token theft/replay has a defined, tested containment behavior.
- [x] Authentication no longer depends on a successful synchronous email-provider call.

##### Execution record - 2026-08-09

- Added migration `20260809140013_AddCustomerAccountLifecycle` with additive `customer.account_tokens`, `customer.refresh_token_families`, `customer.refresh_tokens`, and `customer.email_outbox` tables. One-time credentials and refresh credentials are stored only as SHA-256 digests; email bodies are Data Protection protected before persistence.
- Added neutral verification/reset request endpoints; successful registration queues verification work without waiting for SMTP. SMTP is required outside Development, while the development metadata-only provider avoids logging raw account links.
- Added rotating refresh families, replay containment, logout/logout-all, session invalidation on password change/reset, and a transaction plus PostgreSQL `FOR UPDATE` around refresh consumption. Login, Google login, and completed TOTP/recovery challenges all now issue the same cookie-backed session shape.
- Added [ADR 003](docs/adr/003-customer-account-lifecycle.md), including the intentional policy that unverified and guest-equivalent customers may checkout, browser storage, deployment, and retention policy. Cleanup workers retain expired credentials/challenges/delivered email for 7 days and login history for 90 days.
- Storefront now has verify-email and reset-password routes, cookie-backed startup/near-expiry refresh, expired-session cleanup, and a "Sign out everywhere" control. The short-lived access token is tab-scoped in `sessionStorage`; the refresh credential is never exposed in JSON.

Verification:

- `dotnet build WorkspaceEcommerce.slnx --no-restore --nologo`: passed, 0 errors and 0 warnings.
- `dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --nologo`: passed 508/508 (291 Application, 163 Infrastructure, 54 API integration).
- `dotnet ef migrations has-pending-model-changes ... --no-build`: passed with no pending model changes.
- `cd frontend; corepack pnpm typecheck`: passed for all participating packages/apps.
- `cd frontend; corepack pnpm build`: passed. Existing Vite bundle-size advisories remain (Admin 554.01 kB; Storefront 799.41 kB).

#### PRH-006 - Add an explicit blog comment moderation workflow

- [x] Replace the comment approval boolean with explicit `Pending`, `Approved`, and `Rejected` status.
- [x] Create all public comments as pending.
- [x] Return a neutral submission acknowledgement; do not return content as publicly visible before approval.
- [x] Ensure storefront blog reads include approved comments only.
- [x] Add admin filters and endpoints to list pending comments and approve/reject them.
- [x] Record moderation timestamp and moderator identity.
- [x] Keep rejection as an auditable soft state; the legacy delete route now rejects rather than removes data.
- [x] Add comment-specific rate limiting and retain maximum body/name/email validation.
- [x] Render comment content as plain text by default; storefront and admin React views do not inject comment HTML.
- [x] Keep moderation independent of any unavailable spam provider; pending review is the safe default.
- [x] Update admin/storefront UI states and counts.

Required tests:

- [x] New comment is pending and absent from storefront reads.
- [x] Admin approval makes it visible exactly once.
- [x] Rejected/deleted comment remains hidden.
- [x] Unauthorized users cannot moderate.
- [x] Oversized, malformed, rate-limited, and script-bearing content is handled safely.

Acceptance criteria:

- No public submission bypasses moderation.
- Moderator actions are attributable and storefront rendering is XSS-safe.

Completion evidence (2026-08-09): application and API integration tests cover Pending/Approved/Rejected visibility, public neutral acknowledgement, admin authorization, moderation attribution, script-bearing plain-text rendering, and oversized comment rejection. The dedicated `blog-comment` fixed-window policy is 3 requests/window in production (500 in Development).

#### PRH-007 - Replace local-only media upload with validated durable storage

- [x] Keep `IMediaStorageService` as the application boundary and add save/delete/read-metadata capabilities.
- [x] Implement S3-compatible object storage and a profile-gated MinIO emulator/bootstrap container for development.
- [x] Keep the local provider as an explicit Development-only option; startup rejects it elsewhere.
- [x] Validate content by decoding images and checking magic bytes, MIME type, and filename extension together.
- [x] Enforce decoded pixel count, dimensions, frame count, file size, and supported-format limits.
- [x] Normalize orientation and strip metadata.
- [x] Re-encode supported formats to canonical WebP rather than serving submitted bytes.
- [x] Explicitly reject GIF and all multi-frame sources.
- [x] Generate 320px, 800px, and 1600px responsive variants when source dimensions need them.
- [x] Generate random object keys and fixed server-controlled WebP content types/content disposition.
- [x] Add the `IMediaMalwareScanner` hook; validation/scanning complete before objects are stored or exposed as available.
- [x] Return CDN/public URLs from trusted `MediaStorage:PublicBaseUrl`, never the incoming Host header.
- [x] Track object key, owner, checksum, size, dimensions, content type, variants, state, and creation time.
- [x] Add conservative delete/replace semantics: immediate deletion respects shared references and the worker removes old unreferenced assets.
- [x] Add hourly unreferenced-upload cleanup with a configurable 24-hour minimum retention window.
- [x] Document the one-time migration path for legacy local files before production cutover.
- [x] Update Docker/environment examples without real storage credentials.

Required tests:

- [x] Valid JPG/PNG/WEBP uploads are decoded, normalized, stored, and readable as canonical WebP.
- [x] Spoofed MIME/extension, malformed image, oversized dimensions, excessive frames, and non-image bodies are rejected.
- [x] Object-store failure leaves only a `Failed`, non-usable asset record.
- [x] Database availability-persistence failure leaves an observable `Pending` cleanup candidate.
- [x] Delete/replace does not remove a still-referenced shared object.
- [x] Production startup cannot silently select local disk storage.

Acceptance criteria:

- Upload behavior is safe against content spoofing and resource-exhaustion inputs.
- Assets survive API restarts and work across multiple API instances.
- Object lifecycle, orphan cleanup, and public URL generation are deterministic.

Completion evidence (2026-08-09): `content.media_assets`/`media_asset_variants`, the S3/local providers, image processor, and cleanup worker were added in `20260809144638_AddBlogCommentModerationAndDurableMedia`. API/infrastructure tests cover canonical upload, spoof rejection, storage failure, database-state failure, shared-reference deletion, and production-local configuration rejection. See `docs/adr/004-durable-media-storage.md` for operating and legacy-file migration instructions.

### P2 - Query scalability and final production gate

#### PRH-008 - Move filtering, projection, and pagination into PostgreSQL

- [x] Inventory every `IQueryable` terminal operation in Application and Infrastructure, especially `ToArray`, `ToList`, `FirstOrDefault`, `Count`, and `Any` in request paths.
- [x] Prioritize customer orders, reviews, blogs, coupons, catalog search, dashboard, and lookup endpoints by expected table growth.
- [x] Replace synchronous EF database calls with cancellation-aware async counterparts.
- [x] Apply filters before `CountAsync`, `Skip`, and `Take`; never paginate after full materialization.
- [x] Project directly to DTO/read models and use `AsNoTracking` for read-only queries.
- [x] Replace per-row/N+1 queries with joins, grouped projections, or bounded batch reads.
- [x] Keep deterministic secondary ordering for stable pagination.
- [x] Clamp all page sizes and validate search/filter inputs.
- [x] Review translated SQL for case-insensitive search, JSON/localized fields, aggregates, and large `Contains` sets.
- [x] Add only evidence-backed indexes; verify plans with representative PostgreSQL data before and after each index.
- [x] Add query-count or SQL-capture tests for endpoints prone to N+1 behavior.
- [x] Add a repeatable representative-data performance script and record latency, rows read, and generated SQL/plan evidence.

Initial high-priority targets:

- `CustomerOrderService.GetOrdersAsync` currently materializes all customer orders before pagination.
- Customer order detail and admin/catalog aggregate mapping paths must be checked for per-row child queries.
- Blog/review/coupon lists and dashboard aggregates must be verified as server-side projections.
- Authentication/profile lookups should use async single-row queries and cancellation tokens.

Acceptance criteria:

- Every paged endpoint performs server-side filtered count and bounded page retrieval.
- No high-traffic read endpoint performs unbounded table materialization or request-thread-blocking EF I/O.
- Representative-data tests show bounded query counts and no material regression.

Completion evidence (2026-08-09): The repeatable terminal inventory found 131 syntactic terminal operations after the migration; request-path EF calls are cancellation-aware and the only remaining correlated `Count` expressions are SQL projections for order item count. Customer/admin orders, admin reviews/coupons, loyalty transactions, catalog pages, dashboard reads, blogs, authentication/profile/lookups, payment, shipment tracking, and shipment webhooks now use database-side filters/projections or bounded batch reads. Legacy non-paged blog/review/comment collections have an explicit 100-item database-side cap.

`20260809151744_OptimizeReadPathIndexes` adds only indexes verified against PostgreSQL 17. On isolated representative data (50,000 orders, 10,000 reviews, 10,000 coupons), executor time changed from customer orders 12.729 to 0.129 ms, admin orders 6.908 to 0.124 ms, reviews 0.329 to 0.122 ms, and coupons 1.495 to 0.074 ms; buffer/plan evidence is recorded in `docs/performance/prh-008-query-plan-runbook.md`. SQL-capture tests constrain customer/admin order pages to two selects and coupon pages to four bounded batch selects. `dotnet build WorkspaceEcommerce.slnx --no-restore`, `dotnet test WorkspaceEcommerce.slnx --no-build --no-restore` (533 passed), and EF pending-model check pass.

#### PRH-009 - Cross-cutting production verification and release gate

- [x] Add regression tests spanning checkout stock locking, coupon usage, VNPay callback idempotency, loyalty earning, and shipment outbox/webhook behavior after auth/storage changes. (`scripts/verify-prh-009-regressions.ps1`; adds concurrent final-unit checkout and duplicate shipment-webhook regressions.)
- [x] Verify all new migrations apply from an empty database and upgrade from the latest existing shipment schema. (`scripts/verify-prh-009-migrations.ps1` verifies clean creation and upgrade from `20260802034719_AddShipmentIntegration`.)
- [x] Verify rollback/forward-fix instructions for each migration that changes credentials, tokens, comments, or media metadata. (Documented in `docs/runbooks/production-release.md`; production uses forward fixes for secrets/security state and forbids destructive media down migration.)
- [x] Run dependency vulnerability audit and license review for new TOTP, image, storage, and email packages. (Backend audit has no known vulnerabilities; frontend high findings fixed by `react-router-dom` 7.18.2 and Vite 8.2.1; direct-package licenses recorded in the completion report.)
- [ ] Verify structured logs and Application Insights telemetry redact all new sensitive fields. **Production blocker:** code redacts the development email recipient and requires telemetry configuration, but sink queries with synthetic sensitive markers require the deployed telemetry workspace.
- [ ] Confirm production CORS origins, proxy/forwarded-header handling, rate-limit partitioning, HTTPS/HSTS, and health checks in the deployed topology. **Production blocker:** configuration and startup guards are implemented; ingress/proxy probe evidence and an edge/distributed rate limiter are owned by Platform/SRE.
- [ ] Run a multi-instance smoke test for refresh-token rotation, SignalR, media access, outbox workers, and shipment commands. **Production blocker:** requires a two-replica environment; SignalR currently has no shared backplane and must not be approved for scale-out until Platform/SRE provisions one.
- [ ] Run backup/restore rehearsal for PostgreSQL and object metadata, and verify object-store retention/versioning policy. **Partially complete:** `scripts/verify-prh-009-backup-restore.ps1` restores PostgreSQL and media metadata; real object-byte restore, versioning, retention, encryption, and RPO/RTO evidence require the production storage owner.
- [x] Update README, `.env.example`, runbooks, API contract documentation, and operational alerts. (`README.md`, `.env.example`, `docs/runbooks/production-release.md`, and `docs/reports/prh-009-completion-report.md` now define the contract, release gate, required alert evidence, and owner.)
- [x] Record final backend/frontend test counts, migration status, security scan, dependency audit, and smoke results in the completion report. (Local results and explicit production blockers are recorded in `docs/reports/prh-009-completion-report.md`.)

Acceptance criteria:

- All automated suites and production smoke flows pass from a clean environment.
- No critical/high secret, dependency, or application-security finding remains without an accepted owner and remediation date.
- Deployment, rollback/forward-fix, credential rotation, backup, and incident-recovery steps are documented and rehearsed.

PRH-009 remains open until its production-owned evidence is attached. The work is decomposed into the pre-deployment stabilization tasks below so code changes, platform changes, and release evidence cannot be confused with each other.

## Pre-Deployment Stabilization Roadmap

### P0 - Pre-deployment correctness and release integrity

Execution order:

| Order | Task | Depends on | Exit unlocks |
| --- | --- | --- | --- |
| 1 | PRH-010 release baseline and CI | PRH-001 through PRH-009 local work | A reproducible candidate for every later test |
| 2 | PRH-011 secrets/configuration closure | PRH-010 | Safe staging credentials and deployment configuration |
| 3 | PRH-012 multi-instance coordination | PRH-010 | Reliable two-replica smoke, rolling restart, and soak |
| 4 | PRH-013 runtime/topology hardening | PRH-010, PRH-011 | Hardened candidate container and staging topology |
| 5 | PRH-014 observability/alerts | PRH-013 | Measurable failure, latency, queue, and redaction evidence |
| 6 | PRH-015 automated functional/security gate | PRH-010 through PRH-014 | Repeatable critical-browser/API and security coverage |
| 7 | PRH-016 load, resilience, and soak | PRH-012 through PRH-015 | Stability evidence at representative traffic |
| 8 | PRH-017 disaster recovery rehearsal | PRH-011, PRH-013, PRH-014 | Approved RPO/RTO and recoverability evidence |
| 9 | PRH-018 release-candidate sign-off | All tasks above | Authorization to begin deployment work |

#### PRH-010 - Establish an immutable release baseline and complete CI gates

- [x] Reconcile `task.md` with implemented PRH-001 through PRH-009 evidence; historical shipment details remain in Git history and linked feature documents. (Completed 2026-08-10; PRH-005 through PRH-008 tracker/checklist state restored from implementation evidence.)
- [x] Update `docs/Deloys.md` so its current-state section no longer says production CORS/Application Insights are missing; label older architectural proposals as historical. (Implemented 2026-08-10.)
- [ ] Resolve all current worktree changes into reviewed commits; require a clean checkout and record the candidate commit SHA.
- [x] Add backend restore/build/test, EF pending-model, migration clean-create/upgrade, secret scan, and NuGet vulnerability audit to CI. (Workflow implementation is present; first hosted run remains required evidence.)
- [x] Add frontend locked install, lint, typecheck, build, production dependency audit, and license/SBOM export to CI. (Workflow implementation is present; first hosted run remains required evidence.)
- [x] Build the API container in CI, run it with non-secret test configuration, probe live/ready endpoints, and scan the final image for critical/high OS/package findings. (The disposable PostgreSQL/container smoke script is implemented; Docker daemon was unavailable for its local run on 2026-08-10.)
- [x] Cache dependencies without caching secrets or generated local settings.
- [x] Upload test counts, migration output, audits, SBOM/license inventory, container digest, and scan output as immutable build artifacts.
- [ ] Protect `main`: required checks, review approval, no direct release from a dirty workstation, and no deployment from an unreviewed commit.
- [x] Make the release workflow consume the already-tested image digest; it must not rebuild different bits during deployment. (The manual evidence workflow rejects mutable tags and only pulls/scans `image@sha256`; the platform push/deployment workflow is still external.)

Acceptance criteria:

- A clean clone can produce the same release artifacts using documented commands and lock files.
- Pull requests cannot merge when build, tests, migration checks, secret scan, dependency audit, frontend checks, or container scan fail.
- The release record maps one commit SHA to one immutable container digest and its complete evidence bundle.

##### Implementation evidence - 2026-08-10

- Added `global.json`, `.node-version`, NuGet `packages.lock.json` files, and the pinned local `dotnet-ef` tool manifest. A local `dotnet restore --locked-mode`, Release build, and EF pending-model check passed.
- Added CI workflows for backend/frontend/container evidence and a separate digest-only release-candidate evidence workflow. Hosted execution, GitHub required-check protection, registry immutability, image push/attestation, and deployment-environment approval must be performed by the repository/platform owner before the acceptance criteria can be marked complete.
- Added `scripts/verify-prh-010-container.ps1`, which migrates an isolated PostgreSQL container then probes `/health/live` and `/health/ready` without publishing a port or using an external secret. Its local execution passed on 2026-08-10 under PRH-013, including the non-root runtime identity; hosted CI evidence is still required for the release candidate.

#### PRH-011 - Close credential rotation and production configuration authority

- [ ] Assign an owner and target environment to every credential in the PRH-001 rotation list: PostgreSQL, admin password, JWT signing key, VNPay, MiniLogistics, SMTP, object storage, Google OAuth configuration, Data Protection key access, and Application Insights.
- [ ] Rotate/revoke every value that was ever committed; verify the old database/provider credential and old JWT key no longer authenticate.
- [ ] Store secrets in the deployment secret manager and expose them only to the workload identities that require them.
- [ ] Define a zero-downtime JWT/key rotation procedure or explicitly accept a forced-session-expiry maintenance window.
- [x] Generate a value-free configuration matrix for Development, CI, Staging, and Production with owner, required/optional status, source, and rotation period. (Implemented in `docs/runbooks/configuration-matrix.md`; named environment owners must still attest each deployment.)
- [x] Validate exact production `AllowedHosts`, CORS origins, storefront/media public URLs, trusted proxy IPs, cookie domain/SameSite/Secure policy, and Google OAuth audiences. (Repository validators now reject wildcard/local host values outside Development; CORS/media/Google validation already fails closed. Actual ingress/proxy/cookie evidence remains a staging/platform gate.)
- [ ] Verify the Data Protection key ring is encrypted, persistent, shared by API replicas, backed up, access-controlled, and survives rolling restart.
- [x] Add startup validation for any production configuration that is still able to silently use an unsafe/default value. (Non-Development startup now rejects unsafe host filtering, non-external Data Protection path, placeholder telemetry configuration, and non-HTTPS storefront URL.)
- [ ] Exercise credential rotation in staging and record the service/session impact without storing secret values in evidence.

Acceptance criteria:

- No known exposed credential remains valid in any maintained environment.
- Staging starts only from the external configuration/secret authority and rejects missing or placeholder production values.
- Credential and key rotation is rehearsed, attributable, and does not require editing tracked files.

#### PRH-012 - Make background processing and realtime behavior safe across replicas

- [ ] Choose and document the production SignalR scale-out mechanism: Azure SignalR Service or Redis backplane. Configure it only through validated server settings.
- [ ] Add a two-replica SignalR integration/smoke test proving clients connected through different replicas receive the same authorized notification.
- [x] Replace read-then-process outbox polling with an atomic database claim protocol, normally `FOR UPDATE SKIP LOCKED` plus persisted `lease_owner`, `lease_expires_at`, status, and attempt metadata. (Implemented in migration `20260810011708_AddOutboxLeaseMetadata`.)
- [x] Commit a claim before external I/O, recover expired leases after worker/process failure, and prevent a stale worker from completing work after its lease is lost. (Lease-token guarded completion/retry/dead-letter updates are persisted before/after external I/O as appropriate.)
- [x] Apply the claim protocol to customer-email and shipment-command outboxes. Define at-least-once semantics and the residual SMTP duplicate window; use stable provider idempotency/message identifiers where supported. (Documented in `docs/runbooks/background-outbox-operations.md`; provider retention must still be verified in staging.)
- [x] Verify shipment create/cancel keeps stable idempotency keys and cannot produce concurrent provider commands for one order/type. (Create uses order code, cancel uses `<order-code>:cancel`, and active commands have a PostgreSQL partial unique index.)
- [x] Coordinate account cleanup and media cleanup across replicas using bounded database claims or an advisory-lock/leader policy; deletion must remain safe if retried. (Both cleanup workers take a PostgreSQL session advisory lock before bounded work.)
- [x] Add queue age, due count, leased count, retry count, dead-letter count, oldest item age, and processing duration metrics. (Repository metrics and a bounded snapshot worker are implemented; dashboard/alert rules remain PRH-014 staging evidence.)
- [x] Define retry ceilings and a terminal/dead-letter state; do not retry permanent validation/authentication/provider conflicts forever. (Email and shipment workers use bounded attempts; permanent shipment failures dead-letter immediately.)
- [x] Provide an audited admin/runbook path to inspect and replay terminal work without editing database rows manually. (Admin outbox endpoints create a new command and retain the original terminal row; the runbook records safe audit fields.)
- [ ] Configure cluster-wide rate limiting at the ingress/WAF or a distributed store; keep application limits as defense in depth and verify trusted-client-IP partitioning.
- [ ] Run two-replica tests for refresh-token rotation/reuse, email outbox, shipment create/cancel, webhook idempotency, media access/cleanup, SignalR, and rolling worker restart.

Acceptance criteria:

- Two replicas never process the same leased row simultaneously; a killed worker's work becomes eligible after the lease expires.
- No test produces duplicate shipment side effects, duplicate loyalty earning, duplicate webhook timeline entries, or unbounded email/shipment retries.
- SignalR and rate-limit behavior no longer depends on which replica receives the request.

##### Implementation evidence - 2026-08-10

- Added durable email and shipment state/lease metadata, database-clock `FOR UPDATE SKIP LOCKED` claim paths, stale-lease guarded finalization, retry ceilings, dead-letter state, and a deterministic migration backfill for existing sent/completed rows.
- Checkout and VNPay callbacks now enqueue shipment work transactionally; only the durable worker calls shipment create/cancel. Shipment provider keys remain stable across retries.
- Added advisory-lock leader policy for cleanup, bounded queue/processing metrics, admin inspect/replay endpoints, and the operator runbook. Release build, targeted domain/application/infrastructure tests, and EF pending-model validation passed locally.
- A Docker-backed `CustomerEmailOutboxLeasingIntegrationTests` run passed on 2026-08-10, proving an active PostgreSQL lease is excluded and reclaimed only after expiry. The required two-replica, killed-worker, SMTP/provider, SignalR scale-out, distributed rate-limit, ingress/WAF, and rolling-restart tests remain unpassed because the corresponding staging/platform topology is not yet attached.

#### PRH-013 - Harden the production runtime, container, and network topology

- [x] Run the API image as an explicit non-root user with minimum filesystem permissions; keep only the Data Protection mount and required temp paths writable. (The final image runs as fixed UID/GID `10001`; the CI smoke asserts the identity and writable media/key paths.)
- [x] Pin runtime/build/tool images and deployment artifacts by reviewed version/digest; document the update cadence instead of relying on mutable tags. (Docker runtime/SDK base images are exact tag-plus-digest references aligned to `global.json`; update guidance is in the Dockerfile.)
- [x] Set exact production `AllowedHosts`; reject wildcard host acceptance outside Development. (Implemented by PRH-011 production validation.)
- [x] Define Kestrel/ingress request-header, request-body, upload, connection, keep-alive, and timeout limits consistent with the media API contract. (Validated `RuntimeLimits` config applies bounded Kestrel and graceful-shutdown values.)
- [x] Confirm forwarded headers are accepted only from immediate trusted proxies and that direct forged headers cannot alter scheme/client IP/rate-limit partition. (No headers are processed without an explicit trusted proxy; configured proxy IPs replace framework defaults.)
- [ ] Verify TLS termination, HTTPS redirect, HSTS, security headers, cookie security, CORS credentials, and websocket upgrade through the real ingress path.
- [ ] Define CPU/memory requests and limits, connection-pool sizing, graceful shutdown duration, deployment surge/unavailable settings, and PostgreSQL maximum-connection budget.
- [x] Separate health semantics: liveness detects a stuck process, readiness covers required local/DB capability, and optional dependency diagnostics expose S3/SMTP/MiniLogistics without causing an avoidable cascading restart. (A dedicated liveness check is separate from PostgreSQL readiness; optional-dependency diagnostics remain a platform dashboard task.)
- [x] Ensure workers stop claiming new work during shutdown and finish or release active leases within the termination grace period. (Workers honor the host cancellation token; PRH-012 leases bound recovery after an abrupt stop.)
- [x] Validate the production filesystem is ephemeral except for explicitly mounted/shared state and that media never falls back to local disk. (Production media validation rejects local provider; only explicit media/Data Protection paths are writable in the image.)

Acceptance criteria:

- The hardened image starts and serves traffic as non-root with a read-only root filesystem policy where the platform supports it.
- Ingress tests prove correct host, proxy, TLS/HSTS, CORS, websocket, request-limit, and health behavior.
- A rolling restart causes no request corruption, lost durable work, local-media dependency, or exhausted database connections.

##### Implementation evidence - 2026-08-10

- Added fixed non-root runtime identity, exact reviewed base-image tag/digest references, initialized writable development volumes, request/runtime limits, strict forwarded-header trust, and liveness/readiness separation.
- Local evidence passed: 12 focused API configuration/liveness tests; `docker compose --env-file .env.example config --quiet`; final and migration image builds; and `scripts/verify-prh-010-container.ps1`, including migration execution, UID `10001`, and live/ready probes.
- Real ingress TLS/HSTS/CORS/websocket behavior, platform resource/connection budgets, read-only-root policy, production mounts, optional dependency diagnostics, and rolling-restart evidence remain staging/platform gates and are deliberately still unchecked.

### P1 - Observability, automated verification, and recovery

#### PRH-014 - Complete production observability, redaction, and operational alerts

- [x] Define service-level indicators for request availability/latency, checkout failures, payment callbacks, refresh-token reuse, queue age/retries, webhook rejection, media failures, and database saturation. (Repository signal/owner contract is in `docs/runbooks/observability-and-alerting.md`; staging thresholds remain pending.)
- [x] Add a telemetry initializer/processor or equivalent allowlist so headers, cookies, authorization, query strings, email addresses, request bodies, tokens, TOTP/recovery codes, connection strings, webhook signatures/bodies, and provider credentials cannot enter logs/custom dimensions. (The Application Insights processor redacts these values before transmission and has focused regression tests.)
- [x] Preserve safe correlation fields: trace ID, event ID, order code/ID, message ID, provider status category, route template, and replica identity. (The redaction boundary strips sensitive values by name/content while retaining safe dimensions.)
- [x] Add structured metrics and traces around checkout transaction/stock lock, coupon reservation, VNPay reconciliation, loyalty earning, outbox claims, shipment provider calls, media storage, and cleanup jobs. (Existing commerce/shipment instrumentation plus PRH-012 outbox metrics are documented; dashboard wiring remains external.)
- [ ] Configure Application Insights sampling and retention so security/audit signals and failed dependencies are not accidentally discarded.
- [ ] Create dashboards for API golden signals, PostgreSQL pool/latency, external dependencies, authentication abuse, and background queues.
- [ ] Create actionable alerts with thresholds, evaluation windows, severity, named owner, escalation route, and linked runbook.
- [ ] Send synthetic secret/token/email/webhook markers through staging and prove telemetry/log searches return zero sensitive-content matches.
- [ ] Trigger test alerts for readiness, elevated 5xx/latency, auth rate limits, email/shipment dead letters, invalid webhook signatures, payment failures, database exhaustion, backup failure, object-store failure, and telemetry ingestion loss.
- [ ] Record alert acknowledgment and recovery time; remove noisy/non-actionable alerts before the soak test.

Acceptance criteria:

- Redaction tests and staging sink queries find no sensitive marker content.
- Every release-critical failure has a dashboard signal, an actionable alert, an owner, and a tested runbook.
- Operators can correlate one customer-safe trace across API, database, outbox, and external provider without logging credentials or payload bodies.

##### Implementation evidence - 2026-08-10

- Registered `SensitiveTelemetryRedactionProcessor` with Application Insights. Focused tests verify that request URLs/query values, authorization/cookie/bearer data, custom credential/email/signature fields, dependency text, trace text, and exception text are redacted while safe operational fields remain.
- Added the repository SLI/metric contract and the staging marker/alert verification procedure in `docs/runbooks/observability-and-alerting.md`.
- Sampling/retention, dashboards, alerts, hosted sink-marker searches, alert exercises, acknowledgements, and final noise triage remain Platform/Observability staging evidence and are deliberately still unchecked.

#### PRH-015 - Add automated browser, contract, and security regression gates

- [ ] Add a frontend unit/component test runner and cover session bootstrap/refresh, 2FA challenge/setup, verification/reset, checkout/coupon errors, comment moderation state, and media upload validation UX.
- [x] Add a locked Vitest/jsdom runner and regression tests for completed, malformed, expired, and profile-updated customer sessions. The remaining UI journeys in the preceding item are still open.
- [ ] Add Playwright or an equivalent browser E2E suite against an isolated PostgreSQL/object-store environment.
- [x] Add an isolated Chromium storefront smoke using a temporary PostgreSQL container, API container, loopback Vite server, deterministic demo seed, and local test-only media filesystem. It refuses non-loopback targets and generates all runtime credentials in memory; S3-compatible object-store coverage remains open.
- [ ] Cover guest and customer checkout, concurrent last-stock checkout, VNPay success/failure/duplicate callback, loyalty earning/redeeming, refresh reuse/logout, Google/2FA boundaries, admin moderation, media variants, SignalR notification, and shipment webhook/outbox flows.
- [x] Make E2E data deterministic and isolated per run; never target production or reuse production credentials.
- [ ] Add OpenAPI/API-contract compatibility checks so frontend clients and partner callbacks fail CI on unreviewed breaking changes.
- [x] Add CI API-contract presence checks for public catalog, customer/admin order, authentication, and VNPay callback operations. A reviewed schema/compatibility baseline is still required before the broader preceding item can close.
- [x] Add an authorization matrix test for anonymous/customer/admin access, ownership boundaries, ID enumeration, and admin-only operations.
- [ ] Decide the production malware-scanning policy. If scanning is required, configure a real scanner/quarantine provider and reject `NoOpMediaMalwareScanner` outside Development; otherwise record the accepted risk and compensating controls.
- [x] Implement a fail-closed NoOp exception policy: every non-Development environment requires a valid, short-lived risk owner/reference/expiry, and the scanner returns unavailable after expiry. A real production scanner or a signed operational exception remains required.
- [ ] Run a staging DAST baseline and focused tests for CORS, CSRF/cookie behavior, rate limits, header spoofing, upload fuzzing, webhook replay/signature, open redirects, and error-detail leakage.
- [ ] Add accessibility smoke for critical storefront/admin paths and a supported browser viewport matrix.
- [ ] Triage every finding by severity, owner, and remediation date; block release on unaccepted critical/high findings.

Acceptance criteria:

- Critical storefront/admin flows have repeatable browser coverage and deterministic cleanup.
- API contract and authorization regressions fail CI before merge.
- No critical/high application-security finding remains without explicit accepted risk, owner, and date.

##### Implementation evidence - 2026-08-10

- Added locked Vitest/jsdom session regression tests and a CI frontend-unit-test step. `corepack pnpm test` passes locally (4 tests).
- Added an isolated Playwright Chromium smoke and CI job. The local runner built/used the candidate API and migration images, ran deterministic demo seeding against a temporary PostgreSQL container, and passed catalog → product variant → cart on 2026-08-10. It cleans its containers/network and writes a non-secret result artifact.
- Added API OpenAPI operation checks, anonymous/customer/admin authorization-matrix coverage, and a SignalR token-transport boundary test. The tests confirm a browser query bearer is accepted only for the notification hub, not ordinary API routes.
- Added a documented, tested fail-closed temporary NoOp media-scanner exception policy. It is not production scanner evidence: a real scanner/quarantine integration or a signed, time-bounded operational exception is still required before release.
- Critical-flow breadth, S3-compatible media E2E, staging DAST, accessibility/browser-matrix coverage, finding triage, and executed hosted CI evidence remain deliberately unchecked.

#### PRH-016 - Prove performance and resilience with representative load and soak tests

- [ ] Define approved SLOs and traffic model before testing. Provisional gate: read endpoints p95 <= 500 ms, application-controlled checkout p95 <= 2 s excluding provider latency, HTTP 5xx < 1%, and zero commerce-integrity violations.
- [ ] Generate representative PostgreSQL data/cardinality and S3 media objects; preserve the data-generation version and query plans with results.
- [ ] Add a repeatable k6/Azure Load Testing script for catalog/search, login/refresh, cart, checkout/coupon, order reads, admin lists, media, SignalR, and shipment callbacks.
- [x] Add a local-safe k6 wrapper and eight versioned suites for public reads, authenticated reads/refresh rotation, privileged lists, public media, SignalR transport, signed test webhook, bounded cart/checkout traffic, and resilience probes. The wrapper requires an immutable digest for non-local runs, blocks unsafe targets/raw secret-bearing samples, and records non-secret metadata; it is static-validated only until an approved load-generator/staging run is attached.
- [ ] Run a ramp test and at least 30 minutes at the approved peak (initial planning target: 100 concurrent virtual users), then an 8-hour lower-volume soak.
- [ ] Capture p50/p95/p99 latency, throughput, errors, timeouts, rows read, query count, DB pool usage, CPU, memory/GC, queue lag, external calls, and object-store latency.
- [ ] Prove stock, coupon usage, payment state, loyalty points, webhook inbox, outbox state, and shipment commands remain internally consistent after load.
- [ ] Inject PostgreSQL connection interruption, S3 latency/failure, SMTP failure, MiniLogistics timeout/429/5xx, telemetry outage, replica kill, and worker kill during active work.
- [ ] Verify backoff includes jitter/bounds, circuit behavior recovers, queues drain after recovery, and no retry storm or cascading restart occurs.
- [ ] Run rolling deployments/restarts during traffic and verify session refresh, SignalR reconnect, media access, worker lease recovery, and health routing.
- [ ] Compare results with the PRH-008 baseline and investigate every material regression before approval.

Acceptance criteria:

- Approved latency/error/resource SLOs hold through peak and soak without unbounded memory, connection, queue, or retry growth.
- Failure injection and rolling restart recover within documented bounds without lost durable work or duplicate commerce side effects.
- Results are reproducible from scripts and linked to the exact candidate commit/image digest.

##### Implementation evidence - 2026-08-10

- Added `scripts/performance/run-prh-016-k6.ps1`, nine JavaScript modules, a load/resilience runbook, and a candidate-specific evidence template. All JavaScript modules passed `node --check` and the PowerShell runner passed AST parsing.
- `k6` is not installed on this workstation and no approved isolated staging target, representative data set, candidate digest, fault-injection authority, or telemetry dashboard has been supplied. Therefore no load, peak, soak, integrity reconciliation, rolling-restart, or recovery result is claimed; those release gates remain unchecked.

#### PRH-017 - Rehearse backup, restore, rollback, and incident recovery

- [ ] Obtain business-approved RPO/RTO; provisional planning target is RPO <= 15 minutes and RTO <= 60 minutes until replaced by an approved value.
- [ ] Enable and verify PostgreSQL automated backups/PITR, encryption, retention, restore identity, and backup-failure alerts.
- [ ] Enable and verify object-store versioning, encryption, lifecycle/retention, deletion protection, and access logging.
- [ ] Restore PostgreSQL plus sampled original/variant media objects into an isolated environment; verify checksums, references, permissions, and application reads.
- [ ] Rehearse recovery from accidental media deletion, corrupted metadata, failed migration, expired Data Protection key access, and revoked external credentials.
- [ ] Verify clean-database migrations and upgrade from the shipment schema again using the exact release image/migration job.
- [x] Document forward-fix, data compatibility, rollback boundary, and the production `Down` prohibition for every post-shipment migration currently in the repository.
- [x] Run the repository regression checks for clean-create/upgrade from `20260802034719_AddShipmentIntegration` and a synthetic PostgreSQL + durable-media metadata restore. The exact immutable release image/migration-job rehearsal remains open.
- [ ] Rehearse deployment rollback and forward-fix without restoring an exposed credential or discarding moderation/token/media audit state.
- [ ] Record start/end timestamps, achieved RPO/RTO, operator, backup/object versions, candidate digest, discrepancies, and follow-up owners.

Acceptance criteria:

- PostgreSQL and object data are recoverable together within approved RPO/RTO and validated by the application.
- Migration/deployment recovery procedures are executable by an operator who did not write the feature.
- Backup, restore, credential, and incident runbooks have test evidence rather than document-only approval.

##### Implementation evidence - 2026-08-10

- Added an operator-oriented disaster-recovery/forward-fix runbook and a candidate-specific rehearsal evidence template. They explicitly require isolated target controls, PostgreSQL/PITR and object-version evidence, shared Data Protection key recovery, application media reads, RPO/RTO measurement, and a non-author recovery operator.
- Updated the production migration table with the outbox-lease migration forward fix and production `Down` boundary. It now covers every post-shipment migration in the current repository.
- `verify-prh-009-migrations.ps1` and `verify-prh-009-backup-restore.ps1` passed locally on 2026-08-10. They are code regression checks only; no managed PostgreSQL PITR, S3 object-byte restoration, approved RPO/RTO, platform alert, credential rotation, or full recovery rehearsal has occurred, so those gates remain unchecked.

### P2 - Staging release candidate and final go/no-go

#### PRH-018 - Freeze and approve the pre-deployment release candidate

- [ ] Freeze feature work; allow only reviewed release-blocker fixes and regenerate all evidence after each candidate change.
- [x] Add a repository release-candidate manifest validator that binds a full checked-out commit SHA to an immutable image digest, requires every evidence gate to pass, rejects placeholder evidence/dirty worktrees, and enforces critical/high finding policy. It is a no-go validator, not staging evidence or deployment authorization.
- [ ] Deploy the immutable candidate digest to a production-like staging topology with at least two API replicas, external PostgreSQL, shared SignalR/rate-limit mechanism, shared Data Protection keys, S3-compatible storage, SMTP sandbox, and provider sandboxes.
- [ ] Apply migrations through the one-shot migration job before API rollout; confirm exactly one migration executor.
- [ ] Run full backend, frontend, E2E, security, migration, two-replica, load/soak, failure-injection, backup/restore, and rollback gates.
- [ ] Verify all operational alerts and dashboards during the staging run and attach telemetry-redaction queries.
- [ ] Resolve or formally accept every finding with severity, owner, justification, and remediation date; critical findings cannot be accepted for initial production release.
- [ ] Update README, environment matrix, API contract, deployment/rollback/rotation/backup/incident runbooks, and completion report to the exact candidate.
- [ ] Record final test counts, SLO results, migration version, audits, SBOM/licenses, container scan/digest, RPO/RTO, alert tests, and known limitations.
- [ ] Obtain application, QA/security, release, and Platform/SRE sign-off.

Final go/no-go criteria:

- Worktree and release branch are clean; required CI checks pass on the signed candidate commit and immutable image digest.
- No open critical/high correctness, security, data-loss, duplicate-side-effect, migration, recovery, or operability defect exists.
- Two-replica peak/soak/rolling-restart and disaster-recovery flows pass within approved SLO/RPO/RTO.
- PRH-002 and all four remaining PRH-009 production evidence items are complete.
- Only after every criterion above is evidenced may deployment planning/execution begin.

##### Implementation evidence - 2026-08-10

- Added `verify-prh-018-release-candidate.ps1`, release-gate guidance, and an intentionally pending manifest schema. The validator requires a clean candidate checkout, a full commit SHA, an `image@sha256` digest, twelve named evidence gates, non-placeholder evidence, and formal critical/high-finding handling.
- The validator parsed successfully, rejected the intentionally pending example, and passed only a temporary local fixture run using `-AllowDirtyWorktree`. That fixture was deleted immediately and is not release evidence. Feature freeze, registry/staging deployment, hosted CI, two-replica topology, load/recovery, telemetry/alert, sign-off, and all actual candidate evidence remain unchecked.

## Proposed API Changes

Final route names may be adjusted to match controller conventions, but the capabilities and trust boundaries are required.

### Google authentication

```http
POST /api/customer/auth/google
```

Request contains `idToken` only. Backend audiences come exclusively from validated server configuration.

### Two-factor authentication

```http
POST /api/customer/me/2fa/setup
POST /api/customer/me/2fa/confirm
POST /api/customer/me/2fa/disable
POST /api/customer/auth/2fa/verify
POST /api/customer/auth/2fa/recovery
```

### Account lifecycle

```http
POST /api/customer/auth/email-verification/request
POST /api/customer/auth/email-verification/confirm
POST /api/customer/auth/password/forgot
POST /api/customer/auth/password/reset
POST /api/customer/auth/refresh
POST /api/customer/auth/logout
POST /api/customer/auth/logout-all
```

### Comment moderation

```http
GET  /api/admin/blog-comments?status=Pending&page=1&pageSize=20
POST /api/admin/blog-comments/{id}/approve
POST /api/admin/blog-comments/{id}/reject
```

## Expected Persistence Changes

Use separate migrations by bounded feature so rollback and code review remain manageable.

1. `HardenGoogleAuthConfiguration` should normally be configuration/contract-only and require no schema migration.
2. `AddCustomerTwoFactorSecurity` for encrypted pending/active secret metadata, recovery-code hashes, replay prevention, and challenges.
3. `AddCustomerAccountTokens` for email verification, password reset, refresh-token families, and revocation state.
4. `AddNotificationOutbox` for durable email delivery.
5. `AddBlogCommentModeration` for explicit status and moderator audit fields; existing approved comments must remain approved during data migration.
6. `AddMediaObjectMetadata` only if lifecycle/reference tracking cannot be represented safely by existing product/banner/blog URLs.
7. Query-performance migrations should contain only indexes supported by captured query plans.

Migration rules:

- Do not combine unrelated features into one migration.
- Provide deterministic backfills for existing rows.
- Do not drop current 2FA columns or comment approval data in the first deployment.
- Verify empty-database creation and upgrade from migration `20260802034719_AddShipmentIntegration`.

## Execution Order and PR Slicing

1. `PRH-001`: baseline, inventory, and documented decisions.
2. `PRH-002`: credential removal, rotation, and secret scanning.
3. `PRH-003`: server-controlled Google audience validation.
4. `PRH-004a`: 2FA domain/persistence and encrypted setup flow.
5. `PRH-004b`: login challenge, recovery, frontend migration, and removal of simulated flow.
6. `PRH-005a`: email outbox, verification, and password reset.
7. `PRH-005b`: refresh rotation, logout/revocation, and frontend session handling.
8. `PRH-006`: comment moderation backend and UI.
9. `PRH-007a`: object storage provider and secure image processing.
10. `PRH-007b`: reference lifecycle, cleanup, migration, and multi-instance verification.
11. `PRH-008`: query work split by module, with evidence per PR.
12. `PRH-009`: final audit, smoke verification, runbooks, and completion report.

`PRH-004` key management is decided in ADR 002; production deployment still requires an externally managed persistent key ring. Do not enable refresh cookies in production until CORS, CSRF, SameSite, proxy, and HTTPS behavior is tested in the real topology. Do not switch media providers until existing asset migration and rollback are rehearsed.

## Verification Matrix

Each implementation PR must run the smallest relevant test set while the final gate runs all commands.

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test tests\WorkspaceEcommerce.Application.Tests\WorkspaceEcommerce.Application.Tests.csproj
dotnet test tests\WorkspaceEcommerce.Infrastructure.Tests\WorkspaceEcommerce.Infrastructure.Tests.csproj
dotnet test tests\WorkspaceEcommerce.Api.IntegrationTests\WorkspaceEcommerce.Api.IntegrationTests.csproj
dotnet ef migrations has-pending-model-changes --project src\WorkspaceEcommerce.Infrastructure --startup-project src\WorkspaceEcommerce.Api
cd frontend
corepack pnpm typecheck
corepack pnpm build
```

Additional required verification:

- Google JWT fixtures/tests for audience and issuer boundaries.
- TOTP tests with a fake `TimeProvider`; tests must not sleep or depend on wall-clock timing.
- Refresh/recovery concurrency and replay tests.
- Image corpus tests containing valid, malformed, spoofed, huge-dimension, and animated samples.
- PostgreSQL-backed query tests and captured SQL/query-plan evidence.
- Multi-instance object-storage and worker smoke tests.
- Repository and Git-history secret scan.

## Definition of Done for Every Task

- Implementation, validation, authorization, error mapping, and cancellation behavior are complete.
- Unit and integration tests cover success, validation, unauthorized, conflict/replay, provider failure, and concurrency where relevant.
- No new secret or personal data appears in logs, snapshots, fixtures, or tracked configuration.
- Database migration and backward-compatible rollout behavior are reviewed.
- Frontend types and flows are updated when the API contract changes.
- Environment examples and runbooks use placeholders only.
- Relevant build/test/typecheck commands pass.
- The task checkbox is marked complete only after verification evidence is appended to this file.

## Active Progress Tracker

- [x] PRH-001 - Baseline and security inventory (completed 2026-08-09; 476/476 backend tests, frontend typecheck/build, EF model check, and value-safe history inventory recorded above)
- [ ] PRH-002 - Repository remediation completed 2026-08-09; external credential rotation and deployment verification pending
- [x] PRH-003 - Server-controlled Google OAuth validation (completed 2026-08-09)
- [x] PRH-004 - Real TOTP two-factor authentication (completed 2026-08-09; existing simulated enrollments are revoked by migration and require re-enrollment)
- [x] PRH-005 - Complete customer account lifecycle (completed 2026-08-09; rotating refresh families, protected email outbox, verification/reset and frontend flows verified)
- [x] PRH-006 - Blog comment moderation (completed 2026-08-09)
- [x] PRH-007 - Durable and validated media storage (completed 2026-08-09)
- [x] PRH-008 - Database-side query optimization (completed 2026-08-09; PostgreSQL query-plan and query-count evidence recorded)
- [ ] PRH-009 - Local release gates completed 2026-08-09; production topology, telemetry, multi-instance, object-store, and credential-rotation evidence pending
- [ ] PRH-010 - Immutable release baseline and complete CI gates
- [ ] PRH-011 - Credential rotation and production configuration authority
- [ ] PRH-012 - Multi-instance background/realtime correctness
- [ ] PRH-013 - Production runtime, container, and topology hardening
- [ ] PRH-014 - Production observability, redaction, and alerts
- [ ] PRH-015 - Browser, contract, and security regression gates
- [ ] PRH-016 - Representative load, resilience, and soak verification
- [ ] PRH-017 - Backup, restore, rollback, and incident-recovery rehearsal
- [ ] PRH-018 - Staging release-candidate freeze and final go/no-go

## Completion Report

### PRH-001 - Completed 2026-08-09

- Fresh baseline recorded: build passed with 0 errors; all 476 backend tests passed; frontend typecheck/build passed; EF reported no pending model changes.
- Existing analyzer/test nullability warnings and frontend bundle-size warnings were recorded as baseline debt.
- Configuration inventory and a value-safe scan of 73 commits/957 unique history blobs were completed without exposing values.
- Five committed runtime credentials/signing materials require rotation under PRH-002: one design-time PostgreSQL credential plus four populated development settings.

### PRH-002 - Repository remediation completed 2026-08-09; external rotation pending

- Removed tracked runtime credentials and provider defaults; added ignored local configuration support, fail-safe EF design-time configuration, placeholder-only examples, and a credential-rotation runbook.
- Added a path/rule-only secret scanner and GitHub Actions enforcement; the scanner passes on the revised tracked tree.
- Build and all 476 backend tests pass. The externally managed PostgreSQL, admin, JWT, VNPay, and MiniLogistics credentials must still be rotated/revoked and verified in their target environments before PRH-002 can be marked fully complete.

Add later dated, evidence-backed results here as tasks are completed. Preserve failed/pre-existing checks separately from regressions introduced by this roadmap.
