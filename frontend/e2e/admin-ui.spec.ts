import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import type { OrderReturnSummaryDto } from "@workspace-ecommerce/api-types";
import {
  dashboard,
  distantProduct,
  mockAdmin,
  product,
  order,
  paged,
} from "./admin-ui.fixtures";
const screens = [
  ["/", "Dashboard"],
  ["/products", "Products"],
  ["/categories", "Categories"],
  ["/orders", "Orders"],
  ["/coupons", "Coupons"],
  ["/banners", "Banners"],
  ["/blogs", "Articles"],
  ["/reviews", "Reviews"],
  ["/operations", "Operations"],
  ["/warranties", "Warranties"],
] as const;

test("global search works with keyboard and opens the matching order", async ({ page }) => {
  await mockAdmin(page); await page.setViewportSize({ width: 390, height: 844 }); await page.goto("/");
  await page.keyboard.press("Control+k");
  const modal = page.getByRole("dialog", { name: "Search your store" });
  await expect(modal.getByLabel("Search admin")).toBeFocused();
  await modal.getByLabel("Search admin").fill("desk");
  await expect(modal.getByRole("link", { name: new RegExp(order.orderCode) })).toBeVisible();
  await assertNoOverflow(page); await assertAccessible(page);
  await modal.getByRole("link", { name: new RegExp(order.orderCode) }).click();
  await expect(page).toHaveURL(/orderId=ui-order/);
  await expect(page.getByRole("dialog", { name: `Order ${order.orderCode}`, exact: true })).toBeVisible();
});

test("product filters preserve URL state and submit localized edit payloads", async ({ page }) => {
  const writes = await mockAdmin(page); await page.goto("/products");
  await page.getByLabel("Search products", { exact: true }).fill("DESK-OAK");
  await page.getByLabel("Visibility", { exact: true }).selectOption("true");
  await page.getByLabel("Inventory", { exact: true }).selectOption("low");
  await page.getByLabel("Minimum price (VND)", { exact: true }).fill("100");
  await page.getByRole("button", { name: "Apply filters" }).click();
  await expect(page).toHaveURL(/search=DESK-OAK/); await expect(page).toHaveURL(/isActive=true/); await expect(page).toHaveURL(/stock=low/);
  await page.reload(); await expect(page.getByLabel("Search products", { exact: true })).toHaveValue("DESK-OAK");
  await page.getByRole("button", { name: `Edit ${product.name.en}`, exact: true }).click();
  const editor = page.getByRole("dialog", { name: "Edit product", exact: true });
  await editor.getByLabel("Name", { exact: true }).fill("Updated primary name");
  await editor.getByRole("button", { name: "Save", exact: true }).click();
  await expect(editor.getByRole("alert")).toBeVisible();
  expect(writes.at(-1)?.body).toMatchObject({ name: { vi: "Updated primary name", en: product.name.en }, description: product.description });
});

test("a late status response for A never replaces the open order B", async ({ page }) => {
  await mockAdmin(page);
  const other = { ...order, id: "ui-order-b", orderCode: "WE-ORDER-B", customerName: "Second buyer" };
  let release = () => {}; const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/admin/orders/ui-order/status", async route => { await gate; await route.fulfill({ json: { success: true, data: { ...order, status: 2 }, errors: [] } }); });
  await page.route("**/api/admin/orders/ui-order-b**", async route => {
    if (!new URL(route.request().url()).pathname.endsWith("ui-order-b")) return route.fulfill({ json: { success: true, data: { orderId: other.id, timeline: [] }, errors: [] } });
    return route.fulfill({ json: { success: true, data: other, errors: [] } });
  });
  await page.goto("/orders?orderId=ui-order");
  const first = page.getByRole("dialog", { name: `Order ${order.orderCode}`, exact: true });
  await first.getByRole("tab", { name: "Activity" }).click();
  const started = page.waitForRequest(request => request.url().endsWith("/ui-order/status"));
  await first.getByRole("button", { name: "Update status", exact: true }).click(); await started;
  await page.keyboard.press("Escape");
  await page.evaluate(() => { window.history.pushState({}, "", "/orders?orderId=ui-order-b"); window.dispatchEvent(new PopStateEvent("popstate")); });
  const second = page.getByRole("dialog", { name: "Order WE-ORDER-B", exact: true }); await expect(second).toBeVisible();
  release(); await expect(page.getByText("Order status updated.", { exact: true })).toBeVisible();
  await expect(second).toBeVisible(); await expect(second.getByText("Second buyer", { exact: true })).toBeVisible();
  await expect(second.getByRole("heading", { name: `Order ${order.orderCode}`, exact: true })).not.toBeVisible();
});

