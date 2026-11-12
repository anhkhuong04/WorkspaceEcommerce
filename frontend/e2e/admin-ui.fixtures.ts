import type { Page } from "@playwright/test";
import type {
  AdminDashboardDto,
  AdminOrderDto,
  AdminProductDto,
} from "@workspace-ecommerce/api-types";
const date = "2026-10-14T09:00:00Z";
export const product: AdminProductDto = {
  id: "11111111-1111-4111-8111-111111111111",
  categoryId: "ui-category",
  categoryName: "Standing desks",
  name: { vi: "Standing desk with an adjustable oak worktop for a spacious home office", en: "Standing desk with an adjustable oak worktop for a spacious home office" },
  slug: "ui-standing-desk",
  description: { vi: "A practical desk for everyday work.", en: "A practical desk for everyday work." },
  isFeatured: true,
  isActive: true,
  createdAt: date,
  updatedAt: date,
  variants: [
    {
      id: "ui-variant",
      productId: "11111111-1111-4111-8111-111111111111",
      sku: "DESK-OAK-140",
      name: "Oak / 140 cm",
      color: "Oak",
      size: "140 cm",
      price: 6990000,
      compareAtPrice: 7990000,
      stockQuantity: 3,
      requiresInstallation: true,
      isActive: true,
    },
    {
      id: "ui-variant-white",
      productId: "11111111-1111-4111-8111-111111111111",
      sku: "DESK-WHITE-160",
      name: "White / 160 cm",
      color: "White",
      size: "160 cm",
      price: 8990000,
      compareAtPrice: null,
      stockQuantity: 12,
      requiresInstallation: false,
      isActive: true,
    },
  ],
  images: [
    {
      id: "ui-image",
      productId: "11111111-1111-4111-8111-111111111111",
      imageUrl: "/demo/atlas-standing-desk-1.png",
      altText: "Oak desk",
      sortOrder: 0,
    },
  ],
  specifications: [
    {
      id: "ui-spec",
      productId: "11111111-1111-4111-8111-111111111111",
      name: "Worktop",
      value: "140 × 70 cm",
      sortOrder: 0,
    },
  ],
};
export const distantProduct = {
  ...product,
  id: "22222222-2222-4222-8222-222222222222",
  name: { vi: "A product on the next catalog page", en: "A product on the next catalog page" },
  slug: "ui-distant-product",
  variants: [
    {
      ...product.variants[0],
      id: "distant-variant",
      productId: "22222222-2222-4222-8222-222222222222",
      sku: "DISTANT-OAK",
    },
  ],
};
const products = [
  product,
  ...Array.from({ length: 19 }, (_, i) => ({
    ...product,
    id: `ui-product-${i}`,
    slug: `ui-product-${i}`,
    name: { vi: `Catalog product ${i + 2}`, en: `Catalog product ${i + 2}` },
  })),
  distantProduct,
];
export const order: AdminOrderDto = {
  id: "ui-order",
  orderCode: "WE-20261014-001",
  customerId: null,
  customerName: "Example customer",
  customerPhone: "0900000000",
  customerEmail: null,
  shippingAddress: "Example address",
  note: null,
  couponId: null,
  couponCodeSnapshot: null,
  couponNameSnapshot: null,
  subtotal: 6990000,
  shippingFee: 30000,
  discountAmount: 0,
  totalAmount: 7020000,
  status: 1,
  paymentMethod: 0,
  paymentStatus: 0,
  paidAt: null,
  createdAt: date,
  updatedAt: date,
  itemCount: 1,
  trackingCode: "TRACK-001",
  shipmentStatus: "ReadyForPickup",
  items: [],
  statusHistory: [
    {
      id: "ui-history",
      fromStatus: null,
      toStatus: 0,
      internalNote: null,
      cancellationReason: null,
      customerMessage: null,
      changedBy: "system",
      changedAt: date,
    },
  ],
};
export const dashboard: AdminDashboardDto = {
  totalOrders: 4,
  totalRevenue: 987654321000,
  netCollectedRevenue: 987654321000,
  operations: [{ key: "transfers", label: "Verify bank transfers", count: 2, url: "/orders?queue=transfers" }],
  newOrders: 1,
  lowStockThreshold: 5,
  lowStockVariants: [
    {
      productId: distantProduct.id,
      productName: distantProduct.name.en,
      variantId: "distant-variant",
      sku: "DISTANT-OAK",
      variantName: "Oak / 140 cm",
      stockQuantity: 3,
      isActive: true,
    },
  ],
  orderStatusSummary: [
    { status: 0, count: 1 },
    { status: 4, count: 2 },
    { status: 6, count: 1 },
  ],
  recentOrders: [
    {
      id: order.id,
      orderCode: order.orderCode,
      customerName: order.customerName,
      totalAmount: order.totalAmount,
      status: order.status,
      createdAt: date,
    },
  ],
};
export function paged<T>(
  items: T[],
  totalCount = items.length,
  pageNumber = 1,
  pageSize = 20,
) {
  return {
    items,
    pageNumber,
    pageSize,
    totalCount,
    totalPages: Math.ceil(totalCount / pageSize),
    hasPreviousPage: pageNumber > 1,
    hasNextPage: pageNumber * pageSize < totalCount,
  };
}
export async function mockAdmin(
  page: Page,
  options: {
    warranties?: boolean;
    emptyDashboard?: boolean;
    authenticated?: boolean;
    productCatalog?: AdminProductDto[];
  } = {},
) {
  const writes: { path: string; method: string; body: unknown }[] = [];
  if (options.authenticated !== false)
    await page.addInitScript(() =>
      localStorage.setItem(
        "workspace-ecommerce-admin-session",
        JSON.stringify({
          accessToken: "ui-fixture-token",
          tokenType: "Bearer",
          email: "ui-admin@example.test",
          expiresAt: "2099-01-01T00:00:00Z",
        }),
      ),
    );
  await page.route("**/*", async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    if (url.hostname !== "127.0.0.1") {
      await route.abort();
      return;
    }
    if (!url.pathname.startsWith("/api/")) {
      await route.continue();
      return;
    }
    const path = url.pathname;
    let data: unknown;
    let status = 200;
    if (req.method() !== "GET") {
      writes.push({
        path,
        method: req.method(),
        body: req.postData() ? req.postDataJSON() : null,
      });
      status = 409;
    } else if (path === "/api/admin/dashboard")
      data = options.emptyDashboard
        ? {
            ...dashboard,
            totalOrders: 0,
            totalRevenue: 0,
            netCollectedRevenue: 0,
            newOrders: 0,
            recentOrders: [],
            orderStatusSummary: dashboard.orderStatusSummary.map((x) => ({
              ...x,
              count: 0,
            })),
          }
        : dashboard;
    else if (path === "/api/admin/operations/outbox") data = { observedAtUtc: date, queues: [{ outbox: "customer-email", dueCount: 1, leasedCount: 0, retryCount: 0, deadLetterCount: 0, oldestActiveAt: date }, { outbox: "shipment-command", dueCount: 0, leasedCount: 0, retryCount: 0, deadLetterCount: 0, oldestActiveAt: null }] };
    else if (path === "/api/admin/operations/outbox/dead-letters") data = [];
    else if (path === "/api/admin/categories")
      data = [
        {
          id: "ui-category",
          parentId: null,
          name: { vi: "Standing desks", en: "Standing desks" },
          slug: "standing-desks",
          sortOrder: 0,
          isActive: true,
          children: [
            {
              id: "ui-child",
              parentId: "ui-category",
              name: { vi: "Desk accessories", en: "Desk accessories" },
              slug: "desk-accessories",
              sortOrder: 1,
              isActive: true,
              children: [],
            },
          ],
        },
      ];
    else if (path === "/api/admin/products") {
      const search = (url.searchParams.get("search") ?? "").toLowerCase();
      const ids = url.searchParams.get("ids")?.split(",");
      const catalog = (options.productCatalog ?? products).filter(p =>
        (!url.searchParams.has("productId") || p.id === url.searchParams.get("productId")) &&
        (!ids || ids.includes(p.id)) &&
        (!search || [...Object.values(p.name), p.slug, ...p.variants.map(v => v.sku)].some(value => value.toLowerCase().includes(search))) &&
        (!url.searchParams.has("isActive") || p.isActive === (url.searchParams.get("isActive") === "true")) &&
        (!url.searchParams.has("categoryId") || p.categoryId === url.searchParams.get("categoryId"))
      );
      const n = Number(url.searchParams.get("pageNumber") ?? 1),
        size = Number(url.searchParams.get("pageSize") ?? 20);
      data = paged(
        catalog.slice((n - 1) * size, n * size),
        catalog.length,
        n,
        size,
      );
    } else if (path === "/api/admin/orders") data = paged([order]);
    else if (path === "/api/admin/orders/ui-order") data = order;
    else if (path.endsWith("/stock-history")) data = paged([]);
    else if (path.endsWith("/payment/exceptions")) data = { canRecord: false, currency: "VND", exceptions: paged([]) };
    else if (path.endsWith("/payment")) data = { orderId: order.id, currency: "VND", paymentMethod: order.paymentMethod, paymentStatus: order.paymentStatus, totalAmount: order.totalAmount, receipts: [], canConfirmTransfer: false, canCollectCod: false, canRemitCod: false, canConfirmFullRefund: false, legacySemantics: true };
    else if (path.endsWith("/returns")) data = { orderId: order.id, currency: "VND", canCreate: false, eligibilityMessage: "Returns require a verified paid, completed VND order.", requestedAt: null, reviewedAt: null, decision: null, decisionReason: null, items: [], returns: paged([]) };
    else if (path === "/api/admin/dashboard/search") data = { items: [{ kind: "Products", id: product.id, title: product.name.en, detail: product.slug, url: `/products?productId=${product.id}` }, { kind: "Orders", id: order.id, title: order.orderCode, detail: order.customerName, url: `/orders?orderId=${order.id}` }] };
    else if (path === "/api/admin/orders/ui-order/shipment")
      data = {
        orderId: order.id,
        orderCode: order.orderCode,
        orderStatus: order.status,
        shipmentId: "ui-shipment",
        trackingCode: order.trackingCode,
        provider: "Carrier",
        providerStatus: "ReadyForPickup",
        shippingFeeAmount: 30000,
        currency: "VND",
        lastSyncedAtUtc: date,
        lastEventAtUtc: date,
        canRetry: false,
        canRefresh: true,
        canCancel: true,
        lastCommandError: null,
        timeline: [],
      };
    else if (path === "/api/admin/coupons")
      data = paged([
        {
          id: "ui-coupon",
          code: "WORK10",
          name: "Workspace promotion",
          description: null,
          discountType: 0,
          discountValue: 10,
          maxDiscountAmount: 100000,
          minimumSubtotal: 1000000,
          startsAt: null,
          endsAt: null,
          usageLimit: 100,
          customerId: null,
          source: 0,
          usedCount: 1,
          redemptionCount: 1,
          isActive: true,
          productTargetIds: [],
          createdAt: date,
          updatedAt: date,
        },
      ]);
    else if (path === "/api/admin/banners")
      data = [
        {
          id: "ui-banner",
          title: "Make space for better work",
          imageUrl: "/demo/banner-workspace.svg",
          linkUrl: "/products",
          sortOrder: 0,
          isActive: true,
          createdAt: date,
          updatedAt: date,
        },
      ];
    else if (path === "/api/admin/blog-posts" || path === "/api/admin/blog-posts/page")
      data = [
        {
          id: "ui-blog",
          title: "Designing a comfortable workspace",
          slug: "comfortable-workspace",
          summary: "Practical tips for a comfortable workspace.",
          content: "<p>Adjust your desk to a comfortable height.</p>",
          imageUrl: "/demo/banner-workspace.svg",
          isPublished: true,
          publishedAt: date,
          createdAt: date,
          updatedAt: date,
          relatedProductIds: [],
        },
      ];
    else if (path === "/api/admin/blog-posts/ui-blog/comments") data = [];
    else if (path === "/api/admin/blog-comments/page") data = paged([]);
    else if (path === "/api/admin/reviews")
      data = paged([
        {
          id: "ui-review",
          productId: product.id,
          productName: product.name.en,
          customerId: "ui-customer",
          customerName: "Example reviewer",
          rating: 4,
          comment:
            "A comfortable desk with plenty of room for everyday work. ".repeat(
              5,
            ),
          createdAt: date,
        },
      ]);
    else if (path.startsWith("/api/admin/warrant")) {
      if (!options.warranties) status = 404;
      else if (path === "/api/admin/warranty-plans")
        data = paged([
          {
            id: "ui-plan",
            code: "FRAME",
            name: "Desk warranty",
            activationWindowDays: 60,
            termsVersion: "v1",
            effectiveFrom: date,
            effectiveTo: null,
            isActive: true,
            createdAt: date,
            updatedAt: date,
            coverages: [
              {
                componentCode: "FRAME",
                displayName: "Frame",
                durationMonths: 60,
                sortOrder: 0,
              },
            ],
          },
        ]);
      else if (path === "/api/admin/warranty-units") data = paged([]);
      else if (path === "/api/admin/warranties")
        data = paged([
          {
            id: "ui-warranty",
            serializedProductUnitId: "ui-unit",
            maskedIdentifier: "***1234",
            identifierType: 0,
            warrantyPlanId: "ui-plan",
            warrantyPlanName: "Desk warranty",
            orderId: order.id,
            orderCode: order.orderCode,
            customerId: null,
            productName: product.name.en,
            status: 1,
            purchasedAt: date,
            eligibleAt: date,
            activationDeadline: null,
            activatedAt: date,
            activationSource: 0,
            replacementSerializedProductUnitId: null,
            coverages: [],
            auditEvents: [],
          },
        ]);
      else throw Error(`Missing warranty fixture: ${path}`);
    } else
      throw Error(`Unexpected admin fixture request: ${req.method()} ${path}`);
    if (path === "/api/admin/blog-posts/page" && Array.isArray(data)) data = paged(data);
    await route.fulfill({
      status,
      json: {
        success: status === 200,
        data,
        errors:
          status === 404
            ? ["Warranty management is unavailable"]
            : status === 409
              ? ["This item is currently in use. Try again."]
              : [],
        traceId: "admin-ui-fixture",
      },
    });
  });
  return writes;
}
