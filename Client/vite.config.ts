/// <reference types="vitest/config" />
import { reactRouter } from "@react-router/dev/vite";
import { defineConfig } from "vite";

// Aspire injects service discovery variables for referenced resources.
// When running standalone, fall back to the default local ports.
const coreUrl =
  process.env.services__Core__https__0 ??
  process.env.services__Core__http__0 ??
  "http://localhost:5093";
const oauthUrl = process.env.services__oauth__http__0 ?? "http://localhost:8080";

export default defineConfig({
  // The React Router plugin builds the app; Vitest renders route modules directly instead.
  plugins: process.env.VITEST ? [] : [reactRouter()],
  server: {
    port: Number(process.env.PORT ?? 5173),
    strictPort: true,
    proxy: {
      "/api": { target: coreUrl, changeOrigin: true, secure: false },
      "/hubs": { target: coreUrl, changeOrigin: true, secure: false, ws: true },
      "/default": { target: oauthUrl, changeOrigin: true },
    },
  },
  build: {
    sourcemap: true,
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: { modules: { classNameStrategy: "non-scoped" } },
  },
});
