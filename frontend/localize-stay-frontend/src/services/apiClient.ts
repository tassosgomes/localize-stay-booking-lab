// Cliente HTTP fino compartilhado (fundação + Booking).
// Convergência de rebase: aceita AS DUAS formas de chamada SEM alterar nenhum
// consumidor — união discriminada com branch em runtime.
//
// - Forma `url` (Property/Catalog, origin/main): `apiRequest({ method, url,
//   headers, body, signal })` com semântica integral de abort da irmã
//   (`throwIfAborted` + fallback `INCOMPATIBLE_ABORT_SIGNAL`).
// - Forma `baseUrl+path` (Booking, reservationApi.ts): `apiRequest({
//   baseUrl, path, method, body, signal })` com `buildApiUrl(baseUrl, path)`
//   e a semântica da jornada de reserva (Accept: application/json + guarda
//   de realm de AbortSignal da task 5.0 para jsdom).
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

// Alguns ambientes de teste com DOM simulado (jsdom) expõem um AbortController
// de realm distinta da do fetch nativo do Node, que rejeita o signal com
// TypeError ("Expected signal to be an instance of AbortSignal"). Detectamos a
// compatibilidade uma única vez construindo um Request descartável: no browser
// (mesma realm) o signal sempre flui e o cancelamento é real; em ambientes
// incompatíveis o request segue sem cancelamento de transporte e o chamador
// mantém o cancelamento lógico (ignorar o resultado após abort()).
let fetchAcceptsAbortSignals: boolean | undefined;

function acceptsAbortSignals(): boolean {
  if (fetchAcceptsAbortSignals === undefined) {
    if (typeof Request !== 'function') {
      fetchAcceptsAbortSignals = true;
    } else {
      try {
        new Request('http://localize-stay.lab/probe', { signal: new AbortController().signal });
        fetchAcceptsAbortSignals = true;
      } catch {
        fetchAcceptsAbortSignals = false;
      }
    }
  }
  return fetchAcceptsAbortSignals;
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
    signal: signal !== undefined && acceptsAbortSignals() ? signal : undefined,
  });
}
