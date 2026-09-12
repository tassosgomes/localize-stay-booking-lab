// @vitest-environment jsdom
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from '../../App.tsx';
import { catalogApiBaseUrl, getEnv } from '../../config/env.ts';
import { HOST_REFERENCE_HEADER, type Property } from './api/propertyApi.ts';
import { server } from '../../test/mocks/server.ts';
import '../../test/setup.ts';

const catalogUrl = 'http://catalog.test';
const hostReferenceId = '421ec5d4-3ba2-4d8e-8958-a51dd0711650';
const propertyId = '8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a';
const duplicatePropertyId = 'c3f1a0b8-2d64-4e91-9b77-0f4d8a6c1e20';
const propertyName = 'Pousada Dunas do Sol';
const propertyLocation = 'Cumbuco, Caucaia - CE';

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

function stubBackendUrls(): void {
  vi.stubEnv('VITE_CATALOG_URL', catalogUrl);
  vi.stubEnv('VITE_BOOKING_URL', 'http://booking.test');
  vi.stubEnv('VITE_PAYMENT_URL', 'http://payment.test');
}

function renderPropertiesPage() {
  window.history.pushState({}, '', '/properties');
  return render(<App />);
}

async function fillValidForm(
  user: ReturnType<typeof userEvent.setup>,
): Promise<void> {
  await user.type(screen.getByLabelText('Identificador fictício do Host'), hostReferenceId);
  await user.type(screen.getByLabelText('Nome da hospedagem'), propertyName);
  await user.type(screen.getByLabelText('Localização'), propertyLocation);
}

