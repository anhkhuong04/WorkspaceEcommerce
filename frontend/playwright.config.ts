import { resolve } from "node:path";
import { defineConfig, devices } from "@playwright/test";
import { getLoopbackHttpUrl } from "./e2e/safety";

if (process.env.E2E_ISOLATED_RUN !== "true") {
  throw new Error(
    "Refusing to run browser E2E without E2E_ISOLATED_RUN=true. Use ../scripts/run-prh-015-storefront-e2e.ps1 so data and dependencies are isolated."
  );
}

const storefrontUrl = getLoopbackHttpUrl("E2E_STOREFRONT_URL", "http://127.0.0.1:4173");
const apiProxyTarget = getLoopbackHttpUrl("E2E_API_PROXY_TARGET", "http://127.0.0.1:5080");
const storefrontPort = getBoundedPort("E2E_STOREFRONT_PORT", 4173);
const storefrontUrlPort = new URL(storefrontUrl).port;

if (storefrontUrlPort !== String(storefrontPort)) {
  throw new Error("E2E_STOREFRONT_URL and E2E_STOREFRONT_PORT must describe the same isolated Vite server.");
}

const artifactsDirectory = resolve(process.env.E2E_ARTIFACTS_DIR ?? "test-results/playwright");

export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  timeout: 45_000,
  expect: {
    timeout: 12_000
  },
  forbidOnly: Boolean(process.env.CI),
  fullyParallel: false,
  workers: 1,
  outputDir: resolve(artifactsDirectory, "test-results"),
  reporter: [
    [process.env.CI ? "line" : "list"],
    ["html", { outputFolder: resolve(artifactsDirectory, "playwright-report"), open: "never" }]
  ],
  use: {
    baseURL: storefrontUrl,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure"
  },
  webServer: {
    command: `corepack pnpm --filter @workspace-ecommerce/storefront exec vite --host 127.0.0.1 --port ${storefrontPort} --strictPort`,
    url: storefrontUrl,
    timeout: 45_000,
    reuseExistingServer: false,
    env: {
      ...process.env,
      VITE_API_BASE_URL: "",
      VITE_API_PROXY_TARGET: apiProxyTarget,
      VITE_CART_SESSION_ID: "prh015-e2e-cart"
    }
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] }
    }
  ]
});

function getBoundedPort(variableName: string, fallback: number): number {
  const value = process.env[variableName] ?? String(fallback);
  const port = Number(value);

  if (!Number.isInteger(port) || port < 1024 || port > 65535) {
    throw new Error(`${variableName} must be an integer between 1024 and 65535.`);
  }

  return port;
}
