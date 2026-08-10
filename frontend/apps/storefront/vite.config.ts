import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const apiProxyTarget = process.env.VITE_API_PROXY_TARGET ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      "/api": apiProxyTarget
    }
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
    clearMocks: true,
    restoreMocks: true
  }
});
