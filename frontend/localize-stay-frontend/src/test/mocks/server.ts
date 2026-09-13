import { setupServer } from 'msw/node';
import { handlers } from './handlers.ts';

// Servidor MSW compartilhado do Vitest. Cada arquivo de teste gerencia o
// próprio ciclo de vida (listen/resetHandlers/close) declarado no arquivo,
// padrão do repo para funcionar tanto via `npm run test` quanto via
// `vitest run <filtro>` invocado da raiz (gate), onde o config do frontend
// não é carregado.
export const server = setupServer(...handlers);
