import { expect, test } from "@playwright/test";

test.describe("isolated seeded storefront smoke", () => {
  test("browses the seeded catalog and adds a product to a fresh guest cart", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByRole("heading", { name: "New Arrivals" })).toBeVisible();
    await expect(page.getByText("Atlas Standing Desk").first()).toBeVisible();

    await page.goto("/products/atlas-standing-desk");
    await expect(page.getByRole("heading", { name: "Atlas Standing Desk" })).toBeVisible();

    const oakVariant = page.getByRole("radio", { name: /^Oak \/ 140cm/ });
    await oakVariant.click();
    await expect(oakVariant).toHaveAttribute("aria-checked", "true");

    await page.getByRole("button", { name: "Add to cart" }).click();

    const cart = page.getByRole("dialog", { name: "Cart" });
    await expect(cart).toBeVisible();
    await expect(cart.getByText("Atlas Standing Desk")).toBeVisible();
  await expect(cart.getByRole("heading", { name: "Oak / 140cm" })).toBeVisible();
  });
});
