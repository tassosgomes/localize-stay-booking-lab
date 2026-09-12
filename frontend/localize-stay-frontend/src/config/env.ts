// URLs dos backends locais via variáveis VITE_* (.env.development).
// Leitura em tempo de chamada (não no import) para que testes possam fixar
// os valores com vi.stubEnv antes da renderização.
export interface FrontendEnv {
  catalogUrl: string;
  bookingUrl: string;
  paymentUrl: string;
}

function required(name: 'VITE_CATALOG_URL' | 'VITE_BOOKING_URL' | 'VITE_PAYMENT_URL'): string {
  const value = import.meta.env[name] as string | undefined;
  if (value === undefined || value === '') {
    throw new Error(`Missing required environment variable ${name} (see .env.development)`);
  }
  return value;
}

export function getEnv(): FrontendEnv {
  return {
    catalogUrl: required('VITE_CATALOG_URL'),
    bookingUrl: required('VITE_BOOKING_URL'),
    paymentUrl: required('VITE_PAYMENT_URL'),
  };
}

export function catalogApiBaseUrl(catalogUrl: string): string {
  const trimmed = catalogUrl.replace(/\/+$/, '');
  return trimmed.endsWith('/v1') ? trimmed : `${trimmed}/v1`;
}
