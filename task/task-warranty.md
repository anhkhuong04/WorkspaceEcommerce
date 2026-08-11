# Warranty Activation & Lookup — Pre-Implementation Assessment and Delivery Roadmap

**Assessment date:** 2026-08-11  
**Current status:** Core implementation is complete behind disabled feature flags; release verification and product-owner sign-off remain.  
**Target UI language:** English  
**Proposed module:** Warranty activation, authenticated customer warranty management, public warranty lookup, and admin provisioning

## 1. Executive verdict

The codebase is **not yet functionally ready** to expose warranty activation or lookup. It currently has a static warranty-policy page and a demo product specification only; it has no warranty aggregate, serial/IMEI inventory, database schema, API, admin workflow, customer warranty page, or warranty-specific tests.

The supporting platform is in a good state for implementation. Customer authentication, customer-owned orders, shipment completion events, PostgreSQL/EF Core, durable email outbox, rate limiting, telemetry, admin authorization, and server-side query patterns can all be reused. This should therefore be implemented as a new bounded module, not embedded into the static policy page or represented by free-text product specifications.

### Readiness summary

| Area | Current evidence | Status before implementation |
|---|---|---|
| Warranty content | `/warranty-policy` is a static English support page in `SupportPages.tsx` | Ready to retain; it is not an activation system |
| Product warranty data | Product specifications can contain free-text such as `Warranty = 5 years` | Not suitable for rules, dates, or enforcement |
| Serialized units | No Serial/IMEI entity, validation, import, assignment, or unique index | Missing |
| Order linkage | `OrderItem` snapshots variant/SKU/name and quantity | Reusable, but one item can represent multiple physical units |
| Eligibility date | `Order` has `CreatedAt`, `PaidAt`, status history, and shipment delivery mapping; no explicit `DeliveredAt`/`CompletedAt` | Business rule and durable timestamp required |
| Customer ownership | Customer order endpoints enforce the Customer role and ownership | Ready to reuse |
| Public lookup | Order code + phone lookup exists, but returns order/PII-oriented data | Must not be reused for warranty lookup responses |
| Admin operations | Admin role, catalog, variant, and order management exist | Framework ready; warranty screens/API missing |
| Notifications | Durable protected customer email outbox and worker exist | Ready to reuse |
| Abuse protection | Global IP rate limiter exists; warranty routes would receive the permissive default partition | Dedicated partitions required |
| Persistence/release | PostgreSQL EF migrations and migration verification tooling exist | New schema/migration and empty/upgrade tests required |
| Tests | Application, infrastructure, API integration, frontend unit, and E2E layers exist | Warranty coverage missing |

## 2. Recommended MVP boundary

### In scope

- Admin creates and versions warranty plans, including multiple coverage components (for example frame and motor with different durations).
- Admin imports Serial/IMEI units in an idempotent batch and assigns exactly one physical unit per purchased quantity to an order item.
- A signed-in customer activates an eligible unit purchased through this system within the configured activation window (default 60 days).
- A signed-in customer lists and views their own activated warranties.
- Any visitor can perform exact Serial/IMEI lookup and receive a privacy-safe, minimal warranty result.
- Activation confirmation is sent through the existing durable email outbox.
- Admin can search, inspect, manually activate, void, or replace a unit with a mandatory reason and audit trail.

### Explicitly out of MVP

- Repair tickets, claim adjudication, RMA, service-center scheduling, spare parts, and repair history.
- Automatic activation for external retailers or historical orders without a verified serialized-unit assignment.
- Anonymous self-activation based only on a serial number.
- OCR of labels. Camera/barcode scanning is progressive enhancement with manual input fallback.
- A multilingual UI. The current site requirement remains English even though the reference screenshot is Vietnamese.

## 3. Business decisions that must be locked before WTY-002

The recommended defaults below allow implementation to proceed safely. Any change should be recorded in an ADR because it changes stored dates or authorization behavior.

