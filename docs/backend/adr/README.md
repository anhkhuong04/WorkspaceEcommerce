# Architecture decision records

ADRs capture decisions whose rationale cannot be recovered reliably from the
current code. Accepted records remain historical evidence; supersede them with a
new record or an explicit note instead of silently rewriting the original context.

| ADR | Decision | Status |
| --- | --- | --- |
| [001](001-direct-dbcontext.md) | Direct EF/DbContext querying in Application | Accepted |
| [002](002-customer-totp-authentication.md) | TOTP enrollment, login challenge, recovery, and secret protection | Accepted |
| [003](003-customer-account-lifecycle.md) | Verification/reset, refresh rotation, and browser session model | Accepted |
| [004](004-durable-media-storage.md) | Durable object storage and media lifecycle | Accepted |
| [005](005-warranty-activation-and-identifier-protection.md) | Warranty ownership, eligibility, snapshots, and HMAC identifiers | Accepted |
| [006](006-durable-external-side-effects.md) | Transactional outbox/inbox and at-least-once provider work | Accepted |
