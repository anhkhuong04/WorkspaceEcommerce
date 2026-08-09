# ADR 002: Customer TOTP authentication

## Status

Accepted

## Context

The earlier customer two-factor endpoint generated a random-looking value and immediately set an enabled flag. It neither proved that an authenticator had enrolled nor participated in login, so it was a demo mechanism rather than an authentication control.

## Decision

- Use `Otp.NET` 1.4.1 for RFC 6238-compatible TOTP with a 20-byte cryptographically generated secret, Base32 representation, 30-second period, and six digits.
- Accept the current TOTP period plus one period before and after it (`VerificationWindow.RfcSpecifiedNetworkDelay`). Persist the accepted time step and reject any equal or earlier step, including during the permitted clock-drift window.
- Protect active and pending shared secrets with ASP.NET Core Data Protection before persistence. PostgreSQL receives only protected payloads; its backups alone cannot recover secrets.
- Require `DataProtection__KeyRingPath` in Production. It must be a mounted, access-controlled path outside the repository and database, shared by all API instances, backed up, and retained during rolling deploys. Loss of this key ring makes existing authenticators unrecoverable and requires re-enrollment.
- Setup creates an encrypted pending secret that expires after 10 minutes. It becomes active only after a valid TOTP code confirms enrollment.
- Generate ten recovery codes from 128 bits of cryptographic randomness. Return them once at setup confirmation; persist only password-hash outputs and mark a code used atomically on successful recovery login.
- Primary password and Google authentication issue a 5-minute, single-use challenge rather than a JWT when 2FA is active. The random challenge token is returned only to the client; only its SHA-256 hash is stored.
- Disabling 2FA requires an unused recovery code or a current, not-yet-replayed TOTP code and revokes all stored 2FA material.
- Rate-limit setup and challenge verification under dedicated partitions. Secrets, OTPs, recovery codes, and challenge token values are never logged or included in errors.

## Consequences

- The migration clears every prior simulated 2FA state, so any customer who had toggled the demo feature must re-enroll. This is intentional: those records are not confirmed authenticators and are not Data Protection payloads.
- Deploying two or more API instances requires a shared key ring with least-privilege filesystem permissions. An ephemeral container filesystem is not valid production configuration.
- Recovery codes are an emergency access path; their one-time display means support cannot retrieve them later. A new setup cycle produces a new set.
- This change does not add refresh tokens, email recovery, device trust, or a cleanup job for expired challenges. Those lifecycle concerns remain in PRH-005.
