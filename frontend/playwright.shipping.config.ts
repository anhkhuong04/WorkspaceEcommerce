import { defineConfig } from "@playwright/test";
import { assertLoopbackHttpUrl } from "./e2e/safety";
const baseURL = assertLoopbackHttpUrl("Shipping UI test URL", "http://127.0.0.1:4187");
export default defineConfig({
  testDir: "./e2e", testMatch: "shipping-admin.spec.ts", workers: 1,
  outputDir: "../artifacts/frontend/shipping-ui", reporter: "list",
  use: { baseURL, browserName: "chromium", screenshot: "only-on-failure", trace: "retain-on-failure" },
  webServer: { command: "corepack pnpm --filter @workspace-ecommerce/admin exec vite --host 127.0.0.1 --port 4187 --strictPort", url: baseURL, reuseExistingServer: false, env: { VITE_API_BASE_URL: "" } },
});
