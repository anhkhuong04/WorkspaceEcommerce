# Disaster recovery, restore, and forward-fix runbook

## Scope and authority

This is the operational procedure for PRH-017. It prepares a repeatable recovery
rehearsal; it does not authorize a production restore, credential change, database
deletion, object deletion, or rollback. A platform/SRE owner controls the recovery
environment and credentials, while the release manager owns the final go/no-go
decision.

Until business approval replaces them, the planning targets are **RPO <= 15 minutes**
and **RTO <= 60 minutes**. Record the approved targets, actual timestamps, and any
exception in the [PRH-017 evidence template](../reports/prh-017-disaster-recovery-evidence-template.md).
A document-only review is not a pass: an operator who did not write the feature must
run the rehearsal in an isolated environment.

Never put database passwords, Data Protection keys, JWTs, refresh tokens, TOTP
secrets/recovery codes, SMTP credentials, S3 credentials, webhook bodies, or customer
data in a command line, log, artifact, ticket, or this document. If a secret is exposed,
stop the rehearsal and follow the [credential rotation runbook](credential-rotation.md).

## Recovery prerequisites

Before a rehearsal, record the exact image digest, Git commit, migration version, source
backup/PITR point, source object versions, and intended recovery point. Confirm all of
the following with the platform owner:

| Component | Required control/evidence |
| --- | --- |
| PostgreSQL | Automated backups/PITR cadence meets RPO, backup encryption and retention are enabled, an isolated restore identity exists, and backup/restore-failure alerts route to an owner. |
| Object storage | Versioning, encryption, retention/lifecycle, accidental-delete protection, least-privilege restore access, and access logging are enabled. Original and variant media object versions can be identified. |
| Data Protection | The shared key-ring backup/restore path is available only to the intended application identity; recovery will not replace it with an empty ring. |
| Runtime/deployment | The exact candidate image digest, migration job, configuration source, health probes, and rollback/forward-fix procedure are available. |
| Isolation | Restore account/database/bucket/network cannot send mail, payments, provider commands, webhooks, or public media traffic to production. |
| Evidence | An incident/rehearsal ticket, named incident commander, timekeeper, app owner, storage owner, DB owner, and stop/rollback plan are present. |

The existing local `scripts/verify-prh-009-backup-restore.ps1` exercise restores a
synthetic PostgreSQL and `content.media_assets` metadata sentinel. It does **not**
restore production object bytes, validate provider backup/PITR configuration, or prove
RPO/RTO. Run it as a code regression check, not as a substitute for this rehearsal.

## Rehearsal procedure

### 1. Freeze the recovery record and establish a safe target

1. Open the evidence record and record start time in UTC, candidate image digest, Git
   commit, source system, intended recovery point, and approved RPO/RTO.
2. Verify the target account/database/bucket is isolated. Disable outbound email,
   payment, MiniLogistics, webhook delivery, and public CDN access or point each to an
   approved sandbox sink.
3. Verify restore operators use time-bounded least-privilege credentials. Do not copy
   credentials into shell history; use the platform secret mechanism.
4. Capture the source object keys, object version IDs, checksums, and the corresponding
   `content.media_assets`/variant metadata identifiers for the selected sample.

### 2. Restore PostgreSQL to an isolated database

1. Ask the managed-database operator to restore the selected backup/PITR point into a
   new isolated PostgreSQL instance/database. Record backup ID, requested timestamp,
   restore start/end, encryption state, and restore identity.
2. Use a read-only validation identity to verify the expected migration history and
   selected business/media metadata. Do not run destructive `Down` migrations.
3. Run clean-create and shipment-schema upgrade validation against the exact candidate
   migration job/image, once per isolated target. The repository-level check is:

   ```powershell
   ./scripts/verify-prh-009-migrations.ps1
   ```

   The platform run must instead use the deployed candidate migration job and its
   immutable image digest. Capture its job logs without connection strings or secrets.
4. If the source schema differs from the candidate, follow the migration-specific
   forward-fix guidance in [the production release runbook](production-release.md).
   A code rollback is allowed only where the target code can read the restored data;
   credential, token, moderation, and durable-media migrations use forward fixes.

### 3. Restore media objects and verify application reads

1. Restore a representative original object and every stored variant from the object
   store into the isolated bucket, using the recorded version IDs. Include an accidental
   deletion case if versioning supports it.
2. Restore/validate matching `content.media_assets` and variant metadata in PostgreSQL.
   Do not mark an asset available until object key, checksum, MIME type, dimensions,
   state, references, and access policy agree.
3. Start the exact candidate against only the isolated configuration. Use `/health/ready`
   and authenticated/authorized application reads to verify the sampled media can be
   served through the intended URL path, with no legacy local-disk URL emitted.
4. Record object version IDs, checksums, object and metadata restoration timestamps,
   access/permission result, and any mismatch. A metadata-only restore is a failed
   PRH-017 object-data test.

### 4. Exercise failure and recovery scenarios

Perform each scenario independently and record its decision/owner:

| Scenario | Required recovery behavior |
| --- | --- |
| Accidental media deletion | Restore the correct object version, validate checksum/metadata/reference/access, and show application read success. |
| Corrupted media metadata | Quarantine/mark unavailable as appropriate, repair from a known source, validate variants, and retain audit evidence. |
| Failed migration | Stop rollout, preserve backup and migration history, use the documented forward fix; do not run a destructive down migration against security/media state. |
| Data Protection key access loss | Restore access to the existing protected key ring or its approved backup; do not replace it with an empty ring or expose protected payloads. |
| Revoked external credential | Use the rotation runbook, apply the new secret through the platform store, validate least-privilege access, and prove old credentials fail. |
| Deployment rollback | Roll back only to a data-compatible image or use a forward fix. Do not restore an exposed credential or discard token/moderation/media audit state. |

### 5. Measure, validate, and close

1. Compute actual RPO from the selected recovery point and actual RTO from the agreed
   incident start through application validation. Compare both to approved targets.
2. Validate health, database connectivity, authenticated account flow, media read,
   outbox/worker state, and observability in the isolated environment. Preserve query,
   dashboard, and job links rather than sensitive raw output.
3. Reconcile selected orders, media references, and migration state. Record any data
   unavailable by design and the explicit business owner/date for remediation.
4. Tear down the isolated environment under the platform retention policy, revoke
   temporary access, and ensure no synthetic recovery data is publicly reachable.
5. The release manager marks PRH-017 pass only after the evidence template is complete
   and each failure/recovery discrepancy has an owner and a retest date.

## Repository regression checks

These safe local checks should accompany, but never replace, the real restore exercise:

```powershell
dotnet build WorkspaceEcommerce.slnx --no-restore --disable-build-servers -m:1
dotnet test WorkspaceEcommerce.slnx --no-build --no-restore --disable-build-servers -m:1
./scripts/verify-prh-009-migrations.ps1
./scripts/verify-prh-009-backup-restore.ps1
```

The backup script creates and removes only synthetic temporary PostgreSQL containers by
default. Use `-KeepBackup` only for protected local debugging data; never turn it into a
production backup workflow.

## Related material

- [Production release runbook](production-release.md): migration/release gates,
  topology checks, and rollback boundaries.
- [Credential rotation runbook](credential-rotation.md): external secret and key
  rotation procedure.
- [ADR 004: durable media storage](../adr/004-durable-media-storage.md): media state,
  cleanup, and object/metadata lifecycle.
- [PRH-017 disaster recovery evidence template](../reports/prh-017-disaster-recovery-evidence-template.md).
