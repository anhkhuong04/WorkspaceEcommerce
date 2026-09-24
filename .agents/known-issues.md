# Known issues and protected areas

Snapshot: 2026-09-22. Re-verify before acting; this is context, not authorization to expand scope.

## Open risks

- Application rate limiting remains per process. Multi-replica production requires shared edge/distributed enforcement and topology evidence; do not claim cluster-wide quotas from local middleware tests.

## Documentation/tooling drift

- `overview.md` may be absent; former agent files incorrectly treated it as sole contract.
- Storefront has Vitest/Playwright; Admin has no unit-test script.

## Do not refactor opportunistically

- Direct EF in Application is accepted (ADR 001); narrow `IAppDbContext` incrementally, never via a repository rewrite.
- Split large order/shipment/warranty services only in a scoped, behavior-preserving task with tests.
- Do not rewrite historical migrations or bulk-rename schemas/contracts.
- Do not bypass/replace outboxes, refresh rotation, Data Protection, media lifecycle, warranty HMAC identifiers, or signature checks as cleanup.
- Do not bulk-replace clocks, DTO styles, routes, response envelopes, frontend state, or localization in unrelated work.
- Never hand-edit/commit build output or test/scan artifacts unless explicitly requested.