test("article and product uploads block Save Close and Escape until settled", async ({ page }) => {
  await mockAdmin(page); let release = () => {}; let gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/admin/media", async route => { await gate; await route.fulfill({ json: { success: true, data: { url: "/demo/banner-workspace.svg" }, errors: [] } }); });
  for (const target of ["article", "product"] as const) {
    gate = new Promise<void>(resolve => { release = resolve; });
    if (target === "article") { await page.goto("/blogs"); await page.getByRole("button", { name: "New article", exact: true }).click(); }
    else { await page.goto("/products"); await page.getByRole("button", { name: `Expand ${product.name.en}`, exact: true }).click(); await page.getByRole("tab", { name: /Images/ }).click(); await page.getByRole("button", { name: "Add image", exact: true }).click(); }
    const modal = page.getByRole("dialog");
    try {
      await modal.locator('input[type="file"]').setInputFiles({ name: "ui.png", mimeType: "image/png", buffer: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==", "base64") });
      await expect(modal).toHaveAttribute("aria-busy", "true");
      await expect(modal.getByRole("button", { name: "Save", exact: true })).toBeDisabled();
      await expect(modal.getByRole("button", { name: "Cancel", exact: true })).toBeDisabled();
      await page.keyboard.press("Escape"); await expect(modal).toBeVisible();
    } finally { release(); }
    await expect(modal.getByRole("button", { name: "Save", exact: true })).toBeEnabled();
    await page.keyboard.press("Escape"); await expect(modal).not.toBeVisible();
  }
});

test("unknown admin URL has a useful recovery screen without developer details", async ({ page }) => {
  await mockAdmin(page); await page.goto("/unknown-admin-page");
  await expect(page.getByRole("heading", { name: /Page not found/i })).toBeVisible();
  await expect(page.getByText(/Unexpected Application Error|Stack trace/)).not.toBeVisible(); await assertAccessible(page);
});

test("return forms approve inspect and record verified refunds with separate confirmations", async ({ page }) => {
  await mockAdmin(page); await page.setViewportSize({ width: 390, height: 844 });
  const id = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
  const itemId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
  const writes: { path: string; body: Record<string, unknown> }[] = [];
  let summary: OrderReturnSummaryDto = { orderId: order.id, currency: "VND", canCreate: true, eligibilityMessage: null, requestedAt: null, reviewedAt: null, decision: null, decisionReason: null,
    items: [{ orderItemId: itemId, productName: "Standing desk", sku: "RETURN-SKU", quantity: 1, availableQuantity: 1, netLineAmount: 100000, units: [] }], returns: paged([]) };
  await page.route("**/api/admin/orders/ui-order/returns**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() === "POST") {
      const body = route.request().postDataJSON(); writes.push({ path, body });
      if (path.endsWith("/returns")) summary = { ...summary, canCreate: false, returns: paged([{ id, status: 0, revision: 1, reason: body.reason, createdAt: order.createdAt, receivedAt: null, merchandiseRefund: 0, shippingRefund: 0, refundAmount: 0, refundReference: null, refundedAt: null, rejectionReason: null, items: [{ orderItemId: itemId, productName: "Standing desk", sku: "RETURN-SKU", quantity: 1, receivedQuantity: 0, restockQuantity: 0, refundAmount: 0, inspection: null, serializedUnitIds: [] }], audit: [] }]) };
      else if (path.endsWith("/receive")) summary = { ...summary, returns: paged([{ ...summary.returns.items[0], status: 2, revision: 2, refundAmount: 100000, merchandiseRefund: 100000, receivedAt: order.createdAt, items: [{ ...summary.returns.items[0].items[0], receivedQuantity: 1, restockQuantity: 1, inspection: "Sellable condition", refundAmount: 100000 }] }]) };
      else if (path.endsWith("/refund")) summary = { ...summary, returns: paged([{ ...summary.returns.items[0], status: 3, revision: 3, refundReference: body.reference, refundedAt: order.createdAt }]) };
    }
    await route.fulfill({ json: { success: true, data: summary, errors: [] } });
  });
  await page.goto("/orders?orderId=ui-order"); await page.getByRole("tab", { name: "Returns", exact: true }).click();
  await page.getByRole("button", { name: "Approve return items", exact: true }).click();
  let modal = page.getByRole("dialog", { name: "Approve return items", exact: true });
  await modal.getByLabel(/up to 1/).fill("1"); await modal.getByLabel("Decision / customer message").fill("Return accepted after review");
  await modal.getByRole("button", { name: "Review return approval" }).click();
  expect(writes).toHaveLength(0); await page.getByRole("alertdialog").getByRole("button", { name: "Record operation" }).click();
  await expect(page.getByText("Awaiting receipt", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Receive and inspect", exact: true }).click();
  modal = page.getByRole("dialog", { name: "Receive and inspect goods", exact: true });
  await modal.getByLabel("Restock as sellable", { exact: true }).fill("1"); await modal.getByLabel("Inspection and disposition").fill("Sellable condition"); await modal.getByLabel("Receipt reason").fill("Goods received and counted");
  await assertNoOverflow(page); await assertAccessible(page);
  await modal.getByRole("button", { name: "Review received goods" }).click(); expect(writes).toHaveLength(1);
  await page.getByRole("alertdialog").getByRole("button", { name: "Record operation" }).click();
  await page.getByRole("button", { name: "Record verified refund", exact: true }).click();
  modal = page.getByRole("dialog", { name: "Record a completed refund", exact: true });
  await modal.getByLabel("Refund transaction reference").fill("REFUND-PROOF-001"); await modal.getByLabel("Refund evidence / reason").fill("Bank transfer verified");
  await modal.getByRole("button", { name: "Review refund record" }).click(); expect(writes).toHaveLength(2);
  await page.getByRole("alertdialog").getByRole("button", { name: "Record operation" }).click();
  await expect(page.getByText("Refund recorded", { exact: true })).toBeVisible();
  expect(writes[1].body).toMatchObject({ expectedRevision: 1, items: [{ orderItemId: itemId, receivedQuantity: 1, restockQuantity: 1 }] });
  expect(writes[2].body).toMatchObject({ amount: 100000, reference: "REFUND-PROOF-001", expectedRevision: 2 });
});
async function assertNoOverflow(page: Page) {
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth),
  ).toBeLessThanOrEqual((page.viewportSize()?.width ?? 0) + 1);
}
async function assertAccessible(page: Page) {
  const result = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(
    result.violations.map((v) => ({
      id: v.id,
      nodes: v.nodes.map((n) => n.target),
    })),
  ).toEqual([]);
}

