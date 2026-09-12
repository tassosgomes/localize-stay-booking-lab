import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get(/\/health\/ready$/, () => HttpResponse.text('Healthy')),
];
