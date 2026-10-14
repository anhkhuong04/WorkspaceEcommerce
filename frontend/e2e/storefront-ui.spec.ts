import { expect, test, type Page } from "@playwright/test";
import type { CartDto, StorefrontProductDetailDto, StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";

const product: StorefrontProductListItemDto = {
  id: "ui-product", categoryId: "ui-category", categoryName: "Workspace furniture",
  name: "Atlas Standing Desk with a spacious adjustable oak worktop", slug: "atlas-standing-desk",
  description: "A comfortable workspace, designed around your everyday routine.",
  isFeatured: true, minPrice: 6290000, compareAtPrice: 6990000, isInStock: true,
  imageUrl: "/demo/atlas-standing-desk-1.png"
};
const detail: StorefrontProductDetailDto = {
  ...product,
  variants: [
    { id: "ui-variant-oak", sku: "ATLAS-OAK", name: "Oak / 140cm", color: "Oak", size: "140cm", price: 6290000, compareAtPrice: 6990000, stockQuantity: 10, requiresInstallation: false },
    { id: "ui-variant-white", sku: "ATLAS-WHITE", name: "White / 160cm", color: "White", size: "160cm", price: 7290000, compareAtPrice: null, stockQuantity: 3, requiresInstallation: false }
  ],
  images: [{ id: "ui-image", imageUrl: product.imageUrl!, altText: product.name, sortOrder: 0 }],
  specifications: [{ id: "ui-spec", name: "Worktop", value: "140 × 70 cm", sortOrder: 0 }]
};

async function mockStorefront(page: Page) {
  await page.addInitScript(() => localStorage.setItem("workspace-ecommerce-language", "en"));
  let cart: CartDto = { id: "ui-cart", customerId: null, sessionId: "ui-session", totalQuantity: 0, totalAmount: 0, items: [] };
  await page.route("**/*", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.hostname !== "127.0.0.1") { await route.abort(); return; }
    if (!url.pathname.startsWith("/api/")) { await route.continue(); return; }
    let data: unknown;
    let status = 200;
    switch (url.pathname) {
      case "/api/customer/auth/refresh": status = 401; break;
      case "/api/categories": data = [{ id: "ui-category", parentId: null, name: "Workspace furniture", slug: "furniture", sortOrder: 0, children: [] }]; break;
      case "/api/banners": data = [
        { id: "ui-banner-1", title: "Make room for better work", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 0 },
        { id: "ui-banner-2", title: "A workspace that moves with you", imageUrl: "/demo/banner-ergonomic.png", linkUrl: "/products", sortOrder: 1 }
      ]; break;
      case "/api/blog-posts": data = []; break;
      case "/api/products": data = { items: [product, { ...product, id: "ui-product-2", slug: "sold-out-desk", name: "Compact desk", isInStock: false }], pageNumber: 1, pageSize: 20, totalCount: 2, totalPages: 1, hasNextPage: false, hasPreviousPage: false }; break;
      case "/api/products/atlas-standing-desk": data = detail; break;
      case "/api/products/atlas-standing-desk/reviews": data = { averageRating: 0, totalReviews: 0, reviews: [] }; break;
      case "/api/cart": data = cart; break;
      case "/api/cart/items": {
        const body = request.postDataJSON() as { productVariantId: string; quantity: number };
        const variant = detail.variants.find((item) => item.id === body.productVariantId)!;
        cart = { ...cart, totalQuantity: body.quantity, totalAmount: variant.price * body.quantity, items: [{ id: "ui-cart-item", productVariantId: variant.id, productId: product.id, productName: product.name, productSlug: product.slug, variantName: variant.name, variantColor: variant.color, variantSize: variant.size, imageUrl: product.imageUrl, quantity: body.quantity, unitPriceSnapshot: variant.price, lineTotal: variant.price * body.quantity }] };
        data = cart; break;
      }
      default: throw new Error(`Unexpected UI fixture request: ${request.method()} ${url.pathname}`);
    }
    await route.fulfill({ status, json: { success: status === 200, data, errors: status === 200 ? [] : ["Not authenticated"], traceId: "ui-fixture" } });
  });
}

test.beforeEach(async ({ page }) => { await mockStorefront(page); });

test("captures storefront at desktop and mobile sizes", async ({ page }, testInfo) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  for (const width of [1440, 768, 390, 320]) {
    await page.setViewportSize({ width, height: 900 });
    for (const path of ["/", "/products", "/products/atlas-standing-desk"]) {
      await page.goto(path);
      await expect(page.getByText(product.name).first()).toBeVisible();
      await page.screenshot({ path: testInfo.outputPath(`${width}-${path === "/" ? "home" : path === "/products" ? "catalog" : "detail"}.png`), fullPage: true });
      if (process.env.UI_CAPTURE_LABEL !== "before") {
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
        if (path === "/") {
          const hero = await page.getByRole("heading", { name: "Make room for better work" }).boundingBox();
          const cta = await page.getByRole("link", { name: "Shop now", exact: true }).boundingBox();
          const region = await page.getByRole("region", { name: "Hero banners", exact: true }).boundingBox();
          expect(hero).not.toBeNull();
          expect(hero!.y).toBeGreaterThanOrEqual(region!.y);
          expect(cta!.y + cta!.height).toBeLessThanOrEqual(region!.y + region!.height);
        }
      }
    }
  }
});

