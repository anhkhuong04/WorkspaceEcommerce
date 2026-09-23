# Known issues and protected areas

Snapshot: 2026-09-22. Re-verify before acting; this is context, not authorization to expand scope.

## Open risks

- Integration tests resolve vulnerable transitive `SSH.NET 2025.1.0` (`GHSA-q939-rpr3-3284`) through Testcontainers and emit `NU1903`.
- Global rate limiting runs before authentication, so its warranty customer claim is anonymous; state is per process/IP.
- Cart DTO/checkout have per-item queries; VNPay checkout builds snapshots twice. Do not copy these query shapes.
- Account cleanup materializes every expired row before deletion. Do not extend this unbounded worker pattern.
- Email config permits Log outside literal Production and does not require SMTP TLS outside Development.
- Admin warranty activation lacks the locked transaction used by customer activation. Warranty remains disabled by default.

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
