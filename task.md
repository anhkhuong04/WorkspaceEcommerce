# Implementation Plan - Production Readiness Hardening

> Updated on 2026-08-09. This is the active plan after completion of the shipment integration roadmap. The completed shipment plan and its verification report are retained below as historical context.

## Active Goal

Move WorkspaceEcommerce from a feature-complete demo/MVP to a production-ready baseline by completing the known hardening work in authentication, secrets, moderation, media storage, and database query performance.

The target outcome:

- Google authentication accepts tokens only for server-configured OAuth clients.
- Customer two-factor authentication is a real TOTP challenge flow, not a profile-only toggle.
- No database password or other reusable secret is committed to source control.
- Customer authentication supports verification, recovery, refresh-token rotation, and revocation.
- Public blog comments enter a moderation workflow instead of being published automatically.
- Media uploads are content-validated and stored in durable, multi-instance-compatible object storage.
- List/search endpoints paginate and project in PostgreSQL rather than loading full tables into application memory.
- The existing checkout, VNPay, loyalty, SignalR, and shipment behavior remains regression-safe.

## Current Baseline

The following baseline is derived from the current source and the completed shipment report in this file. It must be re-verified before implementation begins; it is not a substitute for a fresh test run.

- Backend targets .NET 10 with ASP.NET Core, EF Core, and PostgreSQL.
- The solution contains 98 controller endpoints, 31 EF configurations, and 17 migrations.
- Core commerce flows are implemented: catalog, cart, checkout, orders, coupons, loyalty, reviews, blogs, VNPay, and MiniLogistics.
- The current hardening verification is `499/499` passing backend tests; the earlier shipment baseline was `476/476`.
- `src` has no explicit `TODO`, `FIXME`, or `NotImplementedException` markers.
- Known production gaps are behavioral and architectural rather than missing controller/service skeletons.

### Confirmed gaps

1. `AppDbContextDesignTimeFactory` previously contained a reusable PostgreSQL password fallback in source; repository remediation is complete but external credential rotation remains pending.
2. `StorefrontBlogService` creates public comments with `isApproved: true`.
3. `LocalMediaStorageService` stores files on local disk and trusts declared MIME type plus extension without decoding or signature validation.
4. Customer auth has no refresh-token rotation, logout/revocation, password reset, or complete email-verification workflow.
5. Several query paths use synchronous materialization or paginate after loading all matching records into memory.

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

| Check | Command | Result |
| --- | --- | --- |
| Backend build | `dotnet build WorkspaceEcommerce.slnx --no-restore --nologo` | Passed: 0 errors, 3 warnings. |
| Backend tests | `dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --nologo` | Passed: 476/476 (273 Application, 153 Infrastructure, 50 API integration). |
| EF model state | `dotnet ef migrations has-pending-model-changes --project src\WorkspaceEcommerce.Infrastructure\WorkspaceEcommerce.Infrastructure.csproj --startup-project src\WorkspaceEcommerce.Api\WorkspaceEcommerce.Api.csproj --no-build` | Passed: no pending model changes. |
| Frontend typecheck | `cd frontend; corepack pnpm typecheck` | Passed for all 5 participating workspace projects. |
| Frontend production build | `cd frontend; corepack pnpm build` | Passed for Admin and Storefront. |

Baseline warnings, recorded as pre-existing technical debt rather than failures:

- `CA2024` in `OrderImportFileParser.cs:76`: `StreamReader.EndOfStream` is used in an async method.
- `CS8602` in `AdminBlogServiceTests.cs:130` and `:135`: possible null dereference in test code.
- Vite emitted bundle-size warnings only: Admin JavaScript is 553.99 kB and Storefront JavaScript is 786.20 kB after minification, each above the 500 kB advisory threshold.

##### Configuration inventory

Values were deliberately not copied into this document or command output. Dotted names below are canonical backend keys; `__` names in Docker and `.env` files map one-to-one to the same hierarchical backend keys.

