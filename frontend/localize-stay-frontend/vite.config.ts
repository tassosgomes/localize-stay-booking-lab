import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// Frontend de teste da fundação (V-04, ADR-003): roda apenas no Vite dev
// server local (porta 5173). Sem pipeline de runtime-config nesta fase.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
  },
});