| Decision | Recommended default | Reason |
|---|---|---|
| Who may activate? | Authenticated Customer only | Prevents theft/claiming of leaked serials and matches login-only checkout |
| Supported sales channel | Orders created in this platform for MVP | Ownership and purchase evidence are already verifiable |
| When may activation start? | After the order reaches `Completed` from the trusted shipment/admin flow | Avoids activation before delivery and before a return/cancellation outcome is known |
| What is “purchase date”? | Snapshot a documented `PurchasedAt`; online-paid orders use `PaidAt`, COD uses authoritative completion/delivery time | One rule cannot safely infer both prepaid and COD dates from `CreatedAt` |
| Activation deadline | `PurchasedAt + WarrantyPlan.ActivationWindowDays`, default 60 days | Configurable/versioned; never hard-coded in UI or service |
| Coverage start | Snapshot at activation using the approved policy (recommended: `PurchasedAt`) | Prevents delayed activation from extending contractual coverage |
| Warranty duration | Component-based coverage snapshots | The current policy can have different durations for frame, motor, or other components |
| Public disclosure | Product display name, masked identifier, activation state, coverage components/dates only | Must not expose customer, order code, phone, email, address, or internal IDs |
| Historical/external purchase | Admin-assisted verification only in MVP | The system has no reliable serial ownership data to backfill automatically |
| Return/replacement | Explicit audited admin operation; no silent destructive cascade | Contractual treatment depends on policy and must remain explainable |

**Blocking product clarification:** approve the `PurchasedAt` and coverage-start rules above, especially for COD, returns, and replacement units. Development can begin behind a feature flag while this ADR is finalized, but production activation must not launch with ambiguous date semantics.

## 4. Target domain and data model

Use a new PostgreSQL schema named `warranty`. Keep identifiers, entitlements, and coverage snapshots separate so historical warranties are not changed when a product or plan is edited.

### 4.1 `WarrantyPlan`

- `Id`, unique `Code`, `Name`, `ActivationWindowDays`, `TermsVersion`.
- `EffectiveFrom`, optional `EffectiveTo`, `IsActive`, audit timestamps.
- Plans are versioned/retired, not edited retroactively after use.

### 4.2 `WarrantyPlanCoverage`

- `Id`, `WarrantyPlanId`, `ComponentCode`, `DisplayName`, `DurationMonths`, `SortOrder`.
- Unique component code within a plan version.
- Supports product policies such as “Frame: 60 months; Motor: 36 months.”

### 4.3 `ProductVariantWarrantyPlan`

- Associates a product variant with the applicable plan and effective interval.
- Resolve and snapshot the plan when a physical unit is provisioned/assigned.
- Do not derive rules from `ProductSpecification` free text.

### 4.4 `SerializedProductUnit`

- `Id`, `ProductVariantId`, `IdentifierType` (`Serial` or `IMEI`).
- Versioned deterministic lookup fingerprint, encrypted original value when operational display is required, and masked suffix for responses.
- `Status`: `Available`, `Assigned`, `Activated`, `Voided`, `Replaced`, or `Returned`.
- Optional `OrderItemId`, `AssignedAt`, source/import-batch metadata, concurrency token, audit timestamps.
- One unit represents one physical product. An `OrderItem` with quantity 3 must have three unit assignments.
- Unique index on `(IdentifierType, IdentifierKeyVersion, IdentifierFingerprint)`.

### 4.5 `WarrantyEntitlement`

- `Id`, unique `SerializedProductUnitId`, `WarrantyPlanId`, optional `CustomerId`, `OrderId`, and `OrderItemId`.
- `PurchasedAt`, `EligibleAt`, `ActivationDeadline`, optional `ActivatedAt`.
- `Status`: `PendingActivation`, `Active`, `Expired`, `Voided`, or `Replaced`.
- Activation source, accepted terms version, creation/update timestamps, concurrency token.
- Repeated activation by the owner is idempotent; a different owner receives a non-disclosing conflict.

### 4.6 `WarrantyCoverageSnapshot`

- `WarrantyEntitlementId`, component code/name, `StartsAt`, `EndsAt`, source duration.
- Coverage dates are immutable contractual snapshots.
- “Expired” should normally be derived from current time and coverage dates, avoiding a fragile daily mass update.

### 4.7 `WarrantyAuditEvent` and `WarrantyImportBatch`

- Append-only event records actor type/id, action, timestamp, reason, correlation ID, and non-sensitive metadata.
- Import batch records file checksum, counts, status, errors, requester, and timestamps for idempotency and supportability.
- Never put raw Serial/IMEI values in audit text, logs, traces, exception messages, or import error exports.

### 4.8 Order timestamp addition

