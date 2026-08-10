# Observability and alerting contract

This document defines the repository-side signal contract for the release
candidate. Dashboards, retention, sampling, alert routing, and alert exercises
must be configured by the Platform/Observability owner in the target staging
environment before PRH-014 or PRH-018 can be closed.

## Safe telemetry boundary

`SensitiveTelemetryRedactionProcessor` runs immediately before Application
Insights transmission. It removes query strings, URI userinfo/fragments,
Authorization/Cookie/Bearer data, email addresses, known credential/token/TOTP
and recovery-code properties, bodies/payloads, webhook signatures, and
connection-string-like values. It keeps correlation-safe fields such as trace
ID, route template, order ID/code, outbox ID, provider status category, and
replica identity.

Do not place raw request/response bodies, recipients, protected email payloads,
JWTs, refresh tokens, TOTP/recovery values, webhook signatures, connection
strings, or provider secrets in logs, telemetry properties, exceptions, or
metric dimensions. A redaction unit test is a guardrail, not proof of a hosted
telemetry sink.

## SLI and metric catalog

| Concern | Primary signal | Initial alert intent | Owner |
| --- | --- | --- | --- |
| API availability/latency | HTTP success rate and route p50/p95/p99 | Sustained 5xx or latency above approved SLO | Application + Platform |
| Checkout/stock/coupon | Checkout transaction failures and stock/coupon conflicts | Unexpected error or integrity failure growth | Commerce owner |
| VNPay | Callback outcome/reconciliation failures | Payment callback failure or duplicate anomaly | Payments owner |
| Authentication abuse | Auth/2FA rate-limit rejections and refresh-reuse events | Threshold per trusted client partition | Security owner |
| Shipment webhook | Rejection/duplicate counters and provider dependency failures | Invalid-signature or reject spike | Logistics owner |
| Background queues | `workspaceecommerce.outbox.*` metrics | Dead letter > 0, due queue age above SLO, stalled completion | Application on-call |
| Media | Storage failures and cleanup errors | Repeated object-store failure | Content/Platform |
| PostgreSQL | Pool usage, connection/command latency, readiness | Saturation, timeout, or readiness failure | Database owner |

For outbox gauges, aggregate by **maximum** across replicas. Counters and
durations should retain the `outbox` tag only; never attach recipient, email,
token, raw provider response, or unbounded order/customer cardinality.

## Required staging evidence

1. Configure Application Insights/OpenTelemetry sampling and retention so failed
   dependencies and audit/security events remain searchable for the agreed
   incident window.
2. Build the dashboards above with a named owner, escalation route, severity,
   evaluation window, and the linked runbook.
3. Send synthetic marker values for JWT, refresh token, email, webhook body,
   signature, recovery code, and connection string. Search all staging sinks;
   each marker must return zero matches.
4. Exercise alerts for readiness, 5xx/latency, authentication throttling,
   dead letters, invalid webhook, payment failure, database exhaustion,
   backup/object-store failure, and telemetry ingestion loss. Record
   acknowledgement and recovery timestamps.

No release may rely on a dashboard or alert that has not been exercised with
the candidate image/digest.