| Classification | Keys | Sources and handling |
| --- | --- | --- |
| Public/runtime configuration | `AllowedHosts`; `Logging.LogLevel.Default`; `Logging.LogLevel.Microsoft.AspNetCore`; `Cors.AllowedOrigins[]`; `Storefront.BaseUrl`; `Loyalty.MoneyPerPoint`; `Loyalty.VoucherAmountPerPoint`; `Loyalty.VoucherValidityDays`; `MiniLogistics.BaseUrl`; `MiniLogistics.WebhookToleranceSeconds`; `MiniLogistics.OperationTimeoutSeconds`; `MiniLogistics.MaxRetryAttempts`; `MiniLogistics.RetryBaseDelayMilliseconds`; `MiniLogistics.CircuitBreakerFailureThreshold`; `MiniLogistics.CircuitBreakerBreakSeconds`; `MiniLogistics.CommandWorkerIntervalSeconds`; `Payment.VNPay.PaymentUrl`; `Payment.VNPay.ReturnUrl`; `Payment.VNPay.IpnUrl`; `Payment.VNPay.Version`; `Payment.VNPay.Command`; `Payment.VNPay.Locale`; `Payment.VNPay.CurrCode`; `Jwt.Issuer`; `Jwt.Audience`; `Jwt.AccessTokenMinutes`; `POSTGRES_DB`; `POSTGRES_PORT`; `API_PORT`; `API_HTTPS_PORT`; `ASPNETCORE_ENVIRONMENT`; `ASPNETCORE_URLS`; certificate path | Tracked API settings, `docker-compose.yml`, and `.env.example`; runtime local overrides are ignored. |
| Credentials, signing material, or bearer secrets | `ConnectionStrings.DefaultConnection`; `POSTGRES_PASSWORD`; `AdminAuth.Password`; `Jwt.SigningKey`; `MiniLogistics.ApiKey`; `MiniLogistics.WebhookSecret`; `Payment.VNPay.HashSecret`; `ASPNETCORE_HTTPS_CERT_PASSWORD`; `APPLICATIONINSIGHTS_CONNECTION_STRING` | Must be external at runtime. `appsettings.json` and `.env.example` contain placeholder/external-reference values. The tracked development settings and design-time factory have confirmed populated values; see rotation list. |
| Identifiers and personal/confidential configuration | `AdminAuth.Email`; `POSTGRES_USER`; `Payment.VNPay.TmnCode`; `VITE_GOOGLE_CLIENT_ID` | Admin email is personal data and an authentication identifier. PostgreSQL user and VNPay merchant code are confidential identifiers but not authenticators by themselves. The Google client ID is public by OAuth design, but the backend must not accept it from callers (PRH-003). |
| Browser build-time configuration | `VITE_API_BASE_URL`; `VITE_CART_SESSION_ID`; `VITE_GOOGLE_CLIENT_ID` | Storefront `.env` and Admin `.env` are ignored; their `.env.example` files are tracked. The cart session ID is client state, not a secret, and must never grant account authority. |

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

| Scope | Owner | Status | Required PRH-002 action |
| --- | --- | --- | --- |
| Design-time PostgreSQL credential in `AppDbContextDesignTimeFactory` and Git history | Backend/platform maintainer | Exposed; rotation pending | Rotate the database password in every maintained environment, remove the source fallback, and move EF tooling to external configuration. |
| Development PostgreSQL connection string in tracked `appsettings.Development.json` | Backend/platform maintainer | Potentially exposed; rotation pending | Replace with placeholders/external configuration and rotate its database credential. |
| Development admin password in tracked `appsettings.Development.json` | Backend/platform maintainer | Potentially exposed; rotation pending | Remove from tracked config and rotate any environment using it. |
| Development JWT signing key in tracked `appsettings.Development.json` | Backend/platform maintainer | Potentially exposed; rotation pending | Remove from tracked config, rotate the signing key, and invalidate tokens signed by the old key as appropriate. |
| Development VNPay hash secret in tracked `appsettings.Development.json` | Payments/integration maintainer | Potentially exposed; rotation pending | Replace with external sandbox/production configuration and rotate the provider secret. |
| Test fixtures, documentation, and placeholder examples flagged by heuristic rules | Test/documentation maintainer | Not a production credential finding; review pending | Use unmistakably non-routable test values and narrow CI scanner allowlists by exact path/rule only. |

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

