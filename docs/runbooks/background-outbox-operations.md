# Durable background outbox operations

This runbook covers the customer-email and shipment-command outboxes. It is
intended for an authenticated administrator or on-call operator; do not edit
outbox rows directly in PostgreSQL.

## Delivery semantics

- Workers atomically select bounded due rows with PostgreSQL `FOR UPDATE SKIP
  LOCKED`, persist a lease, and commit that lease before any SMTP or carrier
  request.
- An expired lease becomes eligible for another replica. A completion/retry
  update must still match the lease token, so a stale worker cannot overwrite a
  newer owner's result.
- Shipment create uses the stable order code as the provider idempotency key;
  cancel uses `<order-code>:cancel`. The provider must retain idempotency keys
  for longer than the maximum retry horizon.
- The system is at-least-once. SMTP has no universal idempotency contract: a
  process crash after provider acceptance and before the database completion
  commit can result in a duplicate message. Account links remain short-lived
  and one-time; use the provider's message-id/idempotency feature when the
  selected SMTP relay supports it.
- Email and shipment commands stop after their configured retry ceiling or a
  permanent validation/provider conflict and move to `DeadLetter`.

## Inspect

Use an admin access token and the documented API host.

```http
GET /api/admin/operations/outbox
GET /api/admin/operations/outbox/dead-letters?limit=50
```

The response intentionally excludes customer recipients, email subjects,
protected payloads, and provider response bodies. Record the trace ID, source
outbox ID, error category, deployment digest, and safe admin session ID in the
incident ticket.

## Replay

Correct the root cause first: SMTP credentials/allowlist, carrier contract,
shipping address, or provider outage. Then replay through the API, which
creates a new durable pending command and leaves the original dead letter as
audit history.

```http
POST /api/admin/operations/outbox/customer-email/{messageId}/replay
POST /api/admin/operations/outbox/shipment-command/{commandId}/replay
```

The service writes an audit log with the source command ID and JWT session ID;
it never logs recipient, payload, token, or provider body. A replay can be
rejected if an active shipment command of the same type already exists.

For expired account verification/reset links, prefer asking the customer to
initiate a new lifecycle request instead of replaying an old message.

## Monitor and escalate

Monitor these metrics by `outbox` tag, aggregated by **maximum** across
replicas for gauges:

- `workspaceecommerce.outbox.due`
- `workspaceecommerce.outbox.leased`
- `workspaceecommerce.outbox.retrying`
- `workspaceecommerce.outbox.dead_letter`
- `workspaceecommerce.outbox.oldest_active.age`
- `workspaceecommerce.outbox.claimed`, `.completed`, `.retried`,
  `.dead_lettered`, and `.processing.duration`

Alert on a sustained oldest-active age above the agreed SLO, any new dead
letter, a growing retry count, or a queue that is due but has no completed
counter activity. Validate alert rules and ownership in staging before marking
PRH-014 or PRH-018 complete.

## Replica and shutdown checks

Before a rolling restart, confirm active leases and queue age are within the
normal range. Workers stop on the host cancellation token and do not begin the
next poll after shutdown starts. Wait for the configured termination grace
period; leases recover automatically after their configured expiry if a process
is killed.

Two-replica, killed-worker, SMTP/carrier timeout, and provider-idempotency
tests remain mandatory staging evidence. Cluster-wide ingress/WAF rate limiting
and a SignalR scale-out provider are platform requirements; the in-process API
rate limiter remains only defense in depth until those are attached and tested.
