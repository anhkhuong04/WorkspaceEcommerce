# Integration boundaries

This document describes only contracts consumed or exposed by this repository.
Provider portals and provider-owned API specifications remain authoritative for
credentials, availability, and undocumented behavior.

## VNPay

- Checkout creates a pending provider transaction and a redirect URL; card/bank
  credentials never enter WorkspaceEcommerce.
- Browser return and IPN both verify HMAC data, reject missing or malformed
  required provider fields, and converge on the same locked, idempotent
  finalization path. Amount must be positive and equal the stored transaction
  amount; success requires both response and transaction status `00`.
- A successful transaction marks the order paid and inserts shipment-create work
  in the same database transaction.
- Treat IPN as server evidence and the browser redirect as user navigation. Do
  not let a frontend query parameter set payment state.
- Secrets and raw callback material must not be logged. Production URLs must be
  public HTTPS endpoints and match the provider registration.

## MiniLogistics

The configured `MiniLogistics:BaseUrl` is a partner API root. The adapter uses:

| Operation | Relative request | Side-effect policy |
| --- | --- | --- |
| Quote | `POST shipping/quote` | Synchronous, read-like checkout dependency |
| Create shipment | `POST shipments` | Shipment outbox only; `Idempotency-Key` is the order code |
| Tracking | `GET shipments/{trackingCode}` | Customer/admin read or explicit refresh |
| Cancel | `POST shipments/{trackingCode}/cancel` | Shipment outbox only; stable cancel idempotency key |

The inbound endpoint is `POST /api/webhooks/minilogistics`. It requires
`X-MiniLogistics-Timestamp` within the configured tolerance and
`X-MiniLogistics-Signature = sha256=<hex HMAC-SHA256>` over
`<timestamp>.<raw-body>`. Supported events are `shipment.created`,
`shipment.status_changed`, and the no-op `webhook.test` probe.

Status mapping is intentionally conservative: pickup states advance through
Confirmed/Processing, transit states to Shipping, Delivered to Completed,
delivery failure/provider return to FailedDelivery, and provider cancellation
only where the order aggregate permits cancellation. Unknown statuses are
rejected by application processing.

Timeout, bounded retry, and the in-process failure gate protect each provider
attempt. Durable command retries/dead letters protect process-level failure.
Provider responses and credentials are not part of the public API contract.

## Customer email

Application use cases enqueue Data Protection-protected payloads; they do not
send SMTP in the request transaction. Development may use the metadata-only
`Log` provider. Production requires SMTP and bounded leased retries. A crash
after SMTP accepts a message but before completion commit can duplicate mail;
account links therefore remain short-lived and one-time.

## Media storage and malware scanning

Development may use local storage; non-Development uses an S3-compatible object
store and stable public base URL. PostgreSQL owns asset state and references;
object bytes and variants share the lifecycle described in
[ADR 004](../adr/004-durable-media-storage.md).

Only a `NoOp` malware scanner exists. Non-Development startup and each upload
require active, named, time-limited risk acceptance. Follow the
[media policy](../runbooks/media-malware-scanning.md); never describe `NoOp` as
a successful scan.

## Google and browser sessions

The client sends only a Google ID token. The backend validates it against the
server-owned audience allow-list. Customer refresh credentials are rotating
`HttpOnly` cookies; the storefront stores only short-lived access state in the
current tab. See [ADR 003](../adr/003-customer-account-lifecycle.md).

## SignalR

Authenticated customers connect to `/hubs/notifications` and join a customer-
scoped group for order updates. Registration is currently in-process only. More
than one API replica requires an approved shared backplane and a cross-replica
smoke test; sticky sessions alone do not broadcast events between replicas.
