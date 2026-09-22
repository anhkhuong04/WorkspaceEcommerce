# Currency and money convention

WorkspaceEcommerce uses Vietnamese dong (`VND`) as its commerce currency.

## Storage

- Store actual VND amounts. For example, `18_174_000` means eighteen million
  one hundred seventy-four thousand dong.
- Never store a USD amount and rely on the UI to multiply it by an exchange rate.
- Domain and application code use C# `decimal`; PostgreSQL uses exact
  `numeric(18,2)`. VND values are currently whole dong even though the database
  scale supports provider reconciliation and possible future currency work.
- TypeScript transport models use `number`. Revisit this before values can
  approach `Number.MAX_SAFE_INTEGER`.

## Presentation and orders

- Shared formatters default to `vi-VN`/`VND`, display zero fractional digits,
  and never perform conversion.
- UI language does not determine currency.
- Catalog, cart, order, payment, discount, and shipping amounts are actual VND.
  New orders snapshot currency `VND` and exchange rate `1`.