Add an explicit `CompletedAt` or `DeliveredAt` field to `Order`, set during the existing status transition that maps trusted shipment delivery to `Completed`. Backfill historical values from the earliest matching status-history record only after verifying the query and documenting ambiguous rows. The warranty entitlement must snapshot its own approved dates rather than recalculating them from mutable order state.

## 5. Identifier security and normalization

- Accept identifiers only in request bodies, never URL paths or query strings, to reduce access-log leakage.
- IMEI: exactly 15 digits after approved normalization and valid Luhn checksum.
- Serial: define an allow-list and manufacturer-specific normalization. Do not strip characters that may be semantically significant; cap length (recommended 64).
- Lookup uses a versioned HMAC fingerprint with a dedicated secret, not a plain SHA hash. Serial/IMEI spaces are small enough for offline enumeration if a database leaks.
- Encrypt the original identifier at rest only if an admin workflow truly requires retrieval; otherwise retain the fingerprint and masked form only.
- Support key rotation through a key-version column and dual-read migration window.
- Mark responses `Cache-Control: no-store`; redact request bodies and identifiers from Application Insights and structured logging.
- Public lookup returns the same generic response shape/timing for unknown, malformed, and non-public records where practical.
- Create dedicated IP-based public lookup limits and combined customer-ID/IP activation limits. Add escalating cooldown/CAPTCHA capability after repeated failures.
- Ownership is checked server-side through `Order.CustomerId`; frontend state is never proof of ownership.

## 6. Proposed workflows

### 6.1 Provisioning and order assignment

1. Admin versions a warranty plan and assigns it to eligible variants.
2. Admin uploads a bounded CSV containing SKU, identifier type, and Serial/IMEI.
3. Server validates file size/type, SKU, identifier checksum/format, intra-file duplicates, database duplicates, and active plan mapping.
4. Server creates an idempotent import batch with row-level results; no raw identifiers appear in telemetry or exported error cells.
5. During packing/fulfillment, admin assigns one available unit per ordered quantity to the matching `OrderItem`.
6. The assignment snapshots plan and eligibility inputs. Invalid SKU/quantity/order-state combinations fail atomically.

### 6.2 Customer activation

1. Signed-in customer enters or scans Serial/IMEI.
2. API normalizes and fingerprints it, then loads a bounded projection of unit, assignment, order ownership, order state, and entitlement.
3. Service verifies ownership, trusted completion, non-returned state, activation deadline, unit status, and plan snapshot.
4. A database transaction creates/activates the entitlement and coverage snapshots, changes unit state, appends an audit event, and enqueues confirmation email.
5. Unique constraints and concurrency handling make retry/race behavior idempotent.
6. Response shows masked identifier, product, activation date, activation status, and component coverage dates.

### 6.3 Public lookup

1. Visitor submits an exact Serial/IMEI with no customer identity.
2. API applies strict validation, dedicated throttling, fingerprint lookup, and a no-tracking projection.
3. Response contains only public warranty status, masked identifier, product display name, and coverage dates/components.
4. No order, customer, payment, address, internal notes, or unmasked identifier is returned.

### 6.4 Returns and replacements

- Returning an order raises a reviewable warranty state transition; it does not delete the entitlement.
- Replacement creates a link from old unit to new unit and an audit event. Whether dates carry forward or restart is a plan/policy rule and must be explicit.
- Manual activation/void/replacement requires Admin authorization and a mandatory reason.

## 7. Proposed API contracts

### Public

- `POST /api/warranties/lookup` — exact lookup using `{ identifierType?, identifier }`; privacy-safe response, `no-store`.

### Authenticated customer

- `POST /api/customer/warranties/activate` — activate an owned eligible unit; idempotent outcome.
- `GET /api/customer/warranties?pageNumber=&pageSize=` — bounded, server-side paged list.
- `GET /api/customer/warranties/{warrantyId}` — own warranty detail only.

### Admin

- `/api/admin/warranty-plans` — list/create/version/retire plans and coverages.
- `/api/admin/warranty-units/imports` — upload/status/error report for bounded imports.
- `/api/admin/warranty-units` — paged search and detail.
- `POST /api/admin/warranty-units/{id}/assign` — assign to order item with quantity checks.
- `/api/admin/warranties` — paged search/detail.
- `POST /api/admin/warranties/{id}/activate|void|replace` — audited manual transitions.

