import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import type { CartDto, StorefrontProductDetailDto, StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";

const product: StorefrontProductListItemDto = {
  id: "ui-product", categoryId: "ui-category", categoryName: "Workspace furniture",
  name: "Atlas Standing Desk with a spacious adjustable oak worktop", slug: "atlas-standing-desk",
  description: "A comfortable workspace, designed around your everyday routine.",
  variantCount: 2, isFeatured: true, minPrice: 6290000, compareAtPrice: 6990000, isInStock: true,
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
        { id: "ui-banner-2", title: "Desk essentials", imageUrl: "/demo/banner-ergonomic.png", linkUrl: "/products", sortOrder: 1 },
        { id: "ui-banner-3", title: "Everyday comfort", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 2 },
        { id: "ui-banner-4", title: "Explore your workspace", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 3 },
        { id: "ui-banner-5", title: "A workspace that moves with you", imageUrl: "/demo/banner-ergonomic.png", linkUrl: "/products", sortOrder: 4 }
      ]; break;
      case "/api/blog-posts": data = []; break;
      case "/api/products": data = { items: [product, { ...product, id: "ui-product-2", slug: "sold-out-desk", name: "Compact desk", isInStock: false }], pageNumber: 1, pageSize: 20, totalCount: 2, totalPages: 1, hasNextPage: false, hasPreviousPage: false }; break;
      case "/api/products/atlas-standing-desk": data = detail; break;
      case "/api/products/atlas-standing-desk/reviews": data = { averageRating: 0, reviewCount: 0, reviews: [] }; break;
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

test("banner tiers keep artwork unobscured and image-only banners remain accessible", async ({ page }, info) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.route("**/api/banners", (route) => route.fulfill({ json: { success: true, errors: [], data: [
    { id: "image-only", title: "", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 0 },
    { id: "feature-a", title: "Desk essentials", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 1 },
    { id: "feature-b", title: "Everyday comfort", imageUrl: "/demo/banner-ergonomic.png", linkUrl: null, sortOrder: 2 },
    { id: "highlight-a", title: "Explore your workspace", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 3 },
    { id: "highlight-b", title: "", imageUrl: "/demo/banner-ergonomic.png", linkUrl: "/products/atlas-standing-desk", sortOrder: 4 },
    { id: "highlight-c", title: "A different workspace", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: 5 }
  ] } }));
  for (const width of [1440, 768, 390, 320]) {
    await page.setViewportSize({ width, height: 960 });
    await page.goto("/");
    const hero = page.getByRole("region", { name: "Hero banners", exact: true });
    const image = hero.locator("img");
    await expect(hero.getByRole("link", { name: "View Banner 1" })).toBeVisible();
    await expect(hero.getByRole("heading")).toHaveCount(0);
    await expect(hero.getByRole("link", { name: "View more", exact: true })).toHaveCount(0);
    await expect(image).toHaveCSS("filter", "none");
    await expect(image).toHaveCSS("opacity", "1");
    await expect.poll(() => image.evaluate((el: HTMLImageElement) => el.complete && el.naturalWidth > 0)).toBe(true);
    expect(await image.evaluate((el) => {
      const r = el.getBoundingClientRect();
      return document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2) === el;
    })).toBe(true);
    const features = page.getByRole("region", { name: "Featured banners" });
    await expect(features.getByRole("heading")).toHaveCount(2);
    const a = await features.getByRole("heading", { name: "Desk essentials" }).boundingBox();
    const b = await features.getByRole("heading", { name: "Everyday comfort" }).boundingBox();
    if (width >= 768) { expect(a!.x).toBeLessThan(b!.x); expect(Math.abs(a!.y - b!.y)).toBeLessThanOrEqual(1); }
    else expect(a!.y).toBeLessThan(b!.y);
    await expect(page.locator(".home-banner-stack img")).toHaveCount(6);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
    const scan = await new AxeBuilder({ page }).include(".home-banner-stack").withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
    expect(scan.violations.map((v) => ({ id: v.id, nodes: v.nodes.map((n) => n.target) }))).toEqual([]);
    await page.screenshot({ path: info.outputPath(`${width}-banner-tiers.png`), fullPage: false });
    await page.locator(".home-banner-stack").screenshot({ path: info.outputPath(`${width}-full-banner-stack.png`) });
    const highlightBox = await page.getByRole("region", { name: "More highlights" }).boundingBox();
    expect(highlightBox!.height).toBeLessThanOrEqual(width * (width >= 768 ? 0.34375 : 0.5625) + 210);
    const activeSlide = page.locator('.banner-carousel-slide[data-active="true"]');
    const slideBox = await activeSlide.boundingBox();
    expect(Math.abs(slideBox!.x + slideBox!.width / 2 - width / 2)).toBeLessThanOrEqual(1);
    expect(Math.abs(slideBox!.width - (width * (width >= 768 ? 0.55 : 0.9) - 8))).toBeLessThanOrEqual(1);
    const visiblePeeks = await page.locator('.banner-carousel-slide[data-active="false"]').evaluateAll((slides) => slides.filter(slide => { const r = slide.getBoundingClientRect(); return r.right > 0 && r.left < innerWidth; }).length);
    expect(visiblePeeks).toBe(2);
    await page.getByRole("button", { name: "Next banner" }).click();
    const highlights = page.getByRole("region", { name: "More highlights" });
    await expect(highlights.getByRole("link", { name: "View Banner 5" })).toBeVisible();
    await expect(highlights.getByRole("link", { name: "View Banner 5" })).toHaveAttribute("href", "/products/atlas-standing-desk");
    await expect(highlights.getByRole("link", { name: "View Explore your workspace" })).not.toBeVisible();
  }
});

