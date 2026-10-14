# Order receipts

The order receipt feature produces a customer-facing PDF from the immutable data snapshots stored on an order. It is an order confirmation/proof-of-purchase document only. It is not a VAT invoice and must not be used for tax declaration.

## Access rules

- Guest: `GET /api/orders/lookup/receipt?orderCode=...&phone=...`; both values must match the order.
- Customer: `GET /api/customer/orders/{id}/receipt`; the authenticated customer must own the order.
- Admin: `GET /api/admin/orders/{id}/receipt`; an Admin bearer token is required.

All successful responses use `application/pdf` with an attachment file name. Responses contain customer data and therefore set `Cache-Control: private, no-store`. Guest receipt generation has a dedicated per-IP production rate limit.

## Rendering and configuration

PDFs are generated on demand and are not persisted, so there is no receipt migration or stale duplicate document to manage. Labels follow the request's `Accept-Language` header (`vi` or `en`). Amounts use the order currency snapshot and are rendered as whole VND values.

Configure branding in `OrderReceipt`:

```json
{
  "OrderReceipt": {
    "SellerName": "Workspace Ecommerce",
    "SupportEmail": "support@example.com",
    "SupportPhone": "1900 000 000",
    "QuestPdfLicense": "Community"
  }
}
```

QuestPDF requires an explicit license declaration. Before production deployment, confirm that the business qualifies for the configured `Community`, `Professional`, or `Enterprise` license. An unsupported value fails at startup instead of silently selecting another license.

## Deliberate exclusions

- No tax/VAT fields, tax authority code, legal invoice number, or digital signature.
- No internal order notes or administrator-only status-history notes.
- No editing or reissuing workflow: regenerating a receipt simply renders the current order snapshot and status.