describe('PropertyCreate', () => {
  beforeEach(() => {
    stubBackendUrls();
    window.history.pushState({}, '', '/');
  });

  afterEach(() => {
    window.history.pushState({}, '', '/');
  });

  it('keeps the health screen on / and opens the create form at /properties', async () => {
    window.history.pushState({}, '', '/');
    const view = render(<App />);

    expect(await screen.findByRole('heading', { name: 'Service status' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Cadastro de hospedagem' })).not.toBeInTheDocument();

    view.unmount();
    renderPropertiesPage();

    expect(screen.getByRole('heading', { name: 'Cadastro de hospedagem' })).toBeInTheDocument();
    expect(screen.getByLabelText('Identificador fictício do Host')).toBeInTheDocument();
    expect(screen.getByLabelText('Nome da hospedagem')).toBeInTheDocument();
    expect(screen.getByLabelText('Localização')).toBeInTheDocument();
  });

  it('shows confirmation, copyable id, host, data and active after a 201 create', async () => {
    const user = userEvent.setup();
    let capturedHost: string | null = null;
    let capturedBody: unknown;

    server.use(
      http.post(propertiesUrl(), async ({ request }) => {
        capturedHost = request.headers.get(HOST_REFERENCE_HEADER);
        capturedBody = await request.json();
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    renderPropertiesPage();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));

    expect(await screen.findByRole('heading', { name: 'Hospedagem cadastrada' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Cadastro confirmado');
    const identifier = screen.getByLabelText('Identificador');
    expect(identifier).toHaveValue(propertyId);
    expect(identifier).toHaveAttribute('readonly');
    expect(screen.getByText(hostReferenceId)).toBeInTheDocument();
    expect(screen.getByText(propertyName)).toBeInTheDocument();
    expect(screen.getByText(propertyLocation)).toBeInTheDocument();
    expect(screen.getByText('active')).toBeInTheDocument();
    expect(capturedHost).toBe(hostReferenceId);
    expect(capturedBody).toEqual({ name: propertyName, location: propertyLocation });

    await user.click(screen.getByRole('button', { name: 'Copiar identificador' }));
    expect(identifier).toHaveFocus();
    expect(
      await screen.findByText(/Identificador copiado|Identificador selecionado/),
    ).toBeInTheDocument();
  });

  it('accepts a duplicate name and location as another 201 property', async () => {
    const user = userEvent.setup();
    const duplicateProperty = { ...createdProperty, id: duplicatePropertyId };
    let attempts = 0;

    server.use(
      http.post(propertiesUrl(), () => {
        attempts += 1;
        const property = attempts === 1 ? createdProperty : duplicateProperty;
        return HttpResponse.json(property, { status: 201 });
      }),
    );

    renderPropertiesPage();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));
    expect(await screen.findByLabelText('Identificador')).toHaveValue(propertyId);

    await user.click(screen.getByRole('button', { name: 'Cadastrar outra hospedagem' }));
    await user.click(screen.getByRole('button', { name: 'Confirmar nova criação' }));
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));

    expect(await screen.findByLabelText('Identificador')).toHaveValue(duplicatePropertyId);
    expect(screen.getByText(propertyName)).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it('does not call the API for local validation errors and moves focus to the error summary', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    server.use(
      http.post(propertiesUrl(), () => {
        attempts += 1;
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    renderPropertiesPage();
    await user.type(screen.getByLabelText('Identificador fictício do Host'), 'host-invalido');
    await user.type(screen.getByLabelText('Nome da hospedagem'), '   ');
    await user.type(screen.getByLabelText('Localização'), propertyLocation);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));

    const summary = await screen.findByRole('alert', { name: 'Há erros no formulário' });
    expect(summary).toHaveFocus();
    expect(attempts).toBe(0);
    expect(screen.getByLabelText('Identificador fictício do Host')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Identificador fictício do Host')).toHaveAccessibleDescription(
      /UUID de laboratório[\s\S]*UUID válido/i,
    );
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveAccessibleDescription(
      'O nome não pode conter apenas espaços.',
    );
    expect(screen.getByLabelText('Identificador fictício do Host')).toHaveValue('host-invalido');
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveValue('   ');
  });

  it('maps a 400 problem onto fields, preserves values and focuses the error summary', async () => {
    const user = userEvent.setup();
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
        HttpResponse.json(problem, {
          status: 400,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    );

    renderPropertiesPage();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));

    const summary = await screen.findByRole('alert', { name: 'Há erros no formulário' });
    expect(summary).toHaveFocus();
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveAccessibleDescription(
      'O nome não pode conter apenas espaços.',
    );
    expect(screen.getByLabelText('Localização')).toHaveAccessibleDescription(
      'A localização deve ter no máximo 500 caracteres.',
    );
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveValue(propertyName);
    expect(screen.getByLabelText('Localização')).toHaveValue(propertyLocation);
    expect(screen.getByLabelText('Identificador fictício do Host')).toHaveValue(hostReferenceId);
  });

  it('shows a 500 message with traceId, keeps values and allows a manual retry', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    const traceId = 'ec311ea9451e7699887f9097b692f049';

    server.use(
      http.post(propertiesUrl(), () => {
        attempts += 1;
        if (attempts === 1) {
          return HttpResponse.json(
            {
              type: 'https://localize-stay.example/problems/internal-error',
              title: 'Erro',
              status: 500,
              detail: 'Ocorreu um erro inesperado. Tente novamente mais tarde.',
              instance: '/v1/properties',
              code: 'INTERNAL_ERROR',
              details: [],
              traceId,
            },
            { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
          );
        }
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    renderPropertiesPage();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Ocorreu um erro inesperado. Tente novamente mais tarde.');
    expect(alert).toHaveTextContent(traceId);
    expect(screen.getByLabelText('Nome da hospedagem')).toHaveValue(propertyName);

    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));
    expect(await screen.findByRole('heading', { name: 'Hospedagem cadastrada' })).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it('shows a network failure message, keeps values and allows a manual retry', async () => {
    const user = userEvent.setup();
    let attempts = 0;

    server.use(
      http.post(propertiesUrl(), () => {
        attempts += 1;
        if (attempts === 1) {
          return HttpResponse.error();
        }
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    renderPropertiesPage();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('O Catalog está indisponível no momento. Tente novamente.');
    expect(screen.getByLabelText('Localização')).toHaveValue(propertyLocation);

    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));
    expect(await screen.findByRole('heading', { name: 'Hospedagem cadastrada' })).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it('disables submit while creating so a second click does not send another request', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    let release: (() => void) | undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });

    server.use(
      http.post(propertiesUrl(), async () => {
        attempts += 1;
        await gate;
        return HttpResponse.json(createdProperty, { status: 201 });
      }),
    );

    renderPropertiesPage();
    await fillValidForm(user);
    const submit = screen.getByRole('button', { name: 'Cadastrar hospedagem' });
    await user.click(submit);

    expect(submit).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Cadastrando hospedagem');
    await user.click(submit);
    expect(attempts).toBe(1);

    release?.();
    expect(await screen.findByRole('heading', { name: 'Hospedagem cadastrada' })).toBeInTheDocument();
    expect(attempts).toBe(1);
  });

  it('aborts an in-flight create on unmount and does not retry automatically', async () => {
    const user = userEvent.setup();
    let attempts = 0;

    server.use(
      http.post(propertiesUrl(), () => {
        attempts += 1;
        return new Promise(() => {});
      }),
    );

    const view = renderPropertiesPage();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: 'Cadastrar hospedagem' }));
    await waitFor(() => expect(attempts).toBe(1));

    view.unmount();
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(attempts).toBe(1);
  });
});