All handlers require cancellation tokens, async EF calls, `AsNoTracking` and direct DTO projections for reads, validated/clamped pagination, deterministic secondary ordering, and no unbounded materialization. API types must be added to `frontend/packages/api-types` rather than duplicated in applications.

## 8. Storefront and admin UX plan

### Storefront

- Add `/warranty` as the activation/lookup portal; preserve `/warranty-policy` as policy content.
- Change the header Warranty destination to `/warranty`; include a clear link from the portal to the full policy.
- Match the reference composition but use English copy: title, concise instruction, input, optional scan control, lookup action, and three explanatory steps.
- Default action is public **Check warranty**. Show **Activate warranty** only after a valid assigned unit is found; if signed out, display a login-required modal before navigating to login and preserve a safe return URL.
- Add protected `/account/warranties` with paged cards/table and detail view.
- Use accessible labels, keyboard focus, status announcements, loading/empty/error states, mobile layout, and sufficient contrast.
- Barcode/camera scan must be feature-detected, permission-aware, and optional; manual input always remains available.

### Admin

- Add a Warranty navigation entry with tabs for Plans, Units/Imports, and Registrations.
- Provide dry-run import summary before commit, duplicate/error download, progress/status, and safe retry.
- Assignment UI must display order item quantity versus assigned-unit count.
- Destructive state transitions use a confirmation dialog, mandatory reason, and a visible audit timeline.

## 9. Detailed implementation backlog

### WTY-001 — Approve policy rules, ADR, and threat model

- [ ] Approve purchase/coverage dates for prepaid, COD, returns, and replacement products.
- [ ] Confirm internal-order-only MVP and authenticated activation.
- [ ] Define public response fields and identifier normalization rules per manufacturer.
- [ ] Record abuse cases: enumeration, stolen identifier, horizontal access, replay/race activation, malicious import, telemetry leakage.
- [ ] Define retention/deletion rules and support access to identifiers.

**Acceptance:** ADR and threat model are reviewed; no production-affecting date or ownership rule remains implicit.

### WTY-002 — Implement warranty domain model

**Depends on:** WTY-001

- [ ] Add warranty aggregates, enums, guards, state transitions, coverage snapshots, and audit events.
- [ ] Add variant-to-versioned-plan mapping and one-unit-per-physical-item assignment rules.
- [ ] Add identifier normalizer/protector abstractions and time abstraction usage.
- [ ] Register application services through existing dependency-injection conventions.
- [ ] Add domain/application unit tests for every state transition and date boundary.

**Acceptance:** invalid transitions are impossible through domain APIs; plan edits cannot mutate existing coverage snapshots.

### WTY-003 — Add PostgreSQL schema and safe migration

**Depends on:** WTY-002

- [ ] Add `warranty` schema, EF configurations, DbSets/query abstractions, constraints, and the minimal evidence-backed indexes.
- [ ] Add explicit order completion/delivery timestamp and deterministic historical backfill with an ambiguity report.
- [ ] Add unique constraints for identifier fingerprints and one entitlement per unit.
- [ ] Document backup, forward-fix, and rollback behavior; never fabricate units for historical orders.
- [ ] Verify migration from an empty database and upgrade from the current `AddOutboxLeaseMetadata` schema.
- [ ] Rebuild/run the migration image from the same commit so stale binaries cannot report a false up-to-date state.

**Acceptance:** clean and upgrade migrations pass; constraints reject duplicates/races; before/after schema evidence is recorded.

### WTY-004 — Deliver warranty-plan administration

**Depends on:** WTY-003

- [ ] Implement version/create/list/detail/retire APIs and validation.
- [ ] Implement variant plan assignment with effective dates.
- [ ] Build Plans admin UI and shared API types.
- [ ] Prevent retirement/edit operations that would invalidate existing entitlements.
- [ ] Add authorization, audit, API integration, and frontend tests.

**Acceptance:** admin can manage future rules while historical registrations remain unchanged.

### WTY-005 — Deliver serialized-unit import and assignment

**Depends on:** WTY-003, WTY-004