- [ ] Inventory every `IQueryable` terminal operation in Application and Infrastructure, especially `ToArray`, `ToList`, `FirstOrDefault`, `Count`, and `Any` in request paths.
- [ ] Prioritize customer orders, reviews, blogs, coupons, catalog search, dashboard, and lookup endpoints by expected table growth.
- [ ] Replace synchronous EF database calls with cancellation-aware async counterparts.
- [ ] Apply filters before `CountAsync`, `Skip`, and `Take`; never paginate after full materialization.
- [ ] Project directly to DTO/read models and use `AsNoTracking` for read-only queries.
- [ ] Replace per-row/N+1 queries with joins, grouped projections, or bounded batch reads.
- [ ] Keep deterministic secondary ordering for stable pagination.
- [ ] Clamp all page sizes and validate search/filter inputs.
- [ ] Review translated SQL for case-insensitive search, JSON/localized fields, aggregates, and large `Contains` sets.
- [ ] Add only evidence-backed indexes; verify plans with representative PostgreSQL data before and after each index.
- [ ] Add query-count or SQL-capture tests for endpoints prone to N+1 behavior.
- [ ] Add a repeatable representative-data performance script and record latency, rows read, and generated SQL/plan evidence.

Initial high-priority targets:

- `CustomerOrderService.GetOrdersAsync` currently materializes all customer orders before pagination.
- Customer order detail and admin/catalog aggregate mapping paths must be checked for per-row child queries.
- Blog/review/coupon lists and dashboard aggregates must be verified as server-side projections.
- Authentication/profile lookups should use async single-row queries and cancellation tokens.

Acceptance criteria:

- Every paged endpoint performs server-side filtered count and bounded page retrieval.
- No high-traffic read endpoint performs unbounded table materialization or request-thread-blocking EF I/O.
- Representative-data tests show bounded query counts and no material regression.

#### PRH-009 - Cross-cutting production verification and release gate

- [ ] Add regression tests spanning checkout stock locking, coupon usage, VNPay callback idempotency, loyalty earning, and shipment outbox/webhook behavior after auth/storage changes.
- [ ] Verify all new migrations apply from an empty database and upgrade from the latest existing shipment schema.
- [ ] Verify rollback/forward-fix instructions for each migration that changes credentials, tokens, comments, or media metadata.
- [ ] Run dependency vulnerability audit and license review for new TOTP, image, storage, and email packages.
- [ ] Verify structured logs and Application Insights telemetry redact all new sensitive fields.
- [ ] Confirm production CORS origins, proxy/forwarded-header handling, rate-limit partitioning, HTTPS/HSTS, and health checks in the deployed topology.
- [ ] Run a multi-instance smoke test for refresh-token rotation, SignalR, media access, outbox workers, and shipment commands.
- [ ] Run backup/restore rehearsal for PostgreSQL and object metadata, and verify object-store retention/versioning policy.
- [ ] Update README, `.env.example`, runbooks, API contract documentation, and operational alerts.
- [ ] Record final backend/frontend test counts, migration status, security scan, dependency audit, and smoke results in the completion report.

Acceptance criteria:

- All automated suites and production smoke flows pass from a clean environment.
- No critical/high secret, dependency, or application-security finding remains without an accepted owner and remediation date.
- Deployment, rollback/forward-fix, credential rotation, backup, and incident-recovery steps are documented and rehearsed.

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
- [ ] PRH-005 - Complete customer account lifecycle
- [x] PRH-006 - Blog comment moderation (completed 2026-08-09)
- [x] PRH-007 - Durable and validated media storage (completed 2026-08-09)
- [ ] PRH-008 - Database-side query optimization
- [ ] PRH-009 - Final production verification and release gate

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

