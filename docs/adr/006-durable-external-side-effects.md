# ADR 006: Durable external side effects

## Status

Accepted on 2026-09-22. This record documents the architecture implemented by the customer
email and MiniLogistics workflows.

## Context

Checkout, payment, account lifecycle, and fulfillment commit business state in
PostgreSQL but also need SMTP or carrier calls. Calling a provider inside the
request transaction cannot make the database and remote system atomic: a crash
can lose work or repeat an accepted request. Multiple API/worker replicas also
need safe claiming, retry, and operational recovery.

Provider callbacks have the inverse problem. Retries and out-of-order events
must not apply the same transition twice or regress recorded state.

## Decision

- Persist email and shipment commands in PostgreSQL in the same transaction as
  the business event that requires them. Request handlers do not perform the
  corresponding external mutation.
- Workers lease bounded due batches with `FOR UPDATE SKIP LOCKED`. A lease token
  must still match when completing, retrying, or dead-lettering so a stale worker
  cannot overwrite a newer owner.
- Delivery is at-least-once. Use stable provider idempotency keys where the
  provider supports them: order code for shipment creation and
  `<order-code>:cancel` for cancellation.
- Bound provider attempts and durable retries. Permanent conflicts or exhausted
  attempts become dead letters; replay creates new auditable work instead of
  editing the original record.
- Authenticate MiniLogistics callbacks over the raw body and timestamp before
  deserialization. Persist event identity/timeline and apply only known,
  non-regressing provider state.
- Logs, metrics, and admin inspection expose correlation IDs and safe status
  categories, never protected payloads, recipients, tokens, signatures, or raw
  provider bodies.

## Consequences

- A command can be delivered more than once after a crash. Provider
  idempotency retention must exceed the application retry horizon. SMTP may
  still duplicate a message because it has no universal idempotency contract.
- Business success means durable work was accepted, not that the provider
  already completed it. APIs and UIs must expose queued/retrying/dead-letter
  state honestly.
- Every new external mutation must either join this pattern or record a separate
  ADR explaining its consistency and recovery model.
- Operators recover through authenticated inspection/replay APIs and the
  [outbox runbook](../runbooks/background-outbox-operations.md), never through
  direct row edits.
