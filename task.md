# Implementation Plan - Third-Party Shipment Status Integration

> Updated on 2026-08-02. Previous admin media upload and bulk order import checklist was completed and replaced by this shipment tracking integration plan.

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
- The Sandbox runtime requires the `ml_test_` API key prefix. The previous `ml_demo_` default was rejected, so Docker defaults and partner documentation now use `ml_test_demo_partner_key_123456`.
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