---

# Completed Plan Archive - Third-Party Shipment Status Integration

> Completed on 2026-08-02. Retained to preserve design decisions, runtime findings, and the verified shipment baseline.

## Goal

Integrate third-party shipment status into WorkspaceEcommerce cleanly and efficiently.

The target outcome:

- Storefront checkout can calculate shipping fee and create a real shipment through the third-party logistics API.
- Storefront customers and guests can view shipment status and tracking timeline.
- Admin users can inspect shipment status, retry failed shipment creation, and cancel shipment when order cancellation requires it.
- Webhooks update local order/shipment state safely, idempotently, and without duplicate status history.
- The system avoids unnecessary third-party API calls by using webhook-first local snapshots and short-lived live tracking refreshes only when needed.

## Current State

- `IShipmentService` already supports quote, create shipment, and tracking.
- `MiniLogisticsClient` already calls:
  - `POST shipping/quote`
  - `POST shipments`
  - `GET shipments/{trackingCode}`
- `Order` already stores `TrackingCode` and `ShipmentId`.
- COD and ManualBankTransfer checkout currently create shipment after order placement.
- VNPay creates shipment after payment success.
- `POST /api/webhooks/minilogistics` already verifies HMAC signature and maps provider statuses into `OrderStatus`.
- Frontend API types already include `trackingCode` and `shipmentId`, but UI does not yet show provider status/timeline.

## Key Design Decisions

- Keep `OrderStatus` as the business status of the order.
- Add a separate local shipment state model instead of overloading `OrderStatus`.
- Treat webhook as the primary production sync mechanism.
- Use third-party live tracking only for explicit refresh or short-lived cache refresh.
- Do not call third-party tracking on every order page render.
- Use stable idempotency keys for create/cancel operations.
- Use inbox/outbox tables for durable retry and duplicate protection before claiming production readiness.

## Priority Roadmap

### P0 - Required for a correct end-to-end integration

- `[x]` Confirm third-party shipment contract against the real provider runtime.
  - Verify base URL and path prefix.
  - Verify auth header format.
  - Verify request/response casing.
  - Verify create shipment response fields: `shipmentId`, `trackingCode`, `status`, `shippingFeeAmount`.
  - Verify supported provider statuses and map each status deliberately.
  - Verify webhook signature input: `timestamp + "." + raw_body`.
  - Verify webhook payload fields: `eventId`, `event`, `trackingCode`, `externalOrderId`, `status`, `changedAtUtc`.

- `[x]` Add shipment domain/storage model.
  - Add `OrderShipment` entity/table for provider shipment state.
  - Store `OrderId`, provider name, provider shipment id, tracking code, provider status, shipping fee amount, last synced time, last event time.
  - Add unique indexes for `OrderId`, `TrackingCode`, and provider shipment id where applicable.
  - Keep existing `Order.TrackingCode` and `Order.ShipmentId` for backward-compatible read models during transition.

- `[x]` Add webhook inbox for idempotency.
  - Add `ShipmentWebhookEvent` or `ShipmentEventInbox` table.
  - Store `EventId`, event name, tracking code, external order id, provider status, received time, processed time.
  - Add unique index on `EventId`.
  - Skip duplicate events without creating duplicate order status history.

- `[x]` Refactor webhook processing out of the controller.
  - Add application service for shipment webhook handling.
  - Keep controller responsible only for reading raw body, verifying security headers/signature, and returning HTTP response.
  - Move provider-status-to-order-status mapping into a reusable mapper/service.
  - Validate tracking code matches the order when the order already has a tracking code.
  - Persist provider status into `OrderShipment`.

- `[x]` Harden webhook security.
  - Reject missing signature or timestamp.
  - Reject invalid signature.
  - Reject timestamp outside an allowed window, for example 5 minutes.
  - Use constant-time signature comparison.
  - Avoid logging secrets, API keys, Authorization headers, or full sensitive payloads.