test("a single second-tier banner keeps half the hero width on desktop", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.route("**/api/banners", (route) => route.fulfill({ json: { success: true, errors: [], data: [
    { id: "hero", title: "", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products/atlas-standing-desk", sortOrder: 0 },
    { id: "feature", title: "", imageUrl: "/demo/banner-ergonomic.png", linkUrl: "/products/atlas-standing-desk", sortOrder: 1 }
  ] } }));
  await page.goto("/");
  const hero = await page.getByRole("region", { name: "Hero banners" }).boundingBox();
  const feature = page.getByRole("region", { name: "Featured banners" }).locator('.banner-artwork');
  const box = await feature.boundingBox();
  expect(Math.abs(box!.width - (hero!.width - 8) / 2)).toBeLessThanOrEqual(1);
  await feature.getByRole("link", { name: "View Banner 2" }).click();
  await expect(page).toHaveURL(/\/products\/atlas-standing-desk$/);
});

test("many banners keep mobile controls within the page and support direct selection", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 320, height: 960 });
  await page.route("**/api/banners", (route) => route.fulfill({ json: { success: true, errors: [], data: Array.from({ length: 14 }, (_, index) => ({ id: `banner-${index}`, title: "", imageUrl: "/demo/banner-workspace.svg", linkUrl: "/products", sortOrder: index })) } }));
  await page.goto("/");
  const carousel = page.getByRole("region", { name: "More highlights" });
  const lastDot = carousel.getByRole("button", { name: "Go to banner 11", exact: true });
  await lastDot.click();
  await expect(carousel.getByRole("link", { name: "View Banner 14" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
  await page.getByRole("button", { name: "Next banner" }).click();
  await expect(carousel.locator('[aria-current="true"]')).toHaveAttribute("aria-label", "Go to banner 1");
});

test("empty banner feeds hide the promotion area and broken artwork has a fallback", async ({ page }) => {
  await page.route("**/api/banners", (route) => route.fulfill({ json: { success: true, errors: [], data: [] } }));
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1, name: "Your everyday workspace" })).toHaveCount(1);
  await expect(page.getByRole("region", { name: "Hero banners", exact: true })).toHaveCount(0);
  await expect(page.getByText("Add banners in the Admin Portal")).toHaveCount(0);
  await page.unroute("**/api/banners");
  await page.route("**/api/banners", (route) => route.fulfill({ json: { success: true, errors: [], data: [{ id: "broken", title: "", imageUrl: "/ui-broken-banner.png", linkUrl: "/products", sortOrder: 0 }] } }));
  await page.route("**/ui-broken-banner.png", (route) => route.fulfill({ status: 404, body: "missing" }));
  await page.goto("/");
  await expect(page.getByText("This banner image is unavailable.", { exact: true })).toBeVisible();
  await expect(page.getByRole("region", { name: "Hero banners", exact: true }).getByRole("link", { name: "View Banner 1" })).toBeVisible();
});

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
          const cta = await page.getByRole("region", { name: "Hero banners", exact: true }).getByRole("link", { name: "View more", exact: true }).boundingBox();
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
  await page.route("**/api/products?**", route => route.fulfill({ json: { success: true, errors: [], data: { items: [{ ...product, variantCount: 1 }], totalCount: 1 } } }));
  await page.route("**/api/products/atlas-standing-desk", route => route.fulfill({ json: { success: true, errors: [], data: { ...detail, variants: [detail.variants[0]] } } }));
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