test("mobile navigation and search contain focus and restore it on Escape", async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  const menuTrigger = page.getByRole("button", { name: "Open navigation" });
  await menuTrigger.click();
  const menu = page.getByRole("dialog", { name: "Navigation" });
  await expect(menu).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("390-navigation.png") });
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press("Tab");
    expect(await menu.evaluate((node) => node.contains(document.activeElement))).toBe(true);
  }
  await page.keyboard.press("Escape");
  await expect(menu).not.toBeVisible();
  await expect(menuTrigger).toBeFocused();
  const searchTrigger = page.getByRole("button", { name: "Search", exact: true });
  await searchTrigger.click();
  const search = page.getByRole("dialog", { name: "Search", exact: true });
  await search.getByRole("textbox").fill("desk");
  await expect(search.getByText(product.name)).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("390-search.png") });
  await page.keyboard.press("Escape");
  await expect(search).not.toBeVisible();
  await expect(searchTrigger).toBeFocused();
});

test("touch quick-add exposes errors and nested cart login dialog closes independently", async ({ browser }, testInfo) => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  await mockStorefront(page);
  await page.goto("/products");
  const add = page.getByRole("button", { name: `Add ${product.name} to cart` });
  await expect(add).toHaveCSS("opacity", "1");
  await page.route("**/api/cart/items", (route) => route.fulfill({ status: 409, json: { success: false, errors: ["Stock changed. Please retry."], traceId: "ui-conflict" } }));
  await add.tap();
  await expect(page.getByRole("alert")).toContainText("Stock changed");
  await expect(page.getByRole("dialog", { name: "Cart", exact: true })).not.toBeVisible();
  await page.unroute("**/api/cart/items");
  await add.tap();
  const cart = page.getByRole("dialog", { name: "Cart", exact: true });
  await expect(cart).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("390-cart.png") });
  await cart.getByRole("button", { name: "Checkout" }).click();
  const login = page.getByRole("dialog", { name: "Please log in" });
  await expect(login).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(login).not.toBeVisible();
  await expect(cart).toBeVisible();
  await expect(cart.getByRole("button", { name: "Checkout" })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(cart).not.toBeVisible();
  await expect(add).toBeFocused();
  await context.close();
});

test("carousel pauses explicitly and respects reduced motion", async ({ page }) => {
  await page.clock.install();
  await page.goto("/");
  const pause = page.getByRole("button", { name: "Pause slideshow" });
  await pause.click();
  await page.clock.fastForward(6000);
  await expect(page.getByRole("heading", { name: "Make room for better work" })).toBeVisible();
  await page.getByRole("button", { name: "Resume slideshow" }).click();
  await page.mouse.move(0, 0);
  await page.clock.fastForward(6000);
  await expect(page.getByRole("heading", { name: "A workspace that moves with you" })).toBeVisible();
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await page.clock.fastForward(6000);
  await expect(page.getByRole("heading", { name: "Make room for better work" })).toBeVisible();
  await expect(pause).not.toBeVisible();
});

test("mobile filters preserve URL selection and product variants support arrow keys", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/products");
  const filterTrigger = page.getByRole("button", { name: /^Filters/ });
  await filterTrigger.click();
  const filters = page.getByRole("dialog", { name: "Product filters" });
  await filters.getByRole("button", { name: "Workspace furniture" }).click();
  await expect(page).toHaveURL(/categorySlug=furniture/);
  await page.keyboard.press("Escape");
  await expect(filters).not.toBeVisible();
  await expect(filterTrigger).toBeFocused();
  await page.goto("/products/atlas-standing-desk");
  await page.getByRole("radio", { name: /^Oak/ }).focus();
  await page.keyboard.press("ArrowDown");
  await expect(page.getByRole("radio", { name: /^White/ })).toHaveAttribute("aria-checked", "true");
  await page.route("**/api/products/atlas-standing-desk", (route) => route.fulfill({ json: {
    success: true, errors: [], data: { ...detail, variants: detail.variants.map((variant) => ({ ...variant, stockQuantity: variant.id === "ui-variant-white" ? 0 : variant.stockQuantity })) }
  } }));
  // Language changes refetch the catalog. A selected variant may become unavailable.
  await page.getByRole("button", { name: "Open navigation" }).click();
  await page.getByRole("dialog", { name: "Navigation" }).getByRole("button", { name: "Select language" }).click();
  await page.getByRole("menuitemradio", { name: "Vietnamese" }).click();
  await page.getByRole("button", { name: "Đóng điều hướng" }).click();
  await expect(page.getByRole("radio", { name: /^White/ })).toBeDisabled();
  await page.locator("form").first().getByRole("link", { name: "Workspace furniture" }).focus();
  await page.keyboard.press("Tab");
  await expect(page.getByRole("radio", { name: /^Oak/ })).toBeFocused();
  await page.keyboard.press("Space");
  await expect(page.getByRole("radio", { name: /^Oak/ })).toHaveAttribute("aria-checked", "true");
});

test("language popup Escape keeps mobile navigation open and Vietnamese labels fit", async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await page.getByRole("button", { name: "Open navigation" }).click();
  const navigation = page.getByRole("dialog", { name: "Navigation" });
  const language = navigation.getByRole("button", { name: "Select language" });
  await language.click();
  await page.keyboard.press("Escape");
  await expect(navigation).toBeVisible();
  await expect(navigation.getByRole("menu")).not.toBeVisible();
  await expect(language).toBeFocused();
  await language.click();
  await page.getByRole("menuitemradio", { name: "Vietnamese" }).click();
  const vietnameseNavigation = page.getByRole("dialog", { name: "Điều hướng" });
  await expect(vietnameseNavigation).toBeVisible();
  await vietnameseNavigation.getByRole("button", { name: "Đóng điều hướng" }).click();
  await expect(page.getByRole("button", { name: "Mở điều hướng" })).toBeFocused();
  await page.emulateMedia({ reducedMotion: "reduce" });
  for (const width of [390, 1024]) {
    await page.setViewportSize({ width, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`${width}-home-vi.png`), fullPage: true });
  }
});