- [ ] Implement bounded CSV dry-run/import, file checksum idempotency, validation, duplicate detection, and row result reporting.
- [ ] Protect identifiers before persistence and redact every log/error path.
- [ ] Implement unit search and order-item assignment transaction with quantity/SKU/state checks.
- [ ] Build Units/Imports admin UI and assignment workflow.
- [ ] Add large-file limits, cancellation, concurrency, malicious-input, CSV formula-injection, and duplicate tests.

**Acceptance:** one physical unit maps to at most one eligible order item; retries do not duplicate data; raw identifiers do not appear in logs.

### WTY-006 — Deliver customer activation

**Depends on:** WTY-003, WTY-005

- [ ] Implement customer-owned activation service and controller.
- [ ] Enforce completion, ownership, plan, deadline, return, and unit-state rules in one transaction.
- [ ] Snapshot component coverage dates and terms version.
- [ ] Enqueue an activation confirmation through the existing email outbox.
- [ ] Implement customer warranty list/detail with bounded PostgreSQL projections.
- [ ] Add idempotency, race, horizontal-access, COD/prepaid boundary, and email-outbox tests.

**Acceptance:** only the verified owner can activate; retries are safe; quantity and date boundaries are correct; activation survives email-provider failure.

### WTY-007 — Deliver privacy-safe public lookup

**Depends on:** WTY-003, WTY-005

- [ ] Implement body-based exact lookup with strict Serial/IMEI validation.
- [ ] Return a dedicated minimal DTO with masked identifiers and no PII/order data.
- [ ] Add warranty-specific rate-limit partitions and failure metrics.
- [ ] Add `no-store`, telemetry redaction, uniform error behavior, and optional CAPTCHA escalation hook.
- [ ] Add enumeration, malformed-input, cache-header, response-contract, and query-count tests.

**Acceptance:** lookup is useful without revealing owner/order data, raw identifiers, or an inexpensive enumeration oracle.

### WTY-008 — Deliver storefront and customer-account UX

**Depends on:** WTY-006, WTY-007

- [ ] Build English `/warranty` lookup/activation portal and link the existing policy.
- [ ] Add login-required activation modal and safe post-login return handling.
- [ ] Add `/account/warranties` list/detail and responsive states.
- [ ] Add optional camera/barcode progressive enhancement with manual fallback.
- [ ] Add accessibility, component/unit, route, auth-boundary, and browser E2E tests.

**Acceptance:** anonymous visitors can check, only signed-in owners can activate, and the full flow works by keyboard and on mobile without camera support.

### WTY-009 — Deliver admin lifecycle operations and observability

**Depends on:** WTY-005, WTY-006

- [ ] Add paged registration search/detail and audit timeline.
- [ ] Implement manual activation, void, return review, and replacement with mandatory reason.
- [ ] Add structured metrics for lookup outcome, activation outcome, import outcome, latency, throttling, and email delivery without sensitive dimensions.
- [ ] Add dashboards/alerts for activation failure spikes, import failures, worker backlog, and abnormal lookup volume.
- [ ] Write support runbooks for key rotation, import recovery, duplicate handling, mistaken activation, return, and replacement.

**Acceptance:** support can diagnose and correct lifecycle issues without database edits or exposing identifiers in telemetry.

### WTY-010 — Production verification and controlled rollout

**Depends on:** WTY-001 through WTY-009

- [ ] Run complete backend/frontend/E2E suites from a clean environment.
- [ ] Run migration empty/upgrade tests, dependency/security/license audit, and secret scan.
- [ ] Inspect representative PostgreSQL SQL/plans for lookup, account list, admin search, and imports; add indexes only from evidence.
- [ ] Run multi-instance concurrency tests for activation and workers with the shared data-protection/key strategy.
- [ ] Verify backup/restore of warranty metadata and identifier key-rotation rehearsal.
- [ ] Enable flags in stages: schema → admin/plans → unit import/assignment → customer activation → public lookup.
- [ ] Record test counts, migration IDs, scan results, performance evidence, smoke results, rollback/forward-fix steps, and owners for accepted findings.

**Acceptance:** clean-environment smoke flows pass; no unresolved critical/high finding lacks an owner/date; rollback/forward-fix and key recovery are rehearsed.

## 10. Test matrix

