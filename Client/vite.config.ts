import { defineConfig } from "vite";
import path from "path";
import react from "@vitejs/plugin-react-swc";

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    port: parseInt(process.env.PORT ?? "5173"),
    proxy: {
      "/api": {
        target:
          process.env.services__Core__https__0 ??
          process.env.services__Core__http__0 ??
          "http://localhost:55733",
        changeOrigin: true,
        secure: false,
      },
      "/hubs": {
        target:
          process.env.services__Core__https__0 ??
          process.env.services__Core__http__0 ??
          "http://localhost:55733",
        changeOrigin: true,
        secure: false,
        ws: true,
      },
      "/default": {
        target: process.env.services__oauth__http__0 ?? "http://localhost:8080",
        changeOrigin: true,
      },
    },
  },
  resolve: {
    alias: {
      "@shared": path.resolve(__dirname, "src/shared"),
      "@slices": path.resolve(__dirname, "src/slices"),
      "@components": path.resolve(__dirname, "src/components"),
    },
  },
  build: {
    sourcemap: true,
  },
  esbuild:
    mode === "production" ? { drop: ["console", "debugger"] } : undefined,
}));
