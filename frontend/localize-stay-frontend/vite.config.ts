import path from 'node:path';
import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const rootDir = fileURLToPath(new URL('.', import.meta.url));

// Frontend de teste da fundação (V-04, ADR-003): roda apenas no Vite dev
// server local (porta 5173). Sem pipeline de runtime-config nesta fase.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(rootDir, 'src'),
      '@features': path.resolve(rootDir, 'src/features'),
    },
  },
  server: {
    port: 5173,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
    exclude: ['**/node_modules/**', '**/dist/**', '**/e2e/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text-summary'],
      exclude: [
        'src/services/api/generated/**',
        'src/test/**',
        'src/main.tsx',
        '**/*.test.ts',
        '**/*.test.tsx',
        'e2e/**',
      ],
      thresholds: {
        lines: 70,
        functions: 70,
        branches: 70,
        statements: 70,
      },
    },
  },
});
