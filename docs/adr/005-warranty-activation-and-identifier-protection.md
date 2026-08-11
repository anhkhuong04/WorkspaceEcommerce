# ADR 005: Warranty activation, ownership, and identifier protection

## Status

Accepted on 2026-08-11 for the first warranty-release MVP.

## Decision

- Warranty activation requires a Customer-authenticated request. The server verifies that the serialized physical unit is assigned to an order owned by that customer; possession of a Serial/IMEI alone is not authority to activate.
- The MVP accepts only orders created by this platform. Historical and external purchases require an audited admin-assisted process rather than anonymous self-service activation.
- An order must first reach `Completed`. For online payments, `PurchasedAt` is the immutable `PaidAt`; for COD, it is the immutable `CompletedAt`. Warranty coverage starts at `PurchasedAt`, while activation must happen no later than `PurchasedAt + ActivationWindowDays` (60 days by the default plan).
- Warranty plans are versioned records with one or more coverage components. Entitlements snapshot terms and component end dates at activation so later catalog/policy edits do not rewrite a contract.
- One `SerializedProductUnit` maps to one physical product and at most one order item. A line with quantity greater than one requires multiple serial-unit assignments.
- The database retains only a key-versioned HMAC fingerprint and a masked display form of Serial/IMEI. Raw identifiers are never stored, logged, placed in paths/query strings, telemetry, audit reasons, or error exports.
- Public lookup is an exact body-based request. It returns only a generic found/not-found outcome plus masked identifier, product display name, warranty status, and coverage dates. It never exposes customer, order, address, payment, or internal identifiers.
- The warranty module is feature-flagged. Production rollout order is: additive schema, plans, units/imports, assignments, customer activation, then public lookup.

## Consequences

- The system needs dedicated warranty persistence, HMAC configuration, rate-limit partitions, audit records, and tested role/ownership rules.
- A leaked database cannot directly reveal stored Serial/IMEI values. HMAC secret rotation requires supporting a versioned dual-read migration in a future key-rotation release.
- Returns and replacements are explicit audited lifecycle operations. This MVP does not infer or silently delete warranty history.
- Deployment must use an external `Warranty:IdentifierHmacKey` of at least 32 non-placeholder characters whenever any warranty feature is enabled.
