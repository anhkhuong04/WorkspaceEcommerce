# Warranty operations

## Enablement order

1. Deploy the additive warranty migration and confirm the recorded migration ID.
2. Configure a non-placeholder `Warranty:IdentifierHmacKey` of at least 32 characters and `Warranty:IdentifierKeyVersion=1` from the secret manager. `IdentifierHmacKeys` remains empty on the first deployment.
3. Enable `Warranty:Enabled` and `Warranty:AdminEnabled`; keep activation and public lookup disabled.
4. Create versioned plans, assign them to variants, import units, and reconcile each order-item quantity against assigned units.
5. Enable `Warranty:ActivationEnabled` for an internal smoke account, then the customer cohort.
6. Observe activation failures, rate-limit events, and email-outbox delivery before enabling `Warranty:PublicLookupEnabled`.

## Import and correction

- Import CSV accepts `sku,identifier,identifier_type`; use preview before commit. Identifier values are intentionally not recoverable from the database after import, so retain the source file only in the approved operations system.
- Imports are idempotent by a safe checksum derived from identifier fingerprints. Do not retry by changing only whitespace or case; review the preview instead.
- Never correct a unit or entitlement by editing PostgreSQL. Void/replacement actions must include an operational reason and generate an audit event.

## Incident and recovery

- On suspected identifier-key exposure, immediately disable public lookup and activation flags. Deploy a new `IdentifierKeyVersion` and `IdentifierHmacKey`, retain the previous key as `Warranty:IdentifierHmacKeys:{oldVersion}` for dual-read, re-fingerprint/reconcile every unit, then remove the old key only after a sampled lookup smoke and backup verification. Do not overwrite a key in place.
- Rollback is feature-flag disablement. The migration is additive; do not drop warranty tables while support cases or registrations exist.
- Backup and restore `warranty` metadata together with `ordering`, `catalog`, and the secret-manager version metadata. A database backup without the applicable HMAC-key recovery path cannot serve new lookup requests after a rotation.
