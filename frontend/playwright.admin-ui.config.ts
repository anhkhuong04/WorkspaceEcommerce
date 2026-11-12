import { resolve } from "node:path";
import { defineConfig } from "@playwright/test";
import { assertLoopbackHttpUrl } from "./e2e/safety";

const baseURL = assertLoopbackHttpUrl(
  "Admin UI test URL",
  "http://127.0.0.1:4186",
);
export default defineConfig({
  testDir: "./e2e",
  testMatch: ["admin-ui.spec.ts", "shipping-admin.spec.ts"],
  workers: 1,
  timeout: 120_000,
  outputDir: resolve(
    "../artifacts/frontend/admin/ui",
    process.env.UI_CAPTURE_LABEL ?? "verified",
  ),
  reporter: "list",
  use: {
    baseURL,
    browserName: "chromium",
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
  webServer: {
    command:
      "corepack pnpm --filter @workspace-ecommerce/admin exec vite --host 127.0.0.1 --port 4186 --strictPort",
    url: baseURL,
    reuseExistingServer: false,
    env: { VITE_API_BASE_URL: "" },
  },
});