| Layer | Required coverage |
|---|---|
| Domain | State transitions, component date math, 60-day exact boundary, leap/date handling, immutable snapshots, return/replacement |
| Application | Ownership, paid/COD rules, order states, idempotency, quantity assignment, normalization, masking |
| Infrastructure | HMAC/encryption/key version, PostgreSQL constraints, migration/backfill, import idempotency, email outbox |
| API integration | Roles, horizontal access, public DTO privacy, validation, 429 behavior, `no-store`, concurrency races |
| Query/performance | Bounded query count, direct projection, stable pagination, representative lookup/admin plans |
| Frontend unit | Form validation, login modal, safe return route, API states, accessible interaction |
| E2E | Import → assign → complete order → activate → email queued → account view → public lookup; returned/replaced cases |
| Security | Enumeration, malformed/oversized input, stolen serial, log redaction, CSV injection, replay, cross-customer IDs |

## 11. Initial indexes to validate, not blindly add

- Unique serialized-unit identifier fingerprint index.
- Unit indexes for `(ProductVariantId, Status)`, `OrderItemId`, and import batch.
- Unique entitlement index on `SerializedProductUnitId`.
- Entitlement indexes for `(CustomerId, ActivatedAt DESC, Id)`, `OrderId`, and activation deadline/status where an actual job/query uses them.
- Audit index on `(WarrantyEntitlementId, OccurredAt DESC, Id)`.

Capture generated SQL, rows read, latency, and `EXPLAIN (ANALYZE, BUFFERS)` on representative PostgreSQL data before and after every optional index.

## 12. Rollout and rollback strategy

- Add configuration flags: `Warranty:Enabled`, `Warranty:AdminEnabled`, `Warranty:ActivationEnabled`, and `Warranty:PublicLookupEnabled`; production defaults off during migration.
- Deploy additive schema and backend first, then admin UI and plan/unit provisioning.
- Reconcile import counts and assignment counts before enabling customer activation.
- Enable activation to an internal/customer cohort, observe errors and latency, then enable public lookup last.
- Rollback initially means disabling flags and retaining additive data. Do not drop warranty tables or decrypt/rewrite identifiers during an incident.
- Use a forward-fix for data corrections, preserving append-only audit evidence.

## 13. Definition of done

- [ ] Every purchased physical unit that can be activated has an auditable serial/IMEI-to-order-item assignment.
- [ ] Activation eligibility and coverage dates are deterministic, versioned, and snapshotted.
- [ ] Only the authenticated order owner can activate; public lookup exposes no PII or order data.
- [ ] All identifier storage, logs, traces, errors, caches, and exports satisfy the protection/redaction rules.
- [ ] Public lookup and activation have dedicated abuse controls and monitored metrics.
- [ ] All list/search operations are bounded PostgreSQL projections with stable pagination and cancellation.
- [ ] Migration, backup/restore, key rotation, rollback/forward-fix, and multi-instance concurrency are verified.
- [ ] Admin, storefront, account, notification, accessibility, and full regression suites pass.
- [ ] English API/UI documentation, `.env.example`, operational runbooks, alerts, and completion evidence are updated.

## 14. Recommended execution order

`WTY-001 → WTY-002 → WTY-003 → WTY-004 → WTY-005 → WTY-006 + WTY-007 → WTY-008 + WTY-009 → WTY-010`

WTY-006 and WTY-007 may proceed in parallel after unit provisioning is stable. WTY-008 may begin with mocked contracts after the shared API types are approved, but production integration waits for both APIs.

## 15. Assessment notes

- The initial assessment was based on source and repository inspection; subsequent implementation and verification evidence is recorded in section 16.
- The current worktree already contained unrelated changes and a reorganized `task/` directory; they were intentionally left untouched.
- The original assessment did not run a full suite or deployed-topology smoke. Those are now explicit WTY-010 release-gate requirements rather than evidence of completion.

## 16. Implementation progress ledger (2026-08-11)

The assessment above is retained as the original design baseline. This ledger is the current execution status and supersedes the unchecked historical checkboxes where implementation is recorded below.

