import { expect, test, type Page } from "@playwright/test";
const id = "11111111-1111-4111-8111-111111111111";
async function setup(page: Page, retry = false, holdExternal?: Promise<void>) {
  await page.addInitScript(() =>
    sessionStorage.setItem(
      "workspace-ecommerce-customer-session",
      JSON.stringify({
        accessToken: "synthetic-token",
        tokenType: "Bearer",
        expiresAt: "2099-01-01T00:00:00Z",
        customerId: "11111111-1111-4111-8111-111111111111",
        email: "buyer@example.test",
        fullName: "Synthetic Buyer",
        phoneNumber: "0900000000",
      }),
    ),
  );
  const requests: unknown[] = [];
  const item = {
    id,
    productVariantId: id,
    productId: id,
    productName: "Synthetic product",
    productSlug: "synthetic",
    variantName: "Standard",
    sku: "SYNTHETIC",
    quantity: 1,
    unitPrice: 90000,
    lineTotal: 90000,
    imageUrl: null,
    requiresInstallation: false,
  };
  const order = {
    id,
    orderCode: "ORD-SHIPPING-UI",
    customerId: null,
    customerName: "Synthetic Buyer",
    customerPhone: "0900000000",
    customerEmail: null,
    shippingAddress: "1 Synthetic Street",
    note: null,
    couponId: null,
    couponCodeSnapshot: null,
    couponNameSnapshot: null,
    subtotal: 90000,
    shippingFee: 0,
    discountAmount: 0,
    totalAmount: 90000,
    status: 0,
    paymentMethod: 0,
    paymentStatus: 0,
    paidAt: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    trackingCode: null,
    shipmentId: null,
    items: [
      {
        ...item,
        productNameSnapshot: item.productName,
        productImageUrlSnapshot: null,
        skuSnapshot: item.sku,
      },
    ],
  };
  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (!path.startsWith("/api/")) return route.continue();
    const ok = (data: unknown) =>
      route.fulfill({
        json: { success: true, data, errors: [], traceId: "synthetic" },
      });
    if (path.endsWith("/capabilities"))
      return ok({
        enabled: true,
        workspaceEnabled: true,
        externalEnabled: true,
      });
    if (path === "/api/customer/me")
      return ok({
        id,
        email: "buyer@example.test",
        fullName: "Synthetic Buyer",
        phoneNumber: "0900000000",
        isEmailVerified: true,
        createdAt: new Date().toISOString(),
      });
    if (path === "/api/cart")
      return ok({
        id,
        sessionId: "synthetic",
        customerId: null,
        totalAmount: 90000,
        totalQuantity: 1,
        items: [item],
      });
    if (path.endsWith("/shipping/options"))
      return ok([
        { method: 0, available: true, reason: null },
        { method: 1, available: true, reason: "QuoteRequired" },
      ]);
    if (path.endsWith("/shipping/quotes")) {
      const input = route.request().postDataJSON();
      if (input.method === 1) {
        if (holdExternal) await holdExternal;
        else
          return route.fulfill({
            status: 409,
            json: {
              success: false,
              data: null,
              errors: ["Synthetic unavailable"],
              traceId: "synthetic",
            },
          });
      }
      return ok({
        id: input.method === 0 ? id : "external-offer",
        method: input.method,
        customerCharge: input.method === 0 ? 0 : 33000,
        currency: "VND",
        weightGrams: 1000,
        expiresAtUtc: new Date(Date.now() + 120000).toISOString(),
        ratePlanId: input.method === 0 ? id : null,
      });
    }
    if (path === "/api/checkout") {
      requests.push(route.request().postDataJSON());
      if (retry && requests.length === 1)
        return route.fulfill({
          status: 500,
          json: {
            success: false,
            data: null,
            errors: ["Synthetic response lost"],
            traceId: "synthetic",
          },
        });
      return ok({ order, paymentRequired: false, paymentUrl: null });
    }
    if (path.endsWith("/orders/lookup")) return ok({ order });
    if (path.endsWith("/refresh"))
      return route.fulfill({
        status: 401,
        json: { success: false, errors: ["Unauthenticated"], data: null },
      });
    if (path.endsWith("/tracking"))
      return ok({
        timeline: [],
        canRefresh: false,
        canCancel: false,
        canRetry: false,
      });
    return ok([]);
  });
  await page.goto("/checkout");
  await page.getByLabel("Full name", { exact: true }).fill("Synthetic Buyer");
  await page.getByLabel("Phone number", { exact: true }).fill("0900000000");
  await page.getByLabel("Address", { exact: true }).fill("1 Synthetic Street");
  await page.getByLabel("Province/City", { exact: true }).selectOption("01");
  await page.getByLabel("Ward/Commune", { exact: true }).selectOption("00004");
  await expect(
    page.getByRole("radio", { name: "WorkspaceEC", exact: true }),
  ).toBeVisible();
  return requests;
}
test("explicit zero offer, external outage and changed address never silently choose a carrier", async ({
  page,
}) => {
  await setup(page);
  const submit = page.getByRole("button", {
    name: "Place order (COD)",
    exact: true,
  });
  await expect(submit).toBeDisabled();
  await page.getByRole("radio", { name: "MiniLogistics", exact: true }).check();
  await expect(
    page.getByText(/Could not confirm this delivery price/),
  ).toBeVisible();
  await expect(
    page.getByRole("radio", { name: "MiniLogistics", exact: true }),
  ).toBeChecked();
  await expect(submit).toBeDisabled();
  await page.getByRole("radio", { name: "WorkspaceEC", exact: true }).check();
  await expect(page.getByText("0 VND", { exact: true })).toBeVisible();
  await page
    .getByRole("checkbox", { name: "I accept this shipping price" })
    .check();
  await expect(submit).toBeEnabled();
  await page.getByLabel("Address", { exact: true }).fill("99 Changed Street");
  await expect(submit).toBeDisabled();
  await expect(
    page.getByRole("radio", { name: "WorkspaceEC", exact: true }),
  ).not.toBeChecked();
});
test("a late external response cannot replace the selected local quote", async ({
  page,
}) => {
  let release!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await setup(page, false, gate);
  const request = page.waitForRequest(
    (r) =>
      r.url().includes("/shipping/quotes") && r.postDataJSON()?.method === 1,
  );
  await page.getByRole("radio", { name: "MiniLogistics", exact: true }).check();
  await request;
  await page.getByRole("radio", { name: "WorkspaceEC", exact: true }).check();
  await expect(page.getByText("0 VND", { exact: true })).toBeVisible();
  const response = page.waitForResponse(
    (r) =>
      r.url().includes("/shipping/quotes") &&
      r.request().postDataJSON()?.method === 1,
  );
  release();
  await response;
  await expect(
    page.getByRole("radio", { name: "WorkspaceEC", exact: true }),
  ).toBeChecked();
  await expect(page.getByText("0 VND", { exact: true })).toBeVisible();
});
test("quote expiry blocks acceptance until a new quote is confirmed", async ({
  page,
}) => {
  await page.clock.install();
  await setup(page);
  await page.getByRole("radio", { name: "WorkspaceEC", exact: true }).check();
  await page
    .getByRole("checkbox", { name: "I accept this shipping price" })
    .check();
  await expect(
    page.getByRole("button", { name: "Place order (COD)", exact: true }),
  ).toBeEnabled();
  await page.clock.fastForward(121000);
  await expect(page.getByText(/This quote has expired/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Place order (COD)", exact: true }),
  ).toBeDisabled();
});
test("uncertain checkout retries the same accepted quote and payload", async ({
  page,
}) => {
  const requests = await setup(page, true);
  await page.getByRole("radio", { name: "WorkspaceEC", exact: true }).check();
  await page
    .getByRole("checkbox", { name: "I accept this shipping price" })
    .check();
  await page
    .getByRole("button", { name: "Place order (COD)", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Retry the same order request", exact: true })
    .click();
  await expect(page).toHaveURL(/checkout\/success/);
  expect(requests).toHaveLength(2);
  expect(requests[1]).toEqual(requests[0]);
  expect(requests[0]).toMatchObject({
    shippingMethod: 0,
    shippingQuoteId: id,
    shippingProvinceCode: "01",
    shippingWardCode: "00004",
  });
});
