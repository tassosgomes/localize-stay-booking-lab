// @vitest-environment node
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { catalogApiBaseUrl } from '../../../config/env.ts';
import { server } from '../../../test/mocks/server.ts';
import '../../../test/setup.ts';
import {
  HOST_REFERENCE_HEADER,
  propertyApi,
  type Property,
} from './propertyApi.ts';

const catalogUrl = 'http://catalog.test';
const hostReferenceId = '421ec5d4-3ba2-4d8e-8958-a51dd0711650';
const propertyId = '8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a';

const createdProperty = {
  id: propertyId,
  name: 'Pousada Dunas do Sol',
  location: 'Cumbuco, Caucaia - CE',
  hostReferenceId,
  status: 'active',
} as const satisfies Property;

const createInput = {
  name: createdProperty.name,
  location: createdProperty.location,
};

function stubBackendUrls(catalog = catalogUrl): void {
  vi.stubEnv('VITE_CATALOG_URL', catalog);
  vi.stubEnv('VITE_BOOKING_URL', 'http://booking.test');
  vi.stubEnv('VITE_PAYMENT_URL', 'http://payment.test');
}

function propertiesUrl(catalog = catalogUrl): string {
  return `${catalogApiBaseUrl(catalog)}/properties`;
}

function propertyUrl(id = propertyId, catalog = catalogUrl): string {
  return `${propertiesUrl(catalog)}/${id}`;
}