for (const width of [1440, 768, 390, 320])
  test(`admin pages fit ${width}px and have no automated accessibility violations`, async ({
    page,
  }, info) => {
    await mockAdmin(page);
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ reducedMotion: "reduce" });
    for (const [path, title] of screens) {
      await page.goto(path);
      await expect(
        page.getByRole("heading", { name: title, exact: true, level: 1 }),
      ).toBeVisible();
      await page.waitForLoadState("networkidle");
      if (path === "/warranties")
        await expect(
          page.getByRole("heading", {
            name: "Warranty management is unavailable",
          }),
        ).toBeVisible();
      await assertNoOverflow(page);
      await assertAccessible(page);
      await page.screenshot({
        path: info.outputPath(
          `${width}-${path === "/" ? "dashboard" : path.slice(1)}.png`,
        ),
        fullPage: true,
      });
    }
  });

test("mobile navigation traps focus, closes on Escape and adapts to desktop", async ({
  page,
}) => {
  await mockAdmin(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  const trigger = page.getByRole("button", { name: "Open navigation" });
  await trigger.click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  for (let i = 0; i < 16; i++) {
    await page.keyboard.press("Tab");
    expect(
      await dialog.evaluate((el) => el.contains(document.activeElement)),
    ).toBe(true);
  }
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(trigger).toBeFocused();
  await trigger.click();
  await page.setViewportSize({ width: 1440, height: 900 });
  await expect(dialog).not.toBeVisible();
  await expect(trigger).not.toBeVisible();
  await page.getByRole("link", { name: "Products", exact: true }).click();
  await expect(page).toHaveURL(/products$/);
});

test("mobile product cards expose pricing and edit actions without horizontal scrolling", async ({
  page,
}) => {
  await mockAdmin(page);
  await page.setViewportSize({ width: 320, height: 844 });
  await page.goto("/products");
  const card = page.getByRole("article", { name: product.name.en, exact: true });
  await expect(card.getByText("6.990.000 ₫")).toBeVisible();
  const edit = card.getByRole("button", {
    name: `Edit ${product.name.en}`,
    exact: true,
  });
  const box = await edit.boundingBox();
  expect(box?.x).toBeGreaterThanOrEqual(0);
  expect((box?.x ?? 0) + (box?.width ?? 0)).toBeLessThanOrEqual(320);
  await edit.click();
  await expect(
    page.getByRole("dialog", { name: "Edit product", exact: true }),
  ).toBeVisible();
  await assertAccessible(page);
  await page.keyboard.press("Escape");
  await expect(edit).toBeFocused();
  await page.setViewportSize({ width: 1440, height: 900 });
  await expect(card).not.toBeVisible();
  await expect(
    page.getByRole("region", { name: "Products", exact: true }),
  ).toBeVisible();
});

test("product tabs, action menus, validation and API errors keep the form usable", async ({
  page,
}, info) => {
  await mockAdmin(page);
  await page.goto("/products");
  await page
    .getByRole("button", { name: `Expand ${product.name.en}`, exact: true })
    .click();
  const tabs = page.getByRole("tablist", { name: `Manage ${product.name.en}` });
  await tabs.getByRole("tab", { name: "Variants (2)" }).focus();
  await page.keyboard.press("ArrowRight");
  await expect(tabs.getByRole("tab", { name: "Images (1)" })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await expect(page.getByText("Oak desk", { exact: true })).toBeVisible();
  await assertAccessible(page);
  const trigger = page.getByRole("button", {
    name: `More actions for ${product.name.en}`,
    exact: true,
  });
  await trigger.click();
  await page.getByRole("menuitem", { name: "Add specification" }).click();
  let modal = page.getByRole("dialog");
  await expect(
    modal.getByRole("heading", { name: "New specification", exact: false }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(modal).not.toBeVisible();
  await expect(trigger).toBeFocused();
  await page.getByRole("button", { name: "New product", exact: true }).click();
  modal = page.getByRole("dialog");
  await modal.getByRole("button", { name: "Save", exact: true }).click();
  await expect(
    modal.getByText("Name is required.", { exact: true }),
  ).toBeVisible();
  await expect(modal.getByLabel("Name", { exact: true })).toHaveAttribute(
    "aria-invalid",
    "true",
  );
  await modal.getByLabel("Name", { exact: true }).fill("Draft desk");
  await modal.getByLabel("Slug", { exact: true }).fill("draft-desk");
  await modal.getByLabel("Slug", { exact: true }).press("Enter");
  await expect(modal.getByRole("alert")).toContainText("currently in use");
  await expect(modal.getByLabel("Name", { exact: true })).toHaveValue(
    "Draft desk",
  );
  await assertAccessible(page);
  await page.screenshot({
    path: info.outputPath("product-validation-error.png"),
  });
});

test("inventory links find the right product page and pagination survives Back", async ({
  page,
}) => {
  await mockAdmin(page);
  await page.goto("/");
  await page.getByRole("button", { name: "Manage DISTANT-OAK" }).click();
  await expect(page.locator(`#variant-distant-variant`)).toBeVisible();
  await expect(
    page.getByText(distantProduct.name.en, { exact: true }).first(),
  ).toBeVisible();
  await page.getByRole("button", { name: "Show all products" }).click();
  await expect(page).toHaveURL(/\/products$/);
  await page.getByRole("button", { name: "Next page" }).click();
  await expect(page).toHaveURL(/page=2/);
  await page.goBack();
  await expect(page).toHaveURL(/\/products$/);
  await expect(
    page.getByRole("button", { name: `Expand ${product.name.en}`, exact: true }),
  ).toBeVisible();
});

test("saving locks the form and keeps the dialog open until the response", async ({
  page,
}) => {
  await mockAdmin(page);
  let release = () => {};
  const responseGate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/admin/products", async (route) => {
    if (route.request().method() === "GET") return route.fallback();
    await responseGate;
    await route.fulfill({
      status: 409,
      json: { success: false, errors: ["Please try again."], data: null },
    });
  });
  await page.goto("/products");
  await page.getByRole("button", { name: "New product", exact: true }).click();
  const modal = page.getByRole("dialog", { name: "New product", exact: true });
  const name = modal.getByLabel("Name", { exact: true });
  await name.fill("Draft desk");
  await modal.getByLabel("Slug", { exact: true }).fill("draft-desk");
  try {
    await modal.getByRole("button", { name: "Save", exact: true }).click();
    await expect(modal).toHaveAttribute("aria-busy", "true");
    await expect(name).toBeDisabled();
    await expect(
      modal.getByRole("button", { name: "Close dialog" }),
    ).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(modal).toBeVisible();
  } finally {
    release();
  }
  await expect(modal.getByRole("alert")).toContainText("Please try again");
  await expect(name).toBeEnabled();
  await expect(name).toHaveValue("Draft desk");
  await page.keyboard.press("Escape");
  await expect(modal).not.toBeVisible();
});

test("coupon and article product pickers include later pages and retain selection", async ({
  page,
}) => {
  const catalog = [
    product,
    ...Array.from({ length: 99 }, (_, i) => ({
      ...product,
      id: `picker-product-${i}`,
      name: { vi: `Picker product ${i + 2}`, en: `Picker product ${i + 2}` },
    })),
    distantProduct,
  ];
  const writes = await mockAdmin(page, { productCatalog: catalog });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/coupons");
  await page.getByRole("button", { name: "Edit coupon WORK10" }).click();
  const coupon = page.getByRole("dialog", { name: "Edit WORK10", exact: true });
  await coupon
    .getByLabel("Target products", { exact: true })
    .selectOption("products");
  const search = coupon.getByLabel("Find products", { exact: true });
  await expect(search).toBeEnabled();
  await search.fill("DISTANT-OAK");
  await coupon
    .getByRole("checkbox", { name: new RegExp(distantProduct.name.en) })
    .check();
  await search.fill("Picker product 2");
  await expect(coupon.getByText("(1 selected)", { exact: true })).toBeVisible();
  await search.fill("DISTANT-OAK");
  await expect(
    coupon.getByRole("checkbox", { name: new RegExp(distantProduct.name.en) }),
  ).toBeChecked();
  await assertNoOverflow(page);
  await assertAccessible(page);
  await coupon.getByRole("button", { name: "Save", exact: true }).click();
  await expect(
    coupon.getByRole("alert").filter({ hasText: "currently in use" }),
  ).toBeVisible();
  expect(writes[0].body).toMatchObject({
    productTargetIds: [distantProduct.id],
  });
  await page.keyboard.press("Escape");
  await page.goto("/blogs");
  await page.getByRole("button", { name: "New article", exact: true }).click();
  const article = page.getByRole("dialog", {
    name: "New article",
    exact: true,
  });
  const articleSearch = article.getByLabel("Find products", { exact: true });
  await expect(articleSearch).toBeEnabled();
  await articleSearch.fill("DISTANT-OAK");
  await expect(
    article.getByRole("checkbox", { name: new RegExp(distantProduct.name.en) }),
  ).toBeVisible();
  await assertNoOverflow(page);
  await assertAccessible(page);
});

test("filters have explicit search, retain URL values and reset correctly", async ({
  page,
}) => {
  await mockAdmin(page);
  await page.goto("/orders");
  await page.getByLabel("Search orders").fill("WE-001");
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(page).toHaveURL(/search=WE-001/);
  await page.getByLabel("Order status").selectOption("4");
  await expect(page).toHaveURL(/status=4/);
  await page.getByRole("button", { name: "Reset", exact: true }).click();
  await expect(page).toHaveURL(/\/orders$/);
  await expect(page.getByLabel("Search orders")).toHaveValue("");
  await page.goto("/coupons");
  await page.getByLabel("Search coupons").fill("WORK");
  await page.getByLabel("Search coupons").press("Enter");
  await expect(page).toHaveURL(/search=WORK/);
  await page.getByRole("button", { name: "Reset", exact: true }).click();
  await expect(page.getByLabel("Search coupons")).toHaveValue("");
});

test("nested receipt and cancellation dialogs close independently and restore focus", async ({
  page,
}) => {
  await mockAdmin(page);
  await page.goto("/orders");
  await page
    .getByRole("button", { name: "View order WE-20261014-001" })
    .click();
  const drawer = page.getByRole("dialog", {
    name: "Order WE-20261014-001",
    exact: true,
  });
  await expect(drawer).toBeVisible();
  const receipt = drawer.getByRole("button", { name: "View receipt" });
  await receipt.click();
  await expect(
    page.getByRole("dialog", { name: /Receipt preview/ }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(
    page.getByRole("dialog", { name: /Receipt preview/ }),
  ).not.toBeVisible();
  await expect(drawer).toBeVisible();
  await expect(receipt).toBeFocused();
  await drawer.getByRole("tab", { name: "Delivery" }).click();
  const cancel = drawer.getByRole("button", {
    name: "Cancel shipment",
    exact: true,
  });
  await cancel.click();
  const confirm = page.getByRole("alertdialog");
  await expect(
    confirm.getByRole("button", { name: "Cancel", exact: true }),
  ).toBeFocused();
  await confirm
    .getByRole("button", { name: "Cancel shipment", exact: true })
    .click();
  await expect(confirm.getByRole("alert")).toContainText("currently in use");
  await page.keyboard.press("Escape");
  await expect(confirm).not.toBeVisible();
  await expect(drawer).toBeVisible();
  await expect(cancel).toBeFocused();
  await assertAccessible(page);
});

test("warranty actions require a reason and keep failed operations in the dialog", async ({
  page,
}) => {
  const writes = await mockAdmin(page, { warranties: true });
  await page.goto("/warranties");
  await page.getByRole("button", { name: "Void", exact: true }).click();
  const modal = page.getByRole("dialog", { name: "Void warranty" });
  await modal
    .getByRole("button", { name: "Void warranty", exact: true })
    .click();
  await expect(modal.getByRole("alert")).toContainText("reason is required");
  expect(writes.length).toBe(0);
  await modal.getByLabel("Operational reason").fill("Customer requested void");
  await modal
    .getByRole("button", { name: "Void warranty", exact: true })
    .click();
  await expect(modal.getByRole("alert")).toContainText("currently in use");
  expect(writes[0].body).toEqual({ reason: "Customer requested void" });
  await assertAccessible(page);
  await page.keyboard.press("Escape");
  await expect(modal).not.toBeVisible();
  await page.getByRole("tab", { name: "Plans & assignments" }).click();
  await page.getByRole("button", { name: "Retire plan" }).click();
  await expect(
    page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Cancel", exact: true }),
  ).toBeFocused();
});

test("dashboard empty state is concise and login failure preserves entered values", async ({
  page,
}) => {
  await mockAdmin(page, { emptyDashboard: true, authenticated: false });
  await page.goto("/login");
  await page.getByLabel("Email").fill("ui-admin@example.test");
  await page.getByLabel("Password").fill("ui-password");
  await page.getByRole("button", { name: "Login", exact: true }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByLabel("Email")).toHaveValue("ui-admin@example.test");
  await assertAccessible(page);
  await page.evaluate(() =>
    localStorage.setItem(
      "workspace-ecommerce-admin-session",
      JSON.stringify({
        accessToken: "ui-token",
        tokenType: "Bearer",
        email: "ui-admin@example.test",
        expiresAt: "2099-01-01T00:00:00Z",
      }),
    ),
  );
  await page.goto("/");
  await expect(page.getByText("No orders yet", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Recent orders", exact: true }),
  ).not.toBeVisible();
  await expect(
    page.getByText(
      `Up to 10 variants · ≤ ${dashboard.lowStockThreshold} units`,
      { exact: true },
    ),
  ).toBeVisible();
});

test("banners allow image-only artwork and keep supplied titles on edits", async ({ page }) => {
  const writes = await mockAdmin(page);
  await page.goto("/banners");
  await page.getByRole("button", { name: "New banner", exact: true }).click();
  const modal = page.getByRole("dialog", { name: "New banner", exact: true });
  await modal.getByLabel("Image", { exact: true }).fill("/demo/banner-workspace.svg");
  await modal.getByRole("button", { name: "Save", exact: true }).click();
  await expect(modal.getByRole("alert")).toContainText("currently in use");
  expect(writes[0].body).toMatchObject({ title: "", imageUrl: "/demo/banner-workspace.svg" });
  await expect(modal.getByLabel("Title (optional)")).not.toHaveAttribute("aria-invalid", "true");
  await assertAccessible(page);
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Edit Make space for better work" }).click();
  const editor = page.getByRole("dialog", { name: "Edit banner", exact: true });
  await expect(editor.getByLabel("Title (optional)")).toHaveValue("Make space for better work");
  await editor.getByLabel("Title (optional)").fill("");
  await editor.getByRole("button", { name: "Save", exact: true }).click();
  await expect(editor.getByRole("alert")).toContainText("currently in use");
  expect(writes[1].body).toMatchObject({ title: "", imageUrl: "/demo/banner-workspace.svg" });
});

test("banner uploads block premature saving and release the form after completion", async ({ page }) => {
  const writes = await mockAdmin(page);
  let release = () => {};
  const uploadGate = new Promise<void>((resolve) => { release = resolve; });
  await page.route("**/api/admin/media", async (route) => {
    await uploadGate;
    await route.fulfill({ json: { success: true, errors: [], data: { url: "/demo/banner-workspace.svg" } } });
  });
  await page.goto("/banners");
  await page.getByRole("button", { name: "New banner", exact: true }).click();
  const modal = page.getByRole("dialog", { name: "New banner", exact: true });
  try {
    await modal.locator('input[type="file"]').setInputFiles({ name: "ui.png", mimeType: "image/png", buffer: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==", "base64") });
    await expect(modal).toHaveAttribute("aria-busy", "true");
    await expect(modal.getByRole("button", { name: "Save", exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(modal).toBeVisible();
    expect(writes.length).toBe(0);
  } finally { release(); }
  await expect(modal.getByLabel("Image", { exact: true })).toHaveValue("/demo/banner-workspace.svg");
  await expect(modal.getByRole("button", { name: "Save", exact: true })).toBeEnabled();
  await modal.getByRole("button", { name: "Save", exact: true }).click();
  await expect(modal.getByRole("alert")).toContainText("currently in use");
  expect(writes[0].body).toMatchObject({ title: "", imageUrl: "/demo/banner-workspace.svg" });
});
