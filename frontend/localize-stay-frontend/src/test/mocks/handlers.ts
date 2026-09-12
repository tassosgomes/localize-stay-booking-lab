import { http, HttpResponse } from 'msw';

export function problemResponse(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

export const handlers = [
  http.get(/\/health\/ready$/, () => HttpResponse.text('Healthy')),
];
