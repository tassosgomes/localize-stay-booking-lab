// Adapter HTTP da feature Consulta de Reserva — único ponto de contato do
// frontend com Booking F02 (GET /v1/reservations/{reservationId}). Consome
// exclusivamente os tipos gerados de tasks/prd-consulta-reserva/api-contract.yaml;
// a UI (task 4.0) nunca importa paths/components diretamente, só o resultado
// tipado daqui.
import { getBookingApiUrl } from '../../../config/env.ts';
import { apiRequest } from '../../../services/apiClient.ts';
import type { components } from '../../../services/api/generated/reservationDetail.ts';

export type ReservationDetail = components['schemas']['ReservationDetail'];
export type ReservationDetailProblemDetails = components['schemas']['ProblemDetails'];

export type ReservationDetailApiResult =
  | { kind: 'found'; reservation: ReservationDetail }
  | { kind: 'notFound' }
  | { kind: 'malformed' }
  | { kind: 'failed'; status?: number; traceId?: string };

function optionalString(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

async function parseProblemDetails(
  response: Response,
): Promise<Partial<ReservationDetailProblemDetails>> {
  try {
    const body: unknown = await response.json();
    if (typeof body === 'object' && body !== null) {
      return body as Partial<ReservationDetailProblemDetails>;
    }
    return {};
  } catch {
    return {};
  }
}

// `getById` classifica a resposta do contrato por (status, code), sem
// interpretar title/detail; corpo malformado, resposta não contratual ou falha
// de rede viram `failed` — o adapter nunca lança exceção não tratada ao
// chamador (cancelamento via AbortSignal propaga o AbortError para o chamador
// decidir, mesmo princípio de reservationApi.ts de F01).
export async function getById(
  reservationId: string,
  signal: AbortSignal,
): Promise<ReservationDetailApiResult> {
  try {
    const response = await apiRequest({
      baseUrl: getBookingApiUrl(),
      path: `/reservations/${reservationId}`,
      method: 'GET',
      signal,
    });

    if (response.status === 200) {
      try {
        return { kind: 'found', reservation: (await response.json()) as ReservationDetail };
      } catch {
        return { kind: 'failed', status: response.status };
      }
    }

    const problem = await parseProblemDetails(response);
    if (response.status === 400 && problem.code === 'VALIDATION_ERROR') {
      return { kind: 'malformed' };
    }
    if (response.status === 404 && problem.code === 'RESERVATION_NOT_FOUND') {
      return { kind: 'notFound' };
    }
    return { kind: 'failed', status: response.status, traceId: optionalString(problem.traceId) };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    return { kind: 'failed' };
  }
}

export const reservationDetailApi = { getById };
