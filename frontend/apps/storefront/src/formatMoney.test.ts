import { describe, expect, it } from "vitest";
import { formatMoney } from "@workspace-ecommerce/shared-utils";

describe("formatMoney", () => {
  it("formats stored values as Vietnamese dong by default", () => {
    const formatted = formatMoney(18_174_000);

    expect(formatted).toContain("18.174.000");
    expect(formatted).toContain("₫");
  });

  it("does not apply an implicit exchange rate", () => {
    expect(formatMoney(699, "USD")).toBe("$699.00");
  });
});
