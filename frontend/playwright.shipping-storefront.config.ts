import { defineConfig } from "@playwright/test";
import { assertLoopbackHttpUrl } from "./e2e/safety";
const baseURL = assertLoopbackHttpUrl(
  "Shipping storefront UI",
  "http://127.0.0.1:4188",
);
export default defineConfig({
  testDir: "./e2e",
  testMatch: "shipping-storefront.spec.ts",
  timeout: 45000,
  workers: 1,
  reporter: "list",
  outputDir: "../artifacts/frontend/shipping-storefront",
  use: {
    baseURL,
    browserName: "chromium",
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
  webServer: {
    command:
      "corepack pnpm --filter @workspace-ecommerce/storefront exec vite --host 127.0.0.1 --port 4188 --strictPort",
    url: baseURL,
    reuseExistingServer: false,
    env: { VITE_API_BASE_URL: "" },
  },
});
