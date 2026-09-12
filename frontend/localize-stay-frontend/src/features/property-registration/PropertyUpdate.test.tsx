// @vitest-environment jsdom
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from '../../App.tsx';
import { catalogApiBaseUrl, getEnv } from '../../config/env.ts';
import { HOST_REFERENCE_HEADER, type Property } from './api/propertyApi.ts';
import { server } from '../../test/mocks/server.ts';
import { problemResponse } from '../../test/mocks/handlers.ts';
import '../../test/setup.ts';

const catalogUrl = 'http://catalog.test';
const hostReferenceId = '421ec5d4-3ba2-4d8e-8958-a51dd0711650';
const otherHostReferenceId = '9f3c2a10-6b44-4d91-8e27-5c1b0a7d4e88';
const propertyId = '8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a';
const propertyName = 'Pousada Dunas do Sol';
const propertyLocation = 'Cumbuco, Caucaia - CE';
const updatedLocation = 'Praia de Cumbuco, Caucaia - CE';
const updatedName = 'Pousada Dunas do Sol Boutique';

const createdProperty = {
  id: propertyId,
  name: propertyName,
  location: propertyLocation,
  hostReferenceId,
  status: 'active',
} as const satisfies Property;

function propertiesUrl(): string {
  return `${catalogApiBaseUrl(getEnv().catalogUrl)}/properties`;
}

function propertyUrl(): string {
  return `${propertiesUrl()}/${propertyId}`;
}

function stubBackendUrls(): void {
  vi.stubEnv('VITE_CATALOG_URL', catalogUrl);
  vi.stubEnv('VITE_BOOKING_URL', 'http://booking.test');
  vi.stubEnv('VITE_PAYMENT_URL', 'http://payment.test');
}

function renderPropertiesPage() {
  window.history.pushState({}, '', '/properties');
  return render(<App />);
}

async function fillCreateForm(user: ReturnType<typeof userEvent.setup>): Promise<void> {
  await user.type(screen.getByLabelText('Identificador fictício do Host'), hostReferenceId);
  await user.type(screen.getByLabelText('Nome da hospedagem'), propertyName);
  await user.type(screen.getByLabelText('Localização'), propertyLocation);
}

