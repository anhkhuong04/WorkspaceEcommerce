import { describe, expect, it } from "vitest";
import {
  ShippingMethod,
  type ShippingQuoteDto,
} from "@workspace-ecommerce/api-types";
import {
  isShippingQuoteCurrent,
  shippingContextKey,
} from "./shippingQuoteState";
const quote: ShippingQuoteDto = {
  id: "synthetic",
  method: ShippingMethod.WorkspaceEC,
  customerCharge: 0,
  currency: "VND",
  weightGrams: 1000,
  expiresAtUtc: "2026-11-01T00:10:00Z",
  ratePlanId: "synthetic-plan",
};
describe("checkout shipping offers", () => {
  it("accepts an explicit server zero price but rejects unknown, invalid and foreign-currency prices", () => {
    expect(
      isShippingQuoteCurrent(quote, Date.parse("2026-11-01T00:00:00Z")),
    ).toBe(true);
    for (const invalid of [
      undefined,
      { ...quote, customerCharge: -1 },
      { ...quote, customerCharge: NaN },
      { ...quote, currency: "USD" },
    ])
      expect(isShippingQuoteCurrent(invalid, 0)).toBe(false);
  });
  it("expires exactly at the deadline", () => {
    expect(
      isShippingQuoteCurrent(quote, Date.parse(quote.expiresAtUtc) - 1),
    ).toBe(true);
    expect(isShippingQuoteCurrent(quote, Date.parse(quote.expiresAtUtc))).toBe(
      false,
    );
  });
  it("separates address, payment, cart and customer contexts", () => {
    const input = { street: "1 Synthetic Street", payment: 0 };
    const original = shippingContextKey(input, "cart:1", "guest");
    expect(
      shippingContextKey(
        { ...input, street: "2 Changed Street" },
        "cart:1",
        "guest",
      ),
    ).not.toBe(original);
    expect(
      shippingContextKey({ ...input, payment: 2 }, "cart:1", "guest"),
    ).not.toBe(original);
    expect(shippingContextKey(input, "cart:2", "guest")).not.toBe(original);
    expect(shippingContextKey(input, "cart:1", "customer")).not.toBe(original);
  });
});
