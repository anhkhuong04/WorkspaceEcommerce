# ADR 003: Customer account lifecycle and browser session model

## Status

Accepted on 2026-08-09.

## Decision

- Email/password sign-up creates an unverified account and queues a verification email through the durable `customer.email_outbox` table. The request transaction does not call an email provider.
- Verification and password-reset credentials are 256-bit random URL-safe values. Only their SHA-256 hashes are stored in `customer.account_tokens`; they expire and are consumed once. Sending another credential invalidates outstanding credentials of the same purpose.
- Customers **may checkout before email verification**. This is deliberate: checkout already permits guests, so using verification as a gate would create an inconsistent and avoidable purchase blocker. Verification remains required for the account-management trust signal, and the decision is enforced by keeping checkout free of an email-verification precondition.
- Email verification and password reset requests return the same successful envelope whether an eligible account exists. This prevents account enumeration.
- Each completed primary authentication (password, Google, or TOTP/recovery challenge) creates a refresh-token family. Refresh values are 256-bit random, stored only as hashes, and rotate on each use. A used refresh token is replay evidence: the whole family is revoked with reason `refresh_token_reuse`.
- A refresh request holds the presented refresh-token row with PostgreSQL `FOR UPDATE`. A concurrent second request waits, then observes the now-used token and revokes its family. This avoids a race in which two refresh responses can be issued from one credential.
- Resetting a password consumes all outstanding reset credentials and revokes every refresh-token family. Changing a password does the same, and `logout-all` offers an explicit user action to revoke all families.
- Access tokens retain the existing customer role and ownership claims. They remain short-lived according to `Jwt:AccessTokenMinutes`; refresh credential lifetime is independently limited by `CustomerAccountLifecycle:RefreshTokenLifetimeDays`.

## Browser storage strategy

- The API copies the refresh value into the `workspace_ecommerce_refresh` cookie with `HttpOnly`, `SameSite=Strict`, a path limited to `/api/customer/auth`, and `Secure` outside Development. It never serializes the raw refresh value in the JSON response.
- The storefront keeps only the access token and its public profile data in `sessionStorage`, scoped to the current tab. It renews about one minute before access-token expiry and tries a cookie-backed renewal during application restoration.
- CORS explicitly allows credentials only for configured storefront origins. A deployment using different sites must use HTTPS and a same-site origin arrangement compatible with `SameSite=Strict`.

## Delivery and retention

- Email payloads are Data Protection protected before persistence. The outbox worker decrypts them only immediately before delivery and never logs message bodies or raw account credentials.
- Development can use the metadata-only `Log` delivery provider. Production startup rejects it; SMTP configuration is required. Provider failure leaves the outbox row pending with bounded exponential retry.
- Daily cleanup removes account/refresh tokens, delivered email records, and expired 2FA challenges after 7 days; used recovery codes after 7 days; and login history after 90 days. Unsent outbox records are retained for retry.