test("carousel is manually controlled, slides horizontally and respects reduced motion", async ({ page }) => {
  await page.clock.install();
  await page.goto("/");
  const carousel = page.getByRole("region", { name: "More highlights" });
  await page.clock.fastForward(6000);
  await expect(carousel.getByRole("heading", { name: "Explore your workspace" })).toBeVisible();
  await carousel.getByRole("button", { name: "Next banner" }).click();
  await expect(carousel.getByRole("heading", { name: "A workspace that moves with you" })).toBeVisible();
  await expect(carousel.locator('[data-active="true"]')).toHaveCSS("transition-property", "transform");
  await page.emulateMedia({ reducedMotion: "reduce" });
  const duration = await carousel.locator('[data-active="true"]').evaluate(node => getComputedStyle(node).transitionDuration);
  expect(parseFloat(duration)).toBeLessThanOrEqual(0.01);
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

test("new arrivals use five desktop columns and never show more than ten products", async ({ page }, info) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  let sort: string | null = null;
  await page.route("**/api/products?**", route => {
    sort = new URL(route.request().url()).searchParams.get("sortBy");
    return route.fulfill({ json: { success: true, errors: [], data: { items: Array.from({ length: 12 }, (_, index) => ({ ...product, id: `new-${index}` })), totalCount: 12 } } });
  });
  await page.goto("/");
  const grid = page.getByTestId("new-arrivals-grid");
  await expect(grid.locator("article")).toHaveCount(10);
  expect(sort).toBe("created-desc");
  const layout = await grid.evaluate(node => ({ columns: getComputedStyle(node).gridTemplateColumns.split(" ").length, rows: new Set(Array.from(node.children).map(child => Math.round(child.getBoundingClientRect().top))).size }));
  expect(layout).toEqual({ columns: 5, rows: 2 });
  await grid.screenshot({ path: info.outputPath("1440-new-arrivals-5x2.png") });
});

test("multi-variant products require configuration before adding to cart", async ({ page }) => {
  let writes = 0;
  page.on("request", request => { if (request.url().includes("/api/cart/items")) writes++; });
  await page.goto("/products");
  await page.getByRole("link", { name: "Choose options", exact: true }).click();
  await expect(page).toHaveURL(/products\/atlas-standing-desk$/);
  expect(writes).toBe(0);
});

test("payment results distinguish pending, refund and terminal states using server data", async ({ page }) => {
  for (const [status, title] of [[0, "Awaiting payment"], [1, "Payment is being processed"], [2, "Payment confirmed"], [3, "Payment failed"], [4, "Payment cancelled"], [5, "Refund awaiting reconciliation"], [6, "Full refund recorded"]] as const) {
    await page.route("**/api/payments/result?**", route => route.fulfill({ json: { success: true, errors: [], data: { orderCode: `ORDER-${status}`, paymentMethod: 2, paymentStatus: status, paidAt: null, totalAmount: 100000, shipmentCreated: false, trackingCode: null, message: null } } }));
    await page.goto(`/checkout/payment-result?orderCode=ORDER-${status}&status=failed#resultToken=fixture-proof`);
    await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
    await page.unroute("**/api/payments/result?**");
  }
});

test("unknown routes render a useful storefront fallback", async ({ page }) => {
  await page.goto("/does-not-exist");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  await expect(page.getByText("Unexpected Application Error!")).toHaveCount(0);
});

test("account order pagination can reach the twenty-first order", async ({ page }) => {
  const profile = { id: "customer-A", fullName: "Customer A", email: "a@example.test", phoneNumber: "0900000000", avatarUrl: null, isEmailVerified: true, rewardPoints: 0, twoFactorEnabled: false, createdAt: "2026-11-01T00:00:00Z", updatedAt: "2026-11-01T00:00:00Z" };
  await page.addInitScript(profile => sessionStorage.setItem("workspace-ecommerce-customer-session", JSON.stringify({ customerId: profile.id, ...profile, accessToken: "fixture-A", tokenType: "Bearer", expiresAt: new Date(Date.now() + 300000).toISOString() })), profile);
  await page.route("**/api/customer/me", route => route.fulfill({ json: { success: true, errors: [], data: profile } }));
  await page.route("**/api/customer/orders?**", route => {
    const pageNumber = Number(new URL(route.request().url()).searchParams.get("pageNumber"));
    const start = (pageNumber - 1) * 20;
    return route.fulfill({ json: { success: true, errors: [], data: { totalCount: 21, pageNumber, pageSize: 20, items: Array.from({ length: pageNumber === 1 ? 20 : 1 }, (_, index) => ({ id: `order-${start + index + 1}`, orderCode: `ORDER-${start + index + 1}`, totalAmount: 100000, status: 0, paymentMethod: 0, paymentStatus: 0, paidAt: null, createdAt: "2026-11-01T00:00:00Z", updatedAt: "2026-11-01T00:00:00Z", itemCount: 1, trackingCode: null, shipmentProvider: null })) } } });
  });
  await page.goto("/account/orders");
  await expect(page.getByText("ORDER-20", { exact: true })).toBeVisible();
  const pagination = page.getByRole("navigation", { name: "Pagination" });
  await pagination.getByRole("button", { name: "Next" }).click();
  await expect(page.getByText("ORDER-21", { exact: true })).toBeVisible();
  await expect(pagination.getByRole("button", { name: "Next" })).toBeDisabled();
});

test("review stars support keyboard selection and expose a checked rating", async ({ page }) => {
  const profile = { id: "reviewer", fullName: "Reviewer", email: "reviewer@example.test", phoneNumber: "0900000000", avatarUrl: null, isEmailVerified: true, rewardPoints: 0, twoFactorEnabled: false, createdAt: "2026-11-01T00:00:00Z", updatedAt: "2026-11-01T00:00:00Z" };
  await page.addInitScript(profile => sessionStorage.setItem("workspace-ecommerce-customer-session", JSON.stringify({ customerId: profile.id, ...profile, accessToken: "fixture-reviewer", tokenType: "Bearer", expiresAt: new Date(Date.now() + 300000).toISOString() })), profile);
  await page.route("**/api/customer/me", route => route.fulfill({ json: { success: true, errors: [], data: profile } }));
  await page.goto("/products/atlas-standing-desk");
  const first = page.getByRole("radio", { name: "1 star", exact: true });
  await first.focus(); await page.keyboard.press("Space"); await page.keyboard.press("ArrowRight");
  await expect(page.getByRole("radio", { name: "2 stars", exact: true })).toBeChecked();
  await expect(first).not.toBeChecked();
});