| Workstream | Status | Delivered evidence | Remaining release work |
|---|---|---|---|
| WTY-001 policy / ADR | Implemented with a release sign-off gate | ADR `docs/adr/005-warranty-activation-and-identifier-protection.md` locks authenticated internal-order activation, COD/paid date rules, 60-day configurable deadline, public DTO privacy, and replacement carry-forward behavior. | Product owner must formally approve these contractual rules and identifier retention policy before enabling flags. |
| WTY-002 domain | Implemented | New warranty aggregates, immutable coverage snapshots, state guards, audit events, HMAC identifier abstraction, and application services. | Broaden domain test matrix for every terminal/return state and date edge case. |
| WTY-003 schema / migration | Implemented and verified | Additive `warranty` schema; `orders.completed_at`; deterministic earliest-Completed history backfill; unique identifier and entitlement constraints; migration `20260811110854_AddWarrantyActivation`. | Capture production-scale backfill ambiguity and query-plan evidence before rollout. |
| WTY-004 plans | Implemented | Admin APIs and UI create/list/retire versioned plans and map effective plans to variants. Existing entitlements use immutable coverage snapshots. | Add full API and browser test coverage for authorization and effective-date conflict cases. |
| WTY-005 units / assignment | Implemented | Bounded 2 MB / 10,000-row CSV dry-run + commit, checksum idempotency, HMAC fingerprints only, masked output, duplicate checks, effective-plan validation, and transaction/quantity-protected assignment. | Add explicit large-file, CSV formula, cancellation, and multi-writer stress tests; replace manual order-item UUID entry with a fulfillment selector. |
| WTY-006 customer activation | Implemented | Authenticated owner check, unit/order locks, completion and deadline checks, idempotent retries, coverage snapshots, audit event, durable customer-email outbox, and account list/detail. | Add prepaid/COD and concurrent activation load tests. |
| WTY-007 public lookup | Implemented | Body-only exact lookup, no-store response, HMAC lookup, generic malformed/unknown response, minimal PII-free DTO, telemetry redaction, and dedicated IP throttling. | Add enumeration/load tests and decide whether a CAPTCHA provider is needed after observed abuse. |
| WTY-008 storefront | Implemented (manual-entry MVP) | English `/warranty` portal, policy link, login-required activation dialog with safe return path, responsive account warranty page, and manual fallback. | Optional camera/barcode enhancement plus accessibility/browser E2E tests. |
| WTY-009 lifecycle / ops | Partially implemented | Admin registration search/detail, audit timeline, manual activation, mandatory-reason void/replacement, low-cardinality metrics, config matrix, and warranty runbook. | Wire production dashboards/alerts, add an explicit return-review workflow, and rehearse key rotation from the approved identifier source. |
| WTY-010 release gate | Not complete | Focused builds, warranty unit/infrastructure tests, API integration tests, and clean/upgrade migration verification pass. | Run complete suites, security/license scans, PostgreSQL plan capture, multi-instance race/worker smoke, backup/restore, key-rotation rehearsal, and staged production enablement. |

### Implementation details added

- Raw serial/IMEI is never persisted. The `warranty.serialized_product_units` table stores only a masked form and versioned HMAC fingerprint. `Warranty:IdentifierHmacKeys` supports temporary dual-read during key rotation; its values must come only from the secret manager.
- All Warranty feature flags remain `false` in committed configuration. Startup rejects enabled warranty features without a non-placeholder 32+ character HMAC secret.
- The public lookup and customer activation routes have dedicated rate-limit partitions; telemetry redacts `serial`, `imei`, and `identifier` names/assignments.
- The activation flow was tested through the API against PostgreSQL for successful owner activation, outbox enqueue, non-owner non-disclosure, public response privacy, and `Cache-Control: no-store`.

### Verification recorded

- `dotnet build src/WorkspaceEcommerce.Api/WorkspaceEcommerce.Api.csproj --configuration Release --no-restore` passed.
- Warranty application tests: 3 passed. Warranty infrastructure/model/HMAC tests: 10 passed.
- Warranty API integration tests (PostgreSQL Testcontainers): 2 passed.
- `scripts/verify-prh-009-migrations.ps1` passed both a clean database and upgrade from `20260802034719_AddShipmentIntegration` to `20260811110854_AddWarrantyActivation`.
- Storefront and Admin production builds pass. The frontend build emits the pre-existing Vite large-chunk warning only.

### Do not enable in production until

1. The business owner accepts the ADR purchase-date, coverage-start, return, and replacement rules.
2. The platform owner installs the non-placeholder HMAC key in the secret manager and rehearses the documented dual-read rotation/recovery path.
3. Plans, variant assignments, and physical unit/order-item counts are reconciled in the production candidate database.
4. WTY-010 verification is completed and its evidence is attached to the release record.
