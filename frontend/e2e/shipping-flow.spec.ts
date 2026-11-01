import { createHmac, randomUUID } from "node:crypto";
import { mkdir, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { expect, test, type APIRequestContext, type APIResponse } from "@playwright/test";
import type { CustomerAuthResponse, CustomerOrderDto, CustomerOrderListItemDto, PagedResult, ShippingCatalogSummary, ShippingQuoteDto, ShippingRatePlan, ShipmentTrackingDto, WorkspaceShipmentDto } from "@workspace-ecommerce/api-types";
import { getLoopbackHttpUrl } from "./safety";

// The config and runner independently enforce loopback endpoints and a fresh DB.
const api = getLoopbackHttpUrl("E2E_API_PROXY_TARGET", "http://127.0.0.1:5080");
const provider = getLoopbackHttpUrl("E2E_PROVIDER_URL", "http://127.0.0.1:5081");
let adminHeaders: Record<string, string>;
const providerHeaders = { Authorization: `Bearer ${process.env.E2E_PROVIDER_KEY}` };

async function data<T>(response: APIResponse): Promise<T> {
  expect(response.ok(), `${response.status()}: ${await response.text()}`).toBeTruthy();
  return (await response.json()).data as T;
}
async function providerState(request: APIRequestContext) {
  const response = await request.get(`${provider}/__test/state`, { headers: providerHeaders });
  expect(response.ok()).toBeTruthy();
  return await response.json() as { counts: { quote: number; create: number; tracking: number; cancel: number } };
}
async function status(request: APIRequestContext, id: string, value: number) {
  await data(await request.put(`${api}/api/admin/orders/${id}/status`, { headers: adminHeaders, data: { status: value, note: "Synthetic E2E transition" } }));
}
async function localAction(request: APIRequestContext, id: string, revision: number, action: number) {
  const body = { requestId: randomUUID(), expectedRevision: revision, action, reason: "Synthetic private operation", assignedTo: action === 0 ? "SYNTHETIC-DELIVERY-TEAM" : null };
  const result = await data<WorkspaceShipmentDto>(await request.post(`${api}/api/admin/orders/${id}/workspace-shipment/actions`, { headers: adminHeaders, data: body }));
  const replay = await data<WorkspaceShipmentDto>(await request.post(`${api}/api/admin/orders/${id}/workspace-shipment/actions`, { headers: adminHeaders, data: body }));
  expect(replay).toEqual(result);
  return result;
}
function signedCallback(paymentUrl: string, number: number) {
  const payment = new URL(paymentUrl).searchParams;
  const fields: Record<string, string> = {
    vnp_TmnCode: process.env.E2E_VNPAY_CODE!, vnp_TxnRef: payment.get("vnp_TxnRef")!,
    vnp_Amount: payment.get("vnp_Amount")!, vnp_BankCode: "NCB", vnp_OrderInfo: "Synthetic shipping matrix",
    vnp_TransactionNo: String(100000 + number), vnp_ResponseCode: "00", vnp_TransactionStatus: "00",
  };
  const query = Object.keys(fields).sort().map(key => `${encodeURIComponent(key)}=${encodeURIComponent(fields[key])}`).join("&");
  const signature = createHmac("sha512", process.env.E2E_VNPAY_SECRET!).update(query).digest("hex");
  return `${query}&vnp_SecureHash=${signature}`;
}

test.describe("real browser/API/PostgreSQL shipping matrix", () => {
  test.describe.configure({ mode: "serial" });
  test.beforeAll(async ({ request }) => {
    const login = await data<{ accessToken: string }>(await request.post(`${api}/api/admin/auth/login`, { data: { email: process.env.E2E_ADMIN_EMAIL, password: process.env.E2E_ADMIN_PASSWORD } }));
    adminHeaders = { Authorization: `Bearer ${login.accessToken}` };
    const catalog = await data<ShippingCatalogSummary>(await request.get(`${api}/api/admin/shipping/catalog`, { headers: adminHeaders }));
    const plan = await data<ShippingRatePlan>(await request.post(`${api}/api/admin/shipping/rate-plans`, { headers: adminHeaders, data: {
      name: "Synthetic E2E only - never production rates", catalogVersion: catalog.version, originProvinceCode: "01", expectedRevision: 0, reason: "Isolated test",
      zones: [{ code: "ALL", name: "Synthetic nationwide zone", baseWeightGrams: 1000, baseFee: 10000, stepWeightGrams: 500, stepFee: 5000, maximumWeightGrams: null, provinceCodes: catalog.provinces.map(x => x.code) }],
    } }));
    await data(await request.post(`${api}/api/admin/shipping/rate-plans/${plan.id}/publish`, { headers: adminHeaders, data: { expectedRevision: plan.revision, reason: "Isolated test publication", expectedActivePlanId: null, expectedActiveRevision: null } }));
  });

  for (const method of [0, 1]) for (const paymentMethod of [0, 1, 2]) {
    test(`${method === 0 ? "WorkspaceEC" : "MiniLogistics"} / ${["COD", "transfer", "VNPay"][paymentMethod]} keeps its accepted price through delivery`, async ({ page, request }) => {
      test.setTimeout(120_000);
      const number = method * 3 + paymentMethod;
      const customer = await data<CustomerAuthResponse>(await request.post(`${api}/api/customer/auth/register`, { data: { email: `shipping-${randomUUID()}@example.test`, password: `Synthetic-${randomUUID()}!`, fullName: "Synthetic Buyer", phoneNumber: `090000000${number}` } }));
      const customerHeaders = { Authorization: `Bearer ${customer.accessToken}` };
      await page.addInitScript(session => sessionStorage.setItem("workspace-ecommerce-customer-session", JSON.stringify(session)), customer);
      const before = await providerState(request);
      expect((await request.post(`${provider}/__test/outage`, { headers: providerHeaders, data: { enabled: method === 0 } })).ok()).toBeTruthy();
      // Only the synthetic gateway page is intercepted. All commerce requests use the real API.
      await page.route(`${api}/__test/vnpay**`, route => route.fulfill({ contentType: "text/html", body: "Synthetic VNPay gateway" }));
      await page.goto("/products/atlas-standing-desk");
      await page.getByRole("radio", { name: /^Oak \/ 140cm/ }).click();
      await page.getByRole("button", { name: "Add to cart" }).click();
      await expect(page.getByRole("dialog", { name: "Cart" })).toBeVisible();
      await page.goto("/checkout");
      await page.getByLabel("Full name", { exact: true }).fill("Synthetic Buyer");
      await page.getByLabel("Phone number", { exact: true }).fill(`090000000${number}`);
      await page.getByLabel("Address", { exact: true }).fill("1 Synthetic Street");
      await page.getByLabel("Province/City", { exact: true }).selectOption("01");
      await page.getByLabel("Ward/Commune", { exact: true }).selectOption("00004");
      await page.getByRole("radio", { name: [ /^COD\b/, /^Bank Transfer\b/, /^VNPay\b/ ][paymentMethod] }).check();
      const quoted = page.waitForResponse(response => response.url().endsWith("/api/checkout/shipping/quotes") && response.request().method() === "POST");
      await page.getByRole("radio", { name: method === 0 ? "WorkspaceEC" : "MiniLogistics", exact: true }).check();
      const quote = (await (await quoted).json()).data as ShippingQuoteDto;
      expect(quote.method).toBe(method); expect(quote.customerCharge).toBeGreaterThan(0);
      await page.getByRole("checkbox", { name: "I accept this shipping price" }).check();
      const checkout = page.waitForResponse(response => response.url().endsWith("/api/checkout") && response.request().method() === "POST");
      await page.getByRole("button", { name: ["Place order (COD)", "Place order and get transfer info", "Continue to VNPay"][paymentMethod], exact: true }).click();
      const acceptedResponse = await checkout;
      expect(acceptedResponse.ok()).toBeTruthy();
      expect(acceptedResponse.request().postDataJSON().shippingQuoteId).toBe(quote.id);
      if (paymentMethod === 2) await page.waitForURL(`${api}/__test/vnpay**`);
      else await expect(page).toHaveURL(/\/checkout\/success\?/);
      // Read persisted state independently of Chromium response-body lifetime
      // during a cross-origin gateway navigation. Commerce traffic is never mocked.
      const orders = await data<PagedResult<CustomerOrderListItemDto>>(await request.get(`${api}/api/customer/orders`, { headers: customerHeaders }));
      expect(orders.items).toHaveLength(1);
      const order = await data<CustomerOrderDto>(await request.get(`${api}/api/customer/orders/${orders.items[0].id}`, { headers: customerHeaders }));
      expect(order.shipping?.method).toBe(method);
      expect(order.shippingFee).toBe(quote.customerCharge);
      expect(order.totalAmount).toBe(order.subtotal + quote.customerCharge - order.discountAmount);
      expect(order.shipping?.customerCharge).toBe(quote.customerCharge);
      if (paymentMethod === 2) {
        const paymentUrl = page.url();
        expect(Number(new URL(paymentUrl).searchParams.get("vnp_Amount"))).toBe(order.totalAmount * 100);
        const callback = signedCallback(paymentUrl, number);
        const ipn = await request.get(`${api}/api/payments/vnpay/ipn?${callback}`);
        expect((await ipn.json()).RspCode).toBe("00");
        // Duplicate callback is harmless; browser return still uses the signed, real endpoint.
        await request.get(`${api}/api/payments/vnpay/ipn?${callback}`);
        await page.goto(`${api}/api/payments/vnpay/return?${callback}`);
        await expect(page.getByRole("heading", { name: "Payment completed.", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: `Delivery method: ${method === 0 ? "WorkspaceEC" : "MiniLogistics"}` })).toBeVisible();
      } else {
        await expect(page).toHaveURL(/\/checkout\/success\?/);
        await expect(page.getByRole("heading", { name: `Delivery method: ${method === 0 ? "WorkspaceEC" : "MiniLogistics"}` })).toBeVisible();
      }
      await status(request, order.id, 1);
      if (paymentMethod === 1) {
        if (method === 0) {
          const unpaid = await data<WorkspaceShipmentDto>(await request.get(`${api}/api/admin/orders/${order.id}/workspace-shipment`, { headers: adminHeaders }));
          expect(unpaid.canDispatch).toBe(false);
        } else {
          const unpaid = await data<ShipmentTrackingDto>(await request.get(`${api}/api/admin/orders/${order.id}/shipment`, { headers: adminHeaders }));
          expect(unpaid.trackingCode).toBeNull();
        }
        await data(await request.post(`${api}/api/admin/orders/${order.id}/payment/confirm-transfer`, { headers: adminHeaders, data: { amount: order.totalAmount, currency: "VND", reference: `SYN-TRANSFER-${order.id}`, reason: "Synthetic settlement" } }));
      }
      if (method === 0) {
        let local = await data<WorkspaceShipmentDto>(await request.get(`${api}/api/admin/orders/${order.id}/workspace-shipment`, { headers: adminHeaders }));
        expect(local.id).toBe(order.shipping!.localShipmentId);
        expect(order.shipmentId ?? null).toBeNull();
        local = await localAction(request, order.id, local.revision, 0);
        local = await localAction(request, order.id, local.revision, 1);
        await localAction(request, order.id, local.revision, 2);
      } else {
        let tracking: ShipmentTrackingDto;
        await expect.poll(async () => {
          tracking = await data<ShipmentTrackingDto>(await request.get(`${api}/api/admin/orders/${order.id}/shipment`, { headers: adminHeaders }));
          return tracking.trackingCode;
        }, { timeout: 30_000 }).toMatch(/^SYN-/);
        await status(request, order.id, 2); await status(request, order.id, 3);
        expect((await request.post(`${provider}/__test/status`, { headers: providerHeaders, data: { trackingCode: tracking!.trackingCode, status: "Delivered" } })).ok()).toBeTruthy();
        await data(await request.post(`${api}/api/admin/orders/${order.id}/shipment/refresh`, { headers: adminHeaders }));
      }
      if (paymentMethod === 0) {
        const delivered = await data<CustomerOrderDto>(await request.get(`${api}/api/customer/orders/${order.id}`, { headers: customerHeaders }));
        expect(delivered.status).toBe(3); expect(delivered.paymentStatus).toBe(0);
        const collect = { amount: order.totalAmount, currency: "VND", reference: `SYN-COD-${order.id}`, reason: "Synthetic full collection" };
        await data(await request.post(`${api}/api/admin/orders/${order.id}/payment/collect-cod`, { headers: adminHeaders, data: collect }));
        await data(await request.post(`${api}/api/admin/orders/${order.id}/payment/collect-cod`, { headers: adminHeaders, data: collect }));
      }
      const completed = await data<CustomerOrderDto>(await request.get(`${api}/api/customer/orders/${order.id}`, { headers: customerHeaders }));
      expect(completed.status).toBe(4); expect(completed.paymentStatus).toBe(2);
      expect(completed.totalAmount).toBe(order.totalAmount);
      expect(completed.shipping?.customerCharge).toBe(quote.customerCharge);
      const proof = `orderCode=${order.orderCode}&phone=${order.customerPhone}`;
      const lookup = await request.get(`${api}/api/orders/lookup?${proof}`);
      const raw = await lookup.text();
      expect(lookup.ok()).toBeTruthy(); expect(raw).not.toMatch(/actualCost|assignedTo|Synthetic private operation/);
      expect((await lookup.json()).data.order.shipping.customerCharge).toBe(quote.customerCharge);
      const receipt = await request.get(`${api}/api/orders/lookup/receipt?${proof}&language=en`);
      expect(receipt.ok(), `${receipt.status()}: ${receipt.ok() ? "" : await receipt.text()}`).toBeTruthy(); expect(receipt.headers()["content-type"]).toContain("application/pdf");
      const pdf = await receipt.body(); expect(pdf.subarray(0, 4).toString()).toBe("%PDF");
      const directory = resolve(process.env.E2E_ARTIFACTS_DIR!, "receipts");
      await mkdir(directory, { recursive: true }); await writeFile(resolve(directory, `${method}-${paymentMethod}.pdf`), pdf);
      const after = await providerState(request);
      if (method === 0) expect(after.counts).toEqual(before.counts);
      else { expect(after.counts.quote - before.counts.quote).toBe(1); expect(after.counts.create - before.counts.create).toBe(1); }
    });
  }
});
