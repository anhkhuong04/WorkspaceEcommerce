import { expect, test, type Page } from "@playwright/test";
import type { ShippingRatePlan } from "@workspace-ecommerce/api-types";

const id = "11111111-1111-4111-8111-111111111111";
const now = "2026-10-29T00:00:00Z";
function draft(): ShippingRatePlan { return { id, name: "Approved rates", catalogVersion: "test-v1", originProvinceCode: "01", status: 0, revision: 2, createdAtUtc: now, updatedAtUtc: now, publishedAtUtc: null, retiredAtUtc: null, zones: [{ code: "ALL", name: "Nationwide", baseWeightGrams: 1000, baseFee: 20000, stepWeightGrams: 500, stepFee: 3000, maximumWeightGrams: null, provinceCodes: ["01"] }], coverage: { isComplete: true, missingProvinceCodes: [], unknownProvinceCodes: [], errors: [] } }; }
const paged = <T,>(items: T[]) => ({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1, hasNextPage: false, hasPreviousPage: false });
async function setup(page: Page, conflict = false, empty = false) {
  let plan = draft();
  await page.addInitScript(() => localStorage.setItem("workspace-ecommerce-admin-session", JSON.stringify({ accessToken: "synthetic-ui-token", expiresAt: "2099-01-01T00:00:00Z", email: "admin@example.test" })));
  await page.route("**/api/**", async route => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (!path.startsWith("/api/")) return route.continue();
    const ok = (data: unknown) => route.fulfill({ json: { success: true, data, errors: [], traceId: "synthetic" } });
    if (path.endsWith("/catalog")) return ok({ version: "test-v1", provinces: [{ code: "01", name: "Ha Noi" }] });
    if (path.endsWith("/wards")) return ok(paged([{ code: "00001", name: "Ward One" }]));
    if (path.endsWith("/weight-issues") || path.endsWith("/history")) return ok(paged([]));
    if (path.endsWith("/rate-plans") && route.request().method() === "GET") return ok(paged(url.searchParams.get("status") === "1" && plan.status !== 1 || empty ? [] : [plan]));
    if (path.endsWith("/preview")) return ok({ ratePlanId: id, revision: plan.revision, status: plan.status, isPreview: true, canPublish: true, zoneCode: "ALL", destination: {}, calculation: { weightGrams: 1000, totalFee: 20000, currency: "VND" } });
    if (path.endsWith("/publish")) { plan = { ...plan, status: 1, revision: plan.revision + 1 }; return ok(plan); }
    if (route.request().method() === "PUT") {
      if (conflict) return route.fulfill({ status: 409, json: { success: false, data: null, errors: ["Revision changed. Reload the latest plan."], traceId: "synthetic" } });
      plan = { ...plan, ...route.request().postDataJSON(), revision: plan.revision + 1 }; return ok(plan);
    }
    if (path.endsWith(id)) return ok(plan);
    return route.fulfill({ status: 404, json: { success: false, data: null, errors: ["Unexpected mock request"] } });
  });
  await page.goto("/shipping");
}
test("edit, preview saved price, confirm publication and retain history", async ({ page }) => {
  await setup(page);
  await page.getByRole("button", { name: /Approved rates · Draft/ }).click();
  await page.getByLabel("Name", { exact: true }).fill("Reviewed rates");
  await page.getByLabel("Reason", { exact: true }).fill("Approved commercial prices");
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByText("Shipping configuration saved")).toBeVisible();
  await page.getByLabel("Destination province").selectOption("01");
  await page.getByLabel("Destination ward").selectOption("00001");
  await page.getByRole("button", { name: "Preview price" }).click();
  await expect(page.getByText(/Preview: 20/)).toBeVisible();
  await page.getByLabel("Lifecycle reason").fill("Verified all coverage");
  await page.getByRole("button", { name: "Publish plan", exact: true }).click();
  await expect(page.getByRole("alertdialog")).toContainText("Valid existing quotes");
  await page.getByRole("button", { name: "Confirm publish" }).click();
  await expect(page.getByRole("heading", { name: /Reviewed rates · Published/ })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Change history" })).toBeVisible();
});
test("version conflict retains edits and offers reload", async ({ page }) => {
  await setup(page, true);
  await page.getByRole("button", { name: /Approved rates · Draft/ }).click();
  await page.getByLabel("Name", { exact: true }).fill("My retained change");
  await page.getByLabel("Reason", { exact: true }).fill("Review");
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByText("Revision changed. Reload the latest plan.", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Name", { exact: true })).toHaveValue("My retained change");
  await expect(page.getByRole("button", { name: "Reload latest revision" })).toBeVisible();
});
test("empty state and keyboard validation", async ({ page }) => {
  await setup(page, false, true);
  await expect(page.getByText(/No rate plans yet/)).toBeVisible();
  await page.getByRole("button", { name: "New rate plan" }).focus();
  await page.keyboard.press("Enter");
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByLabel("Name", { exact: true })).toBeFocused();
  await expect(page.getByLabel("Name", { exact: true })).toHaveAttribute("aria-invalid", "true");
});
