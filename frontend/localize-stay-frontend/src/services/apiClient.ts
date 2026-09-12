export type ApiMethod = 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';

export interface ApiRequestOptions {
  method: ApiMethod;
  url: string;
  headers?: Record<string, string>;
  body?: unknown;
  signal?: AbortSignal;
}

export async function apiRequest({
  method,
  url,
  headers,
  body,
  signal,
}: ApiRequestOptions): Promise<Response> {
  const requestHeaders = new Headers(headers);
  const init: RequestInit = { method, headers: requestHeaders, signal };

  if (body !== undefined) {
    requestHeaders.set('Content-Type', 'application/json');
    init.body = JSON.stringify(body);
  }

  return fetch(url, init);
}