- `[x]` Make `Delivered` webhook complete all local side effects.
  - When provider status maps to `OrderStatus.Completed`, trigger the same loyalty point earning behavior used by admin status update.
  - Ensure duplicate delivered webhook does not award points twice.

- `[x]` Add focused backend tests for webhook behavior.
  - Valid signature updates shipment and order state.
  - Invalid signature returns unauthorized.
  - Old/future timestamp is rejected.
  - Duplicate `EventId` is acknowledged without duplicate history.
  - Tracking code mismatch is handled safely.
  - Delivered event completes order and awards loyalty once.

### P1 - Required for operational usability

- `[x]` Add tracking read API.
  - Add customer/guest-safe endpoint using order code plus phone verification.
  - Add authenticated customer endpoint for owned order tracking.
  - Add admin endpoint for order shipment tracking.
  - Return local snapshot first: tracking code, provider status, order status, last synced time, timeline.
  - Optionally refresh from third-party API only when explicitly requested or cache is stale.

- `[x]` Store shipment timeline/events locally.
  - Persist provider timeline entries from webhook and live tracking responses.
  - Deduplicate timeline entries by provider status plus changed timestamp, or provider event id when available.
  - Display local timeline even when the third-party API is temporarily down.

- `[x]` Add admin retry shipment creation.
  - Add endpoint: `POST /api/admin/orders/{id}/shipment/retry`.
  - Allow only when order has no shipment/tracking code or previous create attempt failed.
  - Rebuild shipment request from order and order items.
  - Use idempotency key based on `order.OrderCode`.
  - Save shipment id, tracking code, provider status, and fee.
  - Add tests for success, duplicate prevention, missing order, and provider failure.

- `[x]` Add storefront tracking UI.
  - Guest order lookup shows shipment panel when tracking exists.
  - Customer order detail shows shipment status, tracking code, last update, and timeline.
  - Payment result page links to order tracking after successful payment.
  - Empty/pending shipment states should be clear when shipment creation is delayed or failed.

- `[x]` Add admin shipment UI.
  - Admin order detail shows shipment id, tracking code, provider status, last sync, and timeline.
  - Add retry action for missing/failed shipment.
  - Add refresh tracking action with loading/error states.
  - Add tracking code column or compact shipment indicator in admin order list.

- `[x]` Improve query performance before adding shipment-heavy admin screens.
  - Move admin order search/filter/pagination to database query instead of loading all orders into memory.
  - Include shipment indicators with projected DTOs instead of N+1 lookups.
  - Add indexes for tracking code and provider status.

### P2 - Required for production-style reliability

- `[x]` Add cancel shipment support.
  - Extend `IShipmentService` with `CancelShipmentAsync`.
  - Implement `POST /shipments/{trackingCode}/cancel` in the provider client.
  - Call cancel shipment when customer/admin cancels an order with an existing shipment.
  - Decide and document cancellation policy:
    - Strict: provider cancellation must succeed before local order cancellation.
    - Lenient: local cancellation succeeds and provider cancellation is retried asynchronously.
  - Add tests for provider success, provider conflict, no tracking code, and terminal order states.

- `[x]` Add outbox for durable shipment commands.
  - Create shipment command outbox.
  - Cancel shipment command outbox.
  - Background worker retries transient failures with backoff.
  - Admin retry can enqueue command instead of doing all work synchronously if provider is down.

- `[x]` Add resilient HTTP policy for provider client.
  - Configure per-operation timeout.
  - Retry transient `408`, `429`, and `5xx` responses.
  - Respect `Retry-After` for rate limit responses.
  - Add circuit breaker or short failure cache if provider is unavailable.

- `[x]` Add contract and smoke testing.
  - Add a repeatable smoke script for real provider integration:
    1. Quote shipping fee through E-commerce API.
    2. Checkout COD order.
    3. Assert tracking code is saved locally.
    4. Query provider tracking endpoint.
    5. Simulate or receive delivered webhook.
    6. Assert order is completed and shipment timeline is visible.
  - Add contract tests for provider DTO shape using sample JSON fixtures.

