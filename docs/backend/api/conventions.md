# API conventions

## Routes and transport

- Public/storefront routes use `/api/...`; authenticated customer routes use
  `/api/customer/...`; admin routes use `/api/admin/...`; provider callbacks use
  `/api/webhooks/...` or the provider-specific payment path.
- Use resource nouns and HTTP semantics already established by neighboring
  controllers. Preserve existing routes unless a versioned compatibility plan
  exists.
- JSON uses ASP.NET Core web defaults. `Accept-Language` beginning with `vi`
  selects Vietnamese localized fields/receipt labels; all other values use English.

Major route groups are intentionally discoverable through Development OpenAPI:

| Area | Route families |
| --- | --- |
| Storefront content | `/api/categories`, `/api/products`, `/api/banners`, `/api/blog-posts`, product reviews |
| Commerce | `/api/cart`, `/api/checkout`, `/api/orders/lookup`, `/api/payments` |
| Customer | `/api/customer/auth`, `/api/customer/me`, `/api/customer/addresses`, `/api/customer/orders` |
| Loyalty/warranty | `/api/loyalty`, `/api/customer/warranties`, `/api/warranties/lookup` |
| Administration | `/api/admin/*` for catalog, content, coupons, orders, reviews, warranties, media, dashboard, and outbox operations |
| Provider callbacks | `/api/payments/vnpay/*`, `/api/webhooks/minilogistics` |

## Response envelope

Normal JSON responses use:

```json
{
  "success": true,
  "data": {},
  "errors": [],
  "traceId": "..."
}
```

Application `Result` statuses map consistently: validation `400`, unauthorized
`401`, not found `404`, conflict `409`, unexpected failure `500`. Model-binding
errors and global exceptions also return the failure envelope without leaking
internals.

Intentional exceptions are health probes, VNPay redirect/IPN responses,
MiniLogistics webhook acknowledgements, and PDF receipt file responses. Do not
force these provider/platform contracts into the envelope.

## Authentication and ownership

- Admin and Customer bearer tokens carry different roles. Customer access also
  filters by the authenticated customer ID; a GUID alone is never authorization.
- Guest order/receipt/tracking lookup requires order code plus phone. New
  anonymous endpoints exposing customer or transaction data need an equivalent
  possession proof and rate limit.
- Customer refresh credentials are sent only by the scoped `HttpOnly` cookie.
  CORS must allow credentials only for exact configured origins.
- Webhooks authenticate the raw request before JSON business processing. Never
  log authorization headers, cookies, tokens, signatures, or request bodies.

## Validation, errors, and concurrency

- Transport shape/length rules belong in FluentValidation or model binding;
  business invariants belong in domain/application code.
- Return actionable public errors, but keep account-existence, ownership,
  security material, provider bodies, and exception details non-disclosing.
- Use `409 Conflict` for stale/concurrent or illegal current-state changes, not
  for malformed syntax.
- Write endpoints that providers or workers may retry must have an idempotency
  key, durable deduplication, or a proven terminal-state check.

## Paging and files

Paged endpoints use one-based `pageNumber` and bounded `pageSize`; the shared
default is 20 and maximum is 100 unless a narrower endpoint limit is documented.
Return `PagedResult<T>` metadata rather than pagination headers.

PDF receipts are generated on demand, returned as `application/pdf` attachments,
and marked `Cache-Control: private, no-store`. Upload endpoints keep explicit
request-size limits and route all media through `IMediaStorageService`.

Development exposes `/openapi/v1.json`; production does not expose runtime
OpenAPI. Any public contract change must update shared frontend API types,
consumers, integration tests, and this document when it changes a convention.
