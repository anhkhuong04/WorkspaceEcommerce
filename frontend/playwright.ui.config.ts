import { resolve } from "node:path";
import { defineConfig } from "@playwright/test";
import { assertLoopbackHttpUrl } from "./e2e/safety";

// UI-only fixtures intercept every API call; no backend or provider is contacted.
const baseURL = assertLoopbackHttpUrl("UI test URL", "http://127.0.0.1:4185");
const outputDir = resolve("../artifacts/frontend/ui", process.env.UI_CAPTURE_LABEL ?? "after");

export default defineConfig({
  testDir: "./e2e",
  testMatch: "storefront-ui.spec.ts",
  workers: 1,
  timeout: 60_000,
  outputDir,
  reporter: "list",
  use: { baseURL, browserName: "chromium", screenshot: "only-on-failure", trace: "retain-on-failure" },
  webServer: {
    command: "corepack pnpm --filter @workspace-ecommerce/storefront exec vite --host 127.0.0.1 --port 4185 --strictPort",
    url: baseURL,
    reuseExistingServer: false,
    env: { VITE_API_BASE_URL: "", VITE_API_PROXY_TARGET: "http://127.0.0.1:1", VITE_GOOGLE_CLIENT_ID: "" }
  }
});
