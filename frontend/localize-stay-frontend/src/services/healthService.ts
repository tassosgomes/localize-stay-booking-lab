import type { FrontendEnv } from '../config/env.ts';

export type ServiceName = 'Catalog' | 'Booking' | 'Payment';

export type HealthStatus = 'Healthy' | 'Unhealthy';

export interface ServiceHealth {
  name: ServiceName;
  status: HealthStatus;
  detail: string;
}

// O endpoint /health/ready dos serviços responde texto "Healthy" (200).
// Qualquer resposta diferente de 200, corpo inesperado ou falha de rede
// (incluindo erro de CORS) vira Unhealthy com o detalhe para diagnóstico.
export async function checkHealth(name: ServiceName, baseUrl: string): Promise<ServiceHealth> {
  try {
    const response = await fetch(`${baseUrl}/health/ready`);
    if (!response.ok) {
      return { name, status: 'Unhealthy', detail: `HTTP ${response.status}` };
    }
    const body = (await response.text()).trim();
    return body === 'Healthy'
      ? { name, status: 'Healthy', detail: body }
      : { name, status: 'Unhealthy', detail: body === '' ? 'Empty response' : body };
  } catch (error) {
    return {
      name,
      status: 'Unhealthy',
      detail: error instanceof Error ? error.message : 'Unknown error',
    };
  }
}

export function checkAllServices(env: FrontendEnv): Promise<ServiceHealth[]> {
  return Promise.all([
    checkHealth('Catalog', env.catalogUrl),
    checkHealth('Booking', env.bookingUrl),
    checkHealth('Payment', env.paymentUrl),
  ]);
}
