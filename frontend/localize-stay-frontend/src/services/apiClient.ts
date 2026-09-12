// Cliente HTTP fino compartilhado (fundação + Booking).
// Convergência de rebase: aceita AS DUAS formas de chamada SEM alterar nenhum
// consumidor — união discriminada com branch em runtime.
//
// - Forma `url` (Property/Catalog, origin/main): `apiRequest({ method, url,
//   headers, body, signal })` com semântica integral de abort da irmã
//   (`throwIfAborted` + fallback `INCOMPATIBLE_ABORT_SIGNAL`).
// - Forma `baseUrl+path` (Booking, reservationApi.ts): `apiRequest({
//   baseUrl, path, method, body, signal })` com `buildApiUrl(baseUrl, path)`
//   e a semântica atual testada (Accept: application/json, sem fallback).
export type ApiMethod = 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';

// Alias histórico da fatia Booking (task 4.0); idêntico a ApiMethod.
export type ApiRequestMethod = ApiMethod;

interface ApiRequestByUrl {
  method: ApiMethod;
  url: string;
  headers?: Record<string, string>;
  body?: unknown;
  signal?: AbortSignal;
}

interface ApiRequestByBaseUrl {
  method: ApiMethod;
  baseUrl: string;
  path: string;
  headers?: Record<string, string>;
  body?: unknown;
  signal?: AbortSignal;
}

export type ApiRequestOptions = ApiRequestByUrl | ApiRequestByBaseUrl;

const INCOMPATIBLE_ABORT_SIGNAL =
  /Expected signal .* to be an instance of AbortSignal/;

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) {
    throw new DOMException('The operation was aborted.', 'AbortError');
  }
}

export function buildApiUrl(baseUrl: string, path: string): string {
  const normalizedBase = baseUrl.replace(/\/+$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
}

function isUrlForm(options: ApiRequestOptions): options is ApiRequestByUrl {
  return 'url' in options;
}

export async function apiRequest(options: ApiRequestOptions): Promise<Response> {
  if (isUrlForm(options)) {
    const { method, url, headers, body, signal } = options;
    throwIfAborted(signal);

    const requestHeaders = new Headers(headers);
    const init: RequestInit = { method, headers: requestHeaders };

    if (body !== undefined) {
      requestHeaders.set('Content-Type', 'application/json');
      init.body = JSON.stringify(body);
    }

    try {
      return await fetch(url, signal === undefined ? init : { ...init, signal });
    } catch (error) {
      throwIfAborted(signal);
      if (
        signal !== undefined &&
        error instanceof TypeError &&
        INCOMPATIBLE_ABORT_SIGNAL.test(error.message)
      ) {
        return fetch(url, init);
      }
      throw error;
    }
  }

  const { method, baseUrl, path, headers, body, signal } = options;
  const requestHeaders: Record<string, string> = {
    Accept: 'application/json',
    ...headers,
  };
  if (body !== undefined) {
    requestHeaders['Content-Type'] = 'application/json';
  }
  return fetch(buildApiUrl(baseUrl, path), {
    method,
    headers: requestHeaders,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  });
}
