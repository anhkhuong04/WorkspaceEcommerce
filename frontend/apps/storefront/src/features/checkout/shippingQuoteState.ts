import type { ShippingQuoteDto } from "@workspace-ecommerce/api-types";
export function isShippingQuoteCurrent(
  quote: ShippingQuoteDto | undefined,
  now: number,
): quote is ShippingQuoteDto {
  return (
    !!quote &&
    quote.currency === "VND" &&
    Number.isFinite(quote.customerCharge) &&
    quote.customerCharge >= 0 &&
    Date.parse(quote.expiresAtUtc) > now
  );
}
export function shippingContextKey(
  input: unknown,
  cartSignature: string,
  subject: string,
): string {
  return JSON.stringify([input, cartSignature, subject]);
}
