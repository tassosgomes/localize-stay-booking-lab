import { expect, test, type Page } from '@playwright/test';

// Jornada full-stack da task 5.0 (prd-consulta-reserva, V-02): browser real
// → frontend real → Booking real (:5102). Sem MSW/Prism: os 5 cenários
// isolados (200×3 + 400 + 404) mais 500/rede já são provados por RTL+MSW na
// task 4.0; aqui só os 2 cenários alcançáveis por uma jornada real —
// encontrada (solicitada/pendente, criada via jornada real de F01) + não
// encontrada (UUID válido inexistente, tom neutro). Estados
// confirmada/cancelada ficam em RTL+MSW (frontend-techspec.md §Mocks e
// Ambiente de Desenvolvimento) — dependem de seed via SQL direto, não de
// uma jornada de usuário reproduzível pelo browser.

// Fixture do stub sancionado de Catalog (mesmo UUID do exemplo de
// api-contract.yaml de F01): diária "350.00" BRL, capacidade 4, ativa e
// disponível no período.
const FIXTURE_ACCOMMODATION_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

// Período do exemplo do contrato: 3 noites (10 → 13/out/2026).
const CHECK_IN = '2026-10-10';
const CHECK_OUT = '2026-10-13';
const GUEST_REFERENCE = 'guest-e2e-lookup';

const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Cria uma Reservation pela jornada real de F01 (/reservations) e devolve o
// identificador real exibido no ReservationResultSummary. Mesmo padrão de
// e2e/reservation-request.spec.ts (getByLabel/getByRole, timeout estendido
// para o 201 — o publish best-effort do broker leva ~9s sem consumidor na
// Fase 0).
async function createReservationViaRealJourney(page: Page): Promise<string> {
  await page.goto('/reservations');

  await page.getByLabel('ID da acomodação').fill(FIXTURE_ACCOMMODATION_ID);
  await page.getByLabel('Guest de referência').fill(GUEST_REFERENCE);
  await page.getByLabel('Data de check-in').fill(CHECK_IN);
  await page.getByLabel('Data de check-out').fill(CHECK_OUT);
  await page.getByLabel('Número de hóspedes').fill('2');

  await page.getByRole('button', { name: 'Solicitar reserva' }).click();

  await expect(page.getByRole('heading', { name: 'Reserva solicitada' })).toBeVisible({ timeout: 60_000 });

  const reservationId = (
    await page.locator('section[aria-labelledby="reservation-result-title"] dd').first().innerText()
  ).trim();
  expect(reservationId).toMatch(UUID_PATTERN);
  expect(reservationId).not.toBe(FIXTURE_ACCOMMODATION_ID);
  return reservationId;
}

test('Reservation recém-criada é encontrada com dados congelados e saga pendente', async ({ page }) => {
  const reservationId = await createReservationViaRealJourney(page);

  await page.goto('/reservations/consultar');
  await page.getByLabel('Identificador da reserva').fill(reservationId);
  await page.getByRole('button', { name: 'Buscar reserva' }).click();

  // 200 → ReservationDetailView com foco no título do resultado.
  await expect(page.getByRole('heading', { name: 'Reserva encontrada' })).toBeVisible();

  // Identificador consultado, estado do ciclo de vida e situação da saga.
  await expect(page.getByText(reservationId, { exact: true }).first()).toBeVisible();
  await expect(page.getByText('solicitada', { exact: true })).toBeVisible();
  await expect(page.getByText('pendente', { exact: true })).toBeVisible();

  // Dados congelados de F01 (mesmos fatos do stub de Catalog).
  await expect(page.getByText(FIXTURE_ACCOMMODATION_ID, { exact: true })).toBeVisible();
  await expect(page.getByText(GUEST_REFERENCE, { exact: true })).toBeVisible();
  await expect(page.getByText(`${CHECK_IN} a ${CHECK_OUT}`, { exact: true })).toBeVisible();
  await expect(page.getByText('350.00 BRL', { exact: true })).toBeVisible();
  await expect(page.getByText('1050.00 BRL', { exact: true })).toBeVisible();

  // correlationId visível: o dd adjacente ao termo "Identificador de
  // correlação" contém um UUID bem formado. (Na Fase 0 o domínio correlaciona
  // a saga ao próprio id da Reservation — Reservation.cs cria a saga com
  // correlationId = id — então não se exige distinção entre os dois valores,
  // só presença e formato.)
  const correlationId = (
    await page
      .locator(
        'section[aria-labelledby="reservation-detail-title"] dt:has-text("Identificador de correlação") + dd',
      )
      .innerText()
  ).trim();
  expect(correlationId).toMatch(UUID_PATTERN);

  // Reserva solicitada/pendente nunca expõe motivo de cancelamento (o
  // detalhe nem renderiza o dt/dd condicional nesses estados).
  await expect(page.getByText('Motivo do cancelamento')).toHaveCount(0);
});

test('Identificador válido inexistente mostra "não encontrada" em tom neutro', async ({ page }) => {
  // UUID v4 bem formado gerado na hora: passa na validação local (formato)
  // e não corresponde a nenhuma Reservation criada.
  const missingId = crypto.randomUUID();

  await page.goto('/reservations/consultar');
  await page.getByLabel('Identificador da reserva').fill(missingId);
  await page.getByRole('button', { name: 'Buscar reserva' }).click();

  // 404 RESERVATION_NOT_FOUND → OperationFeedback tom neutral
  // (role="status", nunca role="alert" — "não encontrada" é resposta
  // esperada, não erro técnico).
  const feedback = page.getByRole('status');
  await expect(feedback).toHaveText(
    'Nenhuma reserva encontrada com esse identificador. Confira o identificador recebido.',
  );
  await expect(feedback).toHaveAttribute('data-tone', 'neutral');
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Reserva encontrada' })).toHaveCount(0);
});
