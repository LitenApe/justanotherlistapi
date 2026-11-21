import { reactRouter } from "@react-router/dev/vite";
import { defineConfig } from "vite";

// Aspire injects service discovery variables for referenced resources.
// When running standalone, fall back to the default local ports.
const apiUrl =
  process.env.services__Api__https__0 ??
  process.env.services__Api__http__0 ??
  "http://localhost:5093";

export default defineConfig({
  plugins: [reactRouter()],
  server: {
    port: Number(process.env.PORT ?? 5173),
    strictPort: true,
    proxy: {
      "/api": { target: apiUrl, changeOrigin: true, secure: false },
      "/hubs": { target: apiUrl, changeOrigin: true, secure: false, ws: true },
    },
  },
  build: {
    sourcemap: true,
  },
});
