export type ApiMethod = 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';

export interface ApiRequestOptions {
  method: ApiMethod;
  url: string;
  headers?: Record<string, string>;
  body?: unknown;
  signal?: AbortSignal;
}

const INCOMPATIBLE_ABORT_SIGNAL =
  /Expected signal .* to be an instance of AbortSignal/;

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) {
    throw new DOMException('The operation was aborted.', 'AbortError');
  }
}

export async function apiRequest({
  method,
  url,
  headers,
  body,
  signal,
}: ApiRequestOptions): Promise<Response> {
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
