// Adapter HTTP da feature Solicitação de Reserva — único ponto de contato do
// frontend com Booking F01 (POST /v1/reservations). Consome exclusivamente os
// tipos gerados de tasks/prd-solicitacao-reserva/api-contract.yaml; a UI (task
// 5.0) nunca importa paths/components diretamente, só o resultado tipado daqui.
import { getBookingApiUrl } from '../../../config/env.ts';
import { apiRequest } from '../../../services/apiClient.ts';
import type { components, paths } from '../../../services/api/generated/booking.ts';

const RESERVATIONS_PATH = '/reservations' satisfies keyof paths;

export type CreateReservationRequest = components['schemas']['CreateReservationRequest'];
export type Reservation = components['schemas']['Reservation'];
export type ProblemDetails = components['schemas']['ProblemDetails'];

const REJECTION_CODES = [
  'PERIODO_INVALIDO',
  'QUANTIDADE_HOSPEDES_INVALIDA',
  'ACOMODACAO_INDISPONIVEL',
  'CAPACIDADE_EXCEDIDA',
  'PERIODO_INDISPONIVEL',
] as const satisfies readonly NonNullable<ProblemDetails['code']>[];

export type RejectionCode = (typeof REJECTION_CODES)[number];

export type ReservationApiResult =
  | { kind: 'success'; reservation: Reservation }
  | { kind: 'rejected'; code: RejectionCode; status: 422 }
  | { kind: 'unavailable'; status: 503; traceId?: string }
  | { kind: 'malformed'; status: 400 }
  | { kind: 'failed'; status?: number; traceId?: string };

function isRejectionCode(code: unknown): code is RejectionCode {
  return typeof code === 'string' && (REJECTION_CODES as readonly string[]).includes(code);
}

function optionalString(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

async function parseProblemDetails(response: Response): Promise<Partial<ProblemDetails>> {
  try {
    const body: unknown = await response.json();
    if (typeof body === 'object' && body !== null) {
      return body as Partial<ProblemDetails>;
    }
    return {};
  } catch {
    return {};
  }
}

// `request` classifica a resposta do contrato sem interpretar title/detail;
// corpo malformado, resposta não contratual ou falha de rede viram `failed` —
// o adapter nunca lança exceção não tratada ao chamador (cancelamento via
// AbortSignal é decisão do chamador e propaga o AbortError).
export async function request(
  input: CreateReservationRequest,
  signal: AbortSignal,
): Promise<ReservationApiResult> {
  try {
    const response = await apiRequest({
      baseUrl: getBookingApiUrl(),
      path: RESERVATIONS_PATH,
      method: 'POST',
      body: input,
      signal,
    });

    if (response.status === 201) {
      try {
        const reservation = (await response.json()) as Reservation;
        return { kind: 'success', reservation };
      } catch {
        return { kind: 'failed', status: response.status };
      }
    }

    const problem = await parseProblemDetails(response);
    const code = problem.code ?? undefined;
    const traceId = optionalString(problem.traceId);

    if (response.status === 400 && code === 'VALIDATION_ERROR') {
      return { kind: 'malformed', status: 400 };
    }
    if (response.status === 422 && isRejectionCode(code)) {
      return { kind: 'rejected', code, status: 422 };
    }
    if (response.status === 503 && code === 'CATALOG_INDISPONIVEL') {
      return { kind: 'unavailable', status: 503, traceId };
    }
    return { kind: 'failed', status: response.status, traceId };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    return { kind: 'failed' };
  }
}

export const reservationApi = { request };

export { REJECTION_CODES };
