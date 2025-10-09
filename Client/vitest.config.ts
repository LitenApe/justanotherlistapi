import { configDefaults, defineConfig } from 'vitest/config';

import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig({
  plugins: [react(), tsconfigPaths()],
  test: {
    browser: {
      enabled: true,
      headless: true,
      provider: 'playwright',
      instances: [{ browser: 'chromium' }],
    },
    reporters: [
      'default',
      [
        'junit',
        {
          outputFile: './coverage/test-results.xml',
          includeConsoleOutput: false,
        },
      ],
    ],
    coverage: {
      reporter: 'cobertura',
      include: ['src/**/*.ts', 'src/**/*.tsx'],
      exclude: ['src/routes/ts', 'src/root.tsx'],
    },
    exclude: [...configDefaults.exclude, '**/test/e2e/**'],
  },
  optimizeDeps: {
    include: ['react/jsx-dev-runtime'],
  },
});
