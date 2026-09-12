import { catalogApiBaseUrl, getEnv } from '../../../config/env.ts';
import { apiRequest } from '../../../services/apiClient.ts';
import type { components, operations, paths } from '../../../services/api/generated/catalog.ts';

export type Property = components['schemas']['Property'];
export type CreatePropertyRequest = components['schemas']['CreatePropertyRequest'];
export type UpdatePropertyRequest = components['schemas']['UpdatePropertyRequest'];
export type ProblemDetailItem = components['schemas']['ProblemDetailItem'];

type CreatePropertyOperation = operations['createProperty'];
type HostReferenceId = CreatePropertyOperation['parameters']['header']['X-Host-Reference-Id'];
type PropertyId = paths['/properties/{propertyId}']['parameters']['path']['propertyId'];

export const HOST_REFERENCE_HEADER = 'X-Host-Reference-Id';

export type ProblemKind = 'problem' | 'network' | 'invalid-json' | 'unexpected';

export interface NormalizedProblem {
  kind: ProblemKind;
  status: number | null;
  code: string | null;
  type: string | null;
  title: string | null;
  detail: string;
  instance: string | null;
  details: ProblemDetailItem[];
  traceId: string | null;
}

export type PropertyApiSuccess = {
  ok: true;
  status: 200 | 201;
  data: Property;
};

export type PropertyApiFailure = {
  ok: false;
  problem: NormalizedProblem;
};

export type PropertyApiResult = PropertyApiSuccess | PropertyApiFailure;

const UNAVAILABLE_DETAIL = 'O Catalog está indisponível no momento. Tente novamente.';

function omitUndefinedFields(changes: UpdatePropertyRequest): UpdatePropertyRequest {
  return Object.fromEntries(
    Object.entries(changes).filter(([, value]) => value !== undefined),
  ) as UpdatePropertyRequest;
}

function catalogPropertiesUrl(): string {
  return `${catalogApiBaseUrl(getEnv().catalogUrl)}/properties`;
}

function catalogPropertyUrl(propertyId: PropertyId): string {
  return `${catalogPropertiesUrl()}/${encodeURIComponent(propertyId)}`;
}

function hostHeaders(hostReferenceId: HostReferenceId): Record<string, string> {
  return {
    [HOST_REFERENCE_HEADER]: hostReferenceId,
    Accept: 'application/json, application/problem+json',
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(value: unknown): string | null {
  return typeof value === 'string' && value !== '' ? value : null;
}

function readNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function readDetails(value: unknown): ProblemDetailItem[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const details: ProblemDetailItem[] = [];
  for (const item of value) {
    if (!isRecord(item)) {
      continue;
    }
    const field = readString(item.field);
    const message = readString(item.message);
    if (field !== null && message !== null) {
      details.push({ field, message });
    }
  }
  return details;
}

export function normalizeProblemPayload(payload: unknown, httpStatus: number): NormalizedProblem {
  if (!isRecord(payload)) {
    return {
      kind: 'unexpected',
      status: httpStatus,
      code: null,
      type: null,
      title: null,
      detail: UNAVAILABLE_DETAIL,
      instance: null,
      details: [],
      traceId: null,
    };
  }

  return {
    kind: 'problem',
    status: readNumber(payload.status) ?? httpStatus,
    code: readString(payload.code),
    type: readString(payload.type),
    title: readString(payload.title),
    detail: readString(payload.detail) ?? UNAVAILABLE_DETAIL,
    instance: readString(payload.instance),
    details: readDetails(payload.details),
    traceId: readString(payload.traceId),
  };
}

function invalidJsonProblem(httpStatus: number): NormalizedProblem {
  return {
    kind: 'invalid-json',
    status: httpStatus,
    code: null,
    type: null,
    title: null,
    detail: UNAVAILABLE_DETAIL,
    instance: null,
    details: [],
    traceId: null,
  };
}

function networkProblem(): NormalizedProblem {
  return {
    kind: 'network',
    status: null,
    code: null,
    type: null,
    title: null,
    detail: UNAVAILABLE_DETAIL,
    instance: null,
    details: [],
    traceId: null,
  };
}

function isProperty(value: unknown): value is Property {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.name === 'string' &&
    typeof value.location === 'string' &&
    typeof value.hostReferenceId === 'string' &&
    (value.status === 'active' || value.status === 'inactive')
  );
}

function isAbortError(error: unknown): boolean {
  return (
    (error instanceof DOMException && error.name === 'AbortError') ||
    (error instanceof Error && error.name === 'AbortError')
  );
}

async function parseJsonBody(
  response: Response,
): Promise<{ ok: true; value: unknown } | { ok: false; problem: NormalizedProblem }> {
  try {
    return { ok: true, value: await response.json() };
  } catch {
    return { ok: false, problem: invalidJsonProblem(response.status) };
  }
}

async function send(
  method: 'POST' | 'PATCH',
  url: string,
  hostReferenceId: HostReferenceId,
  body: unknown,
  signal?: AbortSignal,
): Promise<PropertyApiResult> {
  let response: Response;
  try {
    response = await apiRequest({
      method,
      url,
      headers: hostHeaders(hostReferenceId),
      body,
      signal,
    });
  } catch (error) {
    if (isAbortError(error)) {
      throw error;
    }
    return { ok: false, problem: networkProblem() };
  }

  const parsed = await parseJsonBody(response);
  if (!parsed.ok) {
    return parsed;
  }

  if ((response.status === 200 || response.status === 201) && isProperty(parsed.value)) {
    return { ok: true, status: response.status, data: parsed.value };
  }

  return { ok: false, problem: normalizeProblemPayload(parsed.value, response.status) };
}

export const propertyApi = {
  create(
    input: CreatePropertyRequest,
    hostReferenceId: HostReferenceId,
    signal?: AbortSignal,
  ): Promise<PropertyApiResult> {
    return send('POST', catalogPropertiesUrl(), hostReferenceId, input, signal);
  },

  update(
    propertyId: PropertyId,
    changes: UpdatePropertyRequest,
    hostReferenceId: HostReferenceId,
    signal?: AbortSignal,
  ): Promise<PropertyApiResult> {
    return send(
      'PATCH',
      catalogPropertyUrl(propertyId),
      hostReferenceId,
      omitUndefinedFields(changes),
      signal,
    );
  },
};