describe('propertyApi', () => {
  it('create posts JSON to /v1/properties with X-Host-Reference-Id and returns the 201 property', async () => {
    stubBackendUrls();
    let captured: { url: string; method: string; contentType: string | null; host: string | null; body: unknown } | undefined;

    server.use(
      http.post(propertiesUrl(), async ({ request }) => {
        captured = {
          url: request.url,
          method: request.method,
          contentType: request.headers.get('content-type'),
          host: request.headers.get(HOST_REFERENCE_HEADER),
          body: await request.json(),
        };
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    const result = await propertyApi.create(createInput, hostReferenceId);

    expect(captured?.method).toBe('POST');
    expect(new URL(captured?.url ?? '').pathname).toBe('/v1/properties');
    expect(captured?.host).toBe(hostReferenceId);
    expect(captured?.contentType).toMatch(/application\/json/i);
    expect(captured?.body).toEqual(createInput);
    expect(result).toEqual({ ok: true, status: 201, data: createdProperty });
  });

  it('update patches only the fields that changed', async () => {
    stubBackendUrls();
    let capturedBody: unknown;

    server.use(
      http.patch(propertyUrl(), async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          { ...createdProperty, name: 'Pousada Dunas do Sol Boutique' },
          { status: 200 },
        );
      }),
    );

    const result = await propertyApi.update(
      propertyId,
      { name: 'Pousada Dunas do Sol Boutique', location: undefined },
      hostReferenceId,
    );

    expect(capturedBody).toEqual({ name: 'Pousada Dunas do Sol Boutique' });
    expect(capturedBody).not.toHaveProperty('location');
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.status).toBe(200);
      expect(result.data.name).toBe('Pousada Dunas do Sol Boutique');
    }
  });

  it('does not duplicate /v1 when the Catalog URL already includes it', async () => {
    const catalogWithVersion = 'http://catalog.test/v1';
    stubBackendUrls(catalogWithVersion);
    let pathname = '';

    server.use(
      http.post(propertiesUrl(catalogWithVersion), ({ request }) => {
        pathname = new URL(request.url).pathname;
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    await propertyApi.create(createInput, hostReferenceId);

    expect(pathname).toBe('/v1/properties');
  });

  it('parses 400 ProblemDetails including details and traceId', async () => {
    stubBackendUrls();
    const problem = {
      type: 'https://localize-stay.example/problems/validation-error',
      title: 'Dados inválidos',
      status: 400,
      detail: 'Corrija os campos indicados e tente novamente.',
      instance: '/v1/properties',
      code: 'VALIDATION_ERROR',
      details: [
        { field: 'name', message: 'O nome não pode conter apenas espaços.' },
        { field: 'location', message: 'A localização deve ter no máximo 500 caracteres.' },
      ],
      traceId: '4f75b9ff793eef884ad28e7d65c2832b',
    };

    server.use(
      http.post(propertiesUrl(), () =>
        HttpResponse.json(problem, { status: 400, headers: { 'Content-Type': 'application/problem+json' } }),
      ),
    );

    const result = await propertyApi.create(createInput, hostReferenceId);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.kind).toBe('problem');
      expect(result.problem.code).toBe('VALIDATION_ERROR');
      expect(result.problem.status).toBe(400);
      expect(result.problem.details).toEqual(problem.details);
      expect(result.problem.traceId).toBe(problem.traceId);
    }
  });

  it.each([
    [403, 'HOST_OWNERSHIP_FORBIDDEN'],
    [404, 'PROPERTY_NOT_FOUND'],
    [500, 'INTERNAL_ERROR'],
  ] as const)('update maps %s %s to a safe problem result', async (status, code) => {
    stubBackendUrls();
    const problem = {
      type: `https://localize-stay.example/problems/${code.toLowerCase().replaceAll('_', '-')}`,
      title: 'Erro',
      status,
      detail: 'Falha contratual.',
      instance: `/v1/properties/${propertyId}`,
      code,
      details: [],
      traceId: 'ec311ea9451e7699887f9097b692f049',
    };

    server.use(
      http.patch(propertyUrl(), () =>
        HttpResponse.json(problem, { status, headers: { 'Content-Type': 'application/problem+json' } }),
      ),
    );

    const result = await propertyApi.update(propertyId, { location: 'Praia de Cumbuco, Caucaia - CE' }, hostReferenceId);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.code).toBe(code);
      expect(result.problem.status).toBe(status);
      expect(result.problem.traceId).toBe(problem.traceId);
      expect(result.problem.details).toEqual([]);
    }
  });

  it('treats incomplete ProblemDetails as a safe result with empty details and no traceId', async () => {
    stubBackendUrls();
    server.use(
      http.post(propertiesUrl(), () => HttpResponse.json({ code: 'VALIDATION_ERROR' }, { status: 400 })),
    );

    const result = await propertyApi.create(createInput, hostReferenceId);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.code).toBe('VALIDATION_ERROR');
      expect(result.problem.status).toBe(400);
      expect(result.problem.details).toEqual([]);
      expect(result.problem.traceId).toBeNull();
      expect(result.problem.detail).toBe('O Catalog está indisponível no momento. Tente novamente.');
    }
  });

  it('returns a network fallback when the request fails before an HTTP response', async () => {
    stubBackendUrls();
    server.use(http.post(propertiesUrl(), () => HttpResponse.error()));

    const result = await propertyApi.create(createInput, hostReferenceId);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.kind).toBe('network');
      expect(result.problem.status).toBeNull();
      expect(result.problem.details).toEqual([]);
      expect(result.problem.traceId).toBeNull();
    }
  });

  it('returns a JSON fallback when the error body is not parseable', async () => {
    stubBackendUrls();
    server.use(
      http.patch(propertyUrl(), () =>
        new HttpResponse('{', { status: 500, headers: { 'Content-Type': 'application/json' } }),
      ),
    );

    const result = await propertyApi.update(propertyId, { name: 'Pousada' }, hostReferenceId);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.kind).toBe('invalid-json');
      expect(result.problem.status).toBe(500);
      expect(result.problem.details).toEqual([]);
      expect(result.problem.traceId).toBeNull();
    }
  });

  it('propagates abort without retrying the request', async () => {
    stubBackendUrls();
    let attempts = 0;
    server.use(
      http.post(propertiesUrl(), () => {
        attempts += 1;
        return new Promise(() => {});
      }),
    );

    const controller = new AbortController();
    const pending = propertyApi.create(createInput, hostReferenceId, controller.signal);
    controller.abort();

    await expect(pending).rejects.toMatchObject({ name: 'AbortError' });
    expect(attempts).toBeLessThanOrEqual(1);
  });

  it('keeps generated catalog types in sync with the OpenAPI contract', () => {
    const script = fileURLToPath(new URL('../../../../scripts/check-catalog-drift.mjs', import.meta.url));
    const result = spawnSync(process.execPath, [script], { encoding: 'utf8' });

    expect(result.status, `${result.stdout}\n${result.stderr}`).toBe(0);
  });
});