- `[x]` Add observability.
  - Log correlation fields: order code, tracking code, provider shipment id, event id, idempotency key.
  - Add metrics/counts for quote failures, create shipment failures, webhook rejects, duplicate webhooks, tracking refresh failures.
  - Keep secret redaction explicit.

## Target Data Model

### `OrderShipment`

- `Id`
- `OrderId`
- `Provider`
- `ProviderShipmentId`
- `TrackingCode`
- `ProviderStatus`
- `ShippingFeeAmount`
- `Currency`
- `LastSyncedAtUtc`
- `LastEventAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### `ShipmentTimelineEntry`

- `Id`
- `OrderShipmentId`
- `ProviderStatus`
- `Note`
- `ChangedAtUtc`
- `Source`
- `ProviderEventId`
- `CreatedAtUtc`

### `ShipmentEventInbox`

- `EventId`
- `Event`
- `TrackingCode`
- `ExternalOrderId`
- `ProviderStatus`
- `ReceivedAtUtc`
- `ProcessedAtUtc`
- `ProcessingError`

## Target API Surface

### Storefront / Customer

```http
GET /api/orders/lookup?orderCode={orderCode}&phone={phone}
GET /api/orders/lookup/tracking?orderCode={orderCode}&phone={phone}
GET /api/customer/orders/{id}/tracking
```

### Admin

```http
GET /api/admin/orders/{id}/shipment
POST /api/admin/orders/{id}/shipment/refresh
POST /api/admin/orders/{id}/shipment/retry
POST /api/admin/orders/{id}/shipment/cancel
```

### Webhook

```http
POST /api/webhooks/minilogistics
```

## Acceptance Criteria

- Shipping quote still works from checkout.
- COD checkout creates or enqueues shipment creation and persists tracking data.
- VNPay success creates or enqueues shipment creation exactly once.
- Guest lookup and customer order detail show tracking code and shipment timeline.
- Admin order detail can inspect shipment state and retry missing shipment creation.
- Provider webhook updates local shipment provider status.
- Provider webhook updates local order business status through the approved mapping.
- Duplicate webhook does not create duplicate status history or duplicate loyalty points.
- Invalid webhook signatures and stale timestamps are rejected.
- Tracking pages do not call provider API on every render.
- Provider outage does not break order lookup; local snapshot remains readable.
- Integration smoke script passes against the configured third-party API.

## Suggested Execution Order

1. Contract verification and status mapping.
2. Shipment persistence model and migrations.
3. Webhook inbox and webhook service refactor.
4. Webhook security hardening and tests.
5. Tracking read API and local timeline model.
6. Admin retry shipment creation.
7. Storefront/customer tracking UI.
8. Admin shipment UI.
9. Cancel shipment support.
10. Outbox/background retry.
11. HTTP resilience policies.
12. Contract/smoke tests and final verification.

## Local Runbook

### E-commerce API

```powershell
docker compose up -d postgres
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm seed-demo
docker compose up -d --build api
```

### Frontend

```powershell
cd frontend
corepack pnpm dev:storefront
corepack pnpm dev:admin
```

### MiniLogistics / Third-Party Provider Assumptions

When E-commerce API runs on host:

```env
MiniLogistics__BaseUrl=http://localhost:5221/api/v1/partner
MiniLogistics__ApiKey=<partner-api-key>
MiniLogistics__WebhookSecret=<same-secret-configured-in-provider>
```

When E-commerce API runs in Docker and provider runs on host:

```env
MiniLogistics__BaseUrl=http://host.docker.internal:5221/api/v1/partner
MiniLogistics__ApiKey=<partner-api-key>
MiniLogistics__WebhookSecret=<same-secret-configured-in-provider>
```

Webhook URL registered in provider:

```text
http://host.docker.internal:5080/api/webhooks/minilogistics
```

## Verification Commands

```powershell
dotnet build WorkspaceEcommerce.slnx
dotnet test tests\WorkspaceEcommerce.Application.Tests\WorkspaceEcommerce.Application.Tests.csproj
dotnet test tests\WorkspaceEcommerce.Infrastructure.Tests\WorkspaceEcommerce.Infrastructure.Tests.csproj
dotnet test tests\WorkspaceEcommerce.Api.IntegrationTests\WorkspaceEcommerce.Api.IntegrationTests.csproj
cd frontend
corepack pnpm typecheck
corepack pnpm build
```

## Completion Report - 2026-08-02

### Status

Implementation and real-runtime smoke verification are complete. All P0, P1, and P2 tasks in this plan are delivered.

### Delivered

- Added `OrderShipment`, local timeline, webhook inbox, and create/cancel command outbox models with EF Core configuration, indexes, and migration `20260802034719_AddShipmentIntegration`.
- Refactored webhook handling into the application layer with HMAC verification, timestamp tolerance, constant-time signature comparison, event idempotency, out-of-order protection, explicit provider status mapping, and one-time loyalty earning on delivery.
- Added guest, customer-owned, and admin shipment tracking APIs backed by local snapshots. Provider tracking is called only by explicit admin refresh.
- Added admin retry, refresh, and cancellation operations. Shipment creation uses `order.OrderCode` as the stable idempotency key.
- Implemented the lenient cancellation policy: local order cancellation succeeds, while provider cancellation is persisted to the outbox and retried asynchronously when the provider is temporarily unavailable. Non-transient `4xx` failures are not retried indefinitely.
- Added provider timeout, transient retry for `408`/`429`/`5xx`, `Retry-After` support, and a shared short circuit breaker.
- Added storefront/customer tracking panels and admin shipment controls/list indicators.
- Moved admin order filtering, searching, counting, and pagination into database queries and projected shipment indicators without per-order provider calls.
- Added structured shipment metrics and correlation-safe logs without secrets or full provider payloads.
- Added contract fixtures, focused application/infrastructure/API integration tests, and `scripts/test-shipment-integration.ps1`.

### Runtime Contract Findings

- Verified the provider base path `/api/v1/partner`, Bearer authentication, camel-case JSON, create/track/cancel routes, response fields, status set, and webhook signature input against the adjacent MiniLogistics runtime and source.
- The Sandbox runtime requires the `ml_test_` API key prefix. The previous `ml_demo_` default was rejected. PRH-002 later removed tracked credential defaults; configure a provider-issued key externally.
- The provider route classifier accepts canonical province names such as `Ho Chi Minh` and normalizes official Vietnamese administrative prefixes/diacritics. The smoke script now uses a supported canonical value.
- Fixed `GetShippingQuoteResponse` from a property-less primary-constructor class to a record after the real runtime exposed an empty `{}` quote response.

### Real Runtime Smoke Result

Executed against the local MiniLogistics Sandbox runtime and the E-commerce API with PostgreSQL:

- Quote amount: `248000 VND`.
- COD checkout order: `ORD-20260802-E577D734`.
- Provider tracking code persisted locally: `ML202608020440381331`.
- Provider tracking endpoint returned a valid shipment state.
- Signed `Delivered` webhook was accepted and changed the local shipment status to `Delivered`.
- Local tracking timeline contained two entries and the order reached `Completed`.

### Verification

- `dotnet test WorkspaceEcommerce.slnx --no-restore`: passed `476/476` tests (`273` application, `153` infrastructure, `50` API integration).
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- `corepack pnpm typecheck`: passed all frontend workspaces.
- `corepack pnpm build`: passed admin and storefront production builds.
- Shipment component lint: passed.
- Full storefront lint still reports eight pre-existing errors in unrelated auth/search/product/checkout files; no shipment integration lint errors remain.
- Dependency audit: no known vulnerable packages after pinning `Microsoft.OpenApi` to patched `2.11.0` and updating `Microsoft.AspNetCore.OpenApi` to `10.0.10`.