async function createAndStartEditing(
  user: ReturnType<typeof userEvent.setup>,
): Promise<void> {
  await fillCreateForm(user);
  await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));
  expect(await screen.findByRole('heading', { name: 'Hospedagem cadastrada' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Editar hospedagem' }));
  expect(await screen.findByRole('heading', { name: 'Editar hospedagem' })).toBeInTheDocument();
}

describe('PropertyUpdate', () => {
  beforeEach(() => {
    stubBackendUrls();
    window.history.pushState({}, '', '/');
    server.use(
      http.post(propertiesUrl(), () => HttpResponse.json(createdProperty, { status: 201 })),
    );
  });

  afterEach(() => {
    window.history.pushState({}, '', '/');
  });

  it('prefills the edit form from the created response and keeps identity fields read-only', async () => {
    const user = userEvent.setup();
    renderPropertiesPage();
    await createAndStartEditing(user);

    expect(screen.getByRole('heading', { name: 'Editar hospedagem' })).toHaveFocus();
    expect(screen.getByLabelText('Identificador fictício do Host')).toHaveValue(hostReferenceId);
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveValue(propertyName);
    expect(screen.getByLabelText('Localização')).toHaveValue(propertyLocation);
    expect(screen.getByLabelText('Identificador', { exact: true })).toHaveValue(propertyId);
    expect(screen.getByLabelText('Identificador', { exact: true })).toHaveAttribute('readonly');
    expect(screen.getByText(hostReferenceId)).toBeInTheDocument();
    expect(screen.getByText('active')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Status' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Host responsável')).not.toBeInTheDocument();
  });

  it('does not send a request when name and location did not change', async () => {
    const user = userEvent.setup();
    let patchAttempts = 0;
    server.use(
      http.patch(propertyUrl(), () => {
        patchAttempts += 1;
        return HttpResponse.json(createdProperty, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Altere o nome ou a localização antes de confirmar.');
    expect(patchAttempts).toBe(0);
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveValue(propertyName);
    expect(screen.getByLabelText('Localização')).toHaveValue(propertyLocation);
  });

  it('patches only the changed location and preserves id, host and status on 200', async () => {
    const user = userEvent.setup();
    let captured: { method: string; host: string | null; body: unknown } | undefined;
    const updatedProperty = { ...createdProperty, location: updatedLocation };

    server.use(
      http.patch(propertyUrl(), async ({ request }) => {
        captured = {
          method: request.method,
          host: request.headers.get(HOST_REFERENCE_HEADER),
          body: await request.json(),
        };
        return HttpResponse.json(updatedProperty, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    expect(await screen.findByRole('status', { name: 'Alteração confirmada' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Hospedagem cadastrada' })).toBeInTheDocument();
    expect(screen.getByLabelText('Identificador', { exact: true })).toHaveValue(propertyId);
    expect(screen.getByText(hostReferenceId)).toBeInTheDocument();
    expect(screen.getByText('active')).toBeInTheDocument();
    expect(screen.getByText(updatedLocation)).toBeInTheDocument();
    expect(screen.queryByText(propertyLocation)).not.toBeInTheDocument();
    expect(captured?.method).toBe('PATCH');
    expect(captured?.host).toBe(hostReferenceId);
    expect(captured?.body).toEqual({ location: updatedLocation });
    expect(captured?.body).not.toHaveProperty('name');
    expect(captured?.body).not.toHaveProperty('id');
    expect(captured?.body).not.toHaveProperty('status');
    expect(captured?.body).not.toHaveProperty('hostReferenceId');
  });

  it('sends name and location together when both changed', async () => {
    const user = userEvent.setup();
    let capturedBody: unknown;
    server.use(
      http.patch(propertyUrl(), async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          { ...createdProperty, name: updatedName, location: updatedLocation },
          { status: 200 },
        );
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Nome da hospedagem'));
    await user.type(screen.getByLabelText('Nome da hospedagem'), updatedName);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    expect(await screen.findByText(updatedName)).toBeInTheDocument();
    expect(capturedBody).toEqual({ name: updatedName, location: updatedLocation });
  });

  it('rejects a whitespace-only location locally, keeps the draft and focuses the error summary', async () => {
    const user = userEvent.setup();
    let patchAttempts = 0;
    server.use(
      http.patch(propertyUrl(), () => {
        patchAttempts += 1;
        return HttpResponse.json(createdProperty, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), '   ');
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const summary = await screen.findByRole('alert', { name: 'Há erros no formulário' });
    expect(summary).toHaveFocus();
    expect(patchAttempts).toBe(0);
    expect(screen.getByLabelText('Localização')).toHaveValue('   ');
    expect(screen.getByLabelText('Localização')).toHaveAccessibleDescription(
      'A localização não pode conter apenas espaços.',
    );
  });

  it('maps a 400 problem onto fields, preserves the draft and focuses the error summary', async () => {
    const user = userEvent.setup();
    const problem = {
      type: 'https://localize-stay.example/problems/validation-error',
      title: 'Dados inválidos',
      status: 400,
      detail: 'Corrija os campos indicados e tente novamente.',
      instance: `/v1/properties/${propertyId}`,
      code: 'VALIDATION_ERROR',
      details: [{ field: 'location', message: 'A localização deve ter no máximo 500 caracteres.' }],
      traceId: '4f75b9ff793eef884ad28e7d65c2832b',
    };

    server.use(http.patch(propertyUrl(), () => problemResponse(400, problem)));

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const summary = await screen.findByRole('alert', { name: 'Há erros no formulário' });
    expect(summary).toHaveFocus();
    expect(screen.getByLabelText('Localização')).toHaveValue(updatedLocation);
    expect(screen.getByLabelText('Localização')).toHaveAccessibleDescription(
      'A localização deve ter no máximo 500 caracteres.',
    );
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveValue(propertyName);
  });

  it('shows a 403 ownership message with traceId and keeps the draft for retry', async () => {
    const user = userEvent.setup();
    const traceId = '1acfc080c9a001b9ea65431bdb96c41d';
    let attempts = 0;
    server.use(
      http.patch(propertyUrl(), () => {
        attempts += 1;
        if (attempts === 1) {
          return problemResponse(403, {
            type: 'https://localize-stay.example/problems/host-ownership-forbidden',
            title: 'Operação não permitida',
            status: 403,
            detail: 'Somente o Host responsável pode editar esta Property.',
            instance: `/v1/properties/${propertyId}`,
            code: 'HOST_OWNERSHIP_FORBIDDEN',
            details: [],
            traceId,
          });
        }
        return HttpResponse.json({ ...createdProperty, location: updatedLocation }, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Identificador fictício do Host'));
    await user.type(screen.getByLabelText('Identificador fictício do Host'), otherHostReferenceId);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Operação não permitida');
    expect(alert).toHaveTextContent('Somente o Host responsável pode editar esta Property.');
    expect(alert).toHaveTextContent(traceId);
    expect(screen.getByLabelText('Localização')).toHaveValue(updatedLocation);
    expect(screen.getByLabelText('Identificador fictício do Host')).toHaveValue(otherHostReferenceId);

    await user.clear(screen.getByLabelText('Identificador fictício do Host'));
    await user.type(screen.getByLabelText('Identificador fictício do Host'), hostReferenceId);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    expect(await screen.findByRole('status', { name: 'Alteração confirmada' })).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it('shows a 404 not-found message with traceId and keeps the draft', async () => {
    const user = userEvent.setup();
    const traceId = 'b7e2c91f04aa6d33c8f10e55a9d4b012';
    server.use(
      http.patch(propertyUrl(), () =>
        problemResponse(404, {
          type: 'https://localize-stay.example/problems/property-not-found',
          title: 'Property não encontrada',
          status: 404,
          detail: 'Não foi encontrada uma Property com o identificador informado.',
          instance: `/v1/properties/${propertyId}`,
          code: 'PROPERTY_NOT_FOUND',
          details: [],
          traceId,
        }),
      ),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Hospedagem não encontrada');
    expect(alert).toHaveTextContent('Não foi encontrada uma Property com o identificador informado.');
    expect(alert).toHaveTextContent(traceId);
    expect(screen.getByLabelText('Localização')).toHaveValue(updatedLocation);
    expect(screen.queryByRole('status', { name: /Alteração confirmada/i })).not.toBeInTheDocument();
  });

  it('shows a 500 message with traceId, keeps the draft and allows a manual retry', async () => {
    const user = userEvent.setup();
    const traceId = 'ec311ea9451e7699887f9097b692f049';
    let attempts = 0;
    server.use(
      http.patch(propertyUrl(), () => {
        attempts += 1;
        if (attempts === 1) {
          return problemResponse(500, {
            type: 'https://localize-stay.example/problems/internal-error',
            title: 'Erro interno',
            status: 500,
            detail: 'Ocorreu um erro inesperado. Tente novamente mais tarde.',
            instance: `/v1/properties/${propertyId}`,
            code: 'INTERNAL_ERROR',
            details: [],
            traceId,
          });
        }
        return HttpResponse.json({ ...createdProperty, location: updatedLocation }, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Não foi possível editar a hospedagem');
    expect(alert).toHaveTextContent('Ocorreu um erro inesperado. Tente novamente mais tarde.');
    expect(alert).toHaveTextContent(traceId);
    expect(screen.getByLabelText('Localização')).toHaveValue(updatedLocation);

    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    expect(await screen.findByRole('status', { name: 'Alteração confirmada' })).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it('shows a network failure message, keeps the draft and allows a manual retry', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    server.use(
      http.patch(propertyUrl(), () => {
        attempts += 1;
        if (attempts === 1) {
          return HttpResponse.error();
        }
        return HttpResponse.json({ ...createdProperty, location: updatedLocation }, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('O Catalog está indisponível no momento. Tente novamente.');
    expect(screen.getByLabelText('Localização')).toHaveValue(updatedLocation);

    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    expect(await screen.findByRole('status', { name: 'Alteração confirmada' })).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it('disables save while updating so a second click does not send another request', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    let release: (() => void) | undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });

    server.use(
      http.patch(propertyUrl(), async () => {
        attempts += 1;
        await gate;
        return HttpResponse.json({ ...createdProperty, location: updatedLocation }, { status: 200 });
      }),
    );

    renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    const save = screen.getByRole('button', { name: 'Salvar alterações' });
    await user.click(save);

    expect(save).toBeDisabled();
    expect(screen.getByRole('status', { name: 'Atualizando hospedagem' })).toBeInTheDocument();
    await user.click(save);
    expect(attempts).toBe(1);

    release?.();
    expect(await screen.findByRole('status', { name: 'Alteração confirmada' })).toBeInTheDocument();
    expect(attempts).toBe(1);
  });

  it('aborts an in-flight update on unmount and does not retry automatically', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    server.use(
      http.patch(propertyUrl(), () => {
        attempts += 1;
        return new Promise(() => {});
      }),
    );

    const view = renderPropertiesPage();
    await createAndStartEditing(user);
    await user.clear(screen.getByLabelText('Localização'));
    await user.type(screen.getByLabelText('Localização'), updatedLocation);
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }));
    await waitFor(() => expect(attempts).toBe(1));

    view.unmount();
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(attempts).toBe(1);
  });
});
