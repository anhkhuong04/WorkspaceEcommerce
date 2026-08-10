# Credential Rotation Runbook

## Purpose

This runbook applies when a password, signing key, API key, webhook secret, or provider hash secret has been committed or otherwise exposed. Removing the value from the latest commit is not a rotation: the previous credential must be revoked or replaced in the system that accepts it.

## Scope

- PostgreSQL connection password.
- Built-in admin password.
- JWT signing key.
- VNPay terminal configuration and hash secret.
- MiniLogistics API key and webhook secret.
- SMTP sender credentials.
- S3/object-storage access credentials and bucket-policy access.
- Google OAuth server audience configuration and public client/domain allow-list.
- Data Protection key-ring access and Application Insights connection access.

## Preparation

1. Create replacement values in the appropriate secret manager or provider portal. Use a cryptographically secure generator for passwords, signing keys, API keys, and webhook secrets.
2. Record the owner, environment, replacement time, and revocation time in the deployment/change record. Do not record the value itself.
3. Store replacement values only in the deployment secret manager, the ignored `.env` file for Docker development, or the ignored `appsettings.Local.json` file for direct local development. CI receives only generated synthetic values.
4. Confirm tracked configuration, examples, documentation, and CI logs contain placeholders only by running:

   ```powershell
   ./scripts/scan-tracked-runtime-secrets.ps1
   ```

## Rotation procedure

### PostgreSQL password

1. Change the database user's password using the approved database administration path.
2. Update `POSTGRES_PASSWORD` and/or `ConnectionStrings__DefaultConnection` in the external deployment secret store.
3. Restart or roll out the API, migration, seed, and worker processes using the replacement configuration.
4. Verify database readiness and one authenticated application request.
5. Disable the old database credential. If zero-downtime rotation is required, create a replacement database role, deploy its connection string, then revoke the old role after verification.

### Admin password and JWT signing key

1. Update the external `AdminAuth__Password` and `Jwt__SigningKey` values together with the deployment configuration.
2. Deploy the API.
3. Verify new admin authentication succeeds.
4. Treat all access tokens signed by the old JWT key as invalid after rollout; users must authenticate again.

#### Current JWT rollover policy

The current HMAC JWT validator accepts one signing key at a time. Until a multi-key
`kid` validation design is implemented and rehearsed, a JWT-signing-key rotation is a
planned forced-session-expiry maintenance window, not a zero-downtime key rotation.
The release manager records the window, affected environment, start/end time, and
customer communication decision without recording either key. Do not claim a
zero-downtime rotation based only on refresh-token persistence.

### VNPay and MiniLogistics credentials

1. Rotate the credential in the VNPay or MiniLogistics portal before or during the coordinated deployment window.
2. Update the matching external environment variable:
   - `Payment__VNPay__TmnCode`
   - `Payment__VNPay__HashSecret`
   - `MiniLogistics__ApiKey`
   - `MiniLogistics__WebhookSecret`
3. Deploy the API and verify VNPay callback signature validation, MiniLogistics quote/create access, and signed webhook acceptance in the correct sandbox or production environment.
4. Revoke the old provider credential after the replacement verification succeeds.

### SMTP, object storage, OAuth, Data Protection, and telemetry access

1. Create a replacement secret, workload identity grant, or key-ring access policy in
   the approved platform authority. Do not copy a secret into a ticket or deployment log.
2. Update only the workload that needs it; deploy staging first and verify SMTP sandbox
   delivery, object read/write, OAuth audience validation, key-ring continuity across a
   restart, or telemetry ingestion as applicable.
3. Remove the old credential/grant and prove it no longer authorizes an operation.
4. For Data Protection, do not replace the key ring with an empty directory. Maintain a
   protected backup and confirm all replicas can read the same ring before rolling them.

## Verification and rollback

Run the repository checks after deployment configuration is prepared:

```powershell
dotnet tool restore
dotnet restore WorkspaceEcommerce.slnx --locked-mode
dotnet build WorkspaceEcommerce.slnx --no-restore --disable-build-servers -m:1
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/WorkspaceEcommerce.Infrastructure/WorkspaceEcommerce.Infrastructure.csproj --startup-project src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj --no-build
./scripts/scan-tracked-runtime-secrets.ps1
```

Do not roll back by restoring the exposed credential. If a replacement fails, issue another replacement, update the external secret store, and repeat verification. The value-free authority/owner matrix is maintained in [configuration-matrix.md](configuration-matrix.md).
