import { expect, test } from '@playwright/test';

// Jornada full-stack da task 6.0 (prd-solicitacao-reserva, V-FE-02): browser
// real → frontend real → Booking real (3.0) → stub sancionado de Catalog
// (E2EAvailabilityStubEndpoints, removível quando Catalog F04 chegar).
// Sem MSW/Prism: os 7 cenários isolados já são provados por RTL+MSW em 5.0;
// aqui só sucesso + uma rejeição representativa (PERIODO_INVALIDO).

// Fixture do stub: mesmo UUID do exemplo de api-contract.yaml. Fatos fixos do
// stub: diária "350.00" BRL, capacidade 4, ativa e disponível no período.
const FIXTURE_ACCOMMODATION_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

// Período do exemplo do contrato: 3 noites (10 → 13/out/2026).
const CHECK_IN = '2026-10-10';
const CHECK_OUT = '2026-10-13';

test('Guest solicita reserva válida e vê o resultado congelado', async ({ page }) => {
  await page.goto('/reservations');

  await page.getByLabel('ID da acomodação').fill(FIXTURE_ACCOMMODATION_ID);
  await page.getByLabel('Guest de referência').fill('guest-e2e-playwright');
  await page.getByLabel('Data de check-in').fill(CHECK_IN);
  await page.getByLabel('Data de check-out').fill(CHECK_OUT);
  await page.getByLabel('Número de hóspedes').fill('2');

  await page.getByRole('button', { name: 'Solicitar reserva' }).click();

  // ReservationResultSummary com os valores efetivamente devolvidos pelo
  // Booking real (que congelou o preço vindo do Catalog): ID real, status
  // "solicitada", diária 350.00 BRL e total 1050.00 BRL (3 noites).
  // Timeout estendido: o 201 só sai após o publish best-effort de
  // booking.reservation_requested esgotar as retentativas contra o broker de
  // laboratório (sem consumidor ligado na Fase 0 — techspec §Riscos
  // Conhecidos), ~9s medidos; 60s dá margem sem mascarar regressão real.
  await expect(page.getByRole('heading', { name: 'Reserva solicitada' })).toBeVisible({ timeout: 60_000 });
  // exact:true: "solicitada" é substring do título e "350.00 BRL" é substring
  // do total — sem exatidão o locator casa em 2 elementos (strict violation).
  await expect(page.getByText('solicitada', { exact: true })).toBeVisible();
  await expect(page.getByText('350.00 BRL', { exact: true })).toBeVisible();
  await expect(page.getByText('1050.00 BRL', { exact: true })).toBeVisible();
  // O resumo exibe dois UUIDs (acomodação fixture + reserva criada): o ID da
  // reserva é o primeiro <dd> da seção de resultado — um UUID real distinto
  // do fixture enviado.
  const reservationId = await page
    .locator('section[aria-labelledby="reservation-result-title"] dd')
    .first()
    .innerText();
  expect(reservationId.trim()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
  expect(reservationId.trim()).not.toBe(FIXTURE_ACCOMMODATION_ID);
});

test('Período inválido é rejeitado sem criar Reservation', async ({ page }) => {
  // RN-02 é antecipada pela validação local do frontend (techspec §Validação
  // de Formulários) com a mesma mensagem do mapeamento do 422 backend; o
  // backend continua sendo a autoridade (provado em 3.0). Nos dois caminhos a
  // garantia de RF-01 vale: nenhuma chamada a Catalog ocorre e nenhuma
  // Reservation é criada.
  const posts: string[] = [];
  await page.route('**/v1/reservations', async (route) => {
    posts.push(route.request().url());
    await route.continue();
  });

  await page.goto('/reservations');

  await page.getByLabel('ID da acomodação').fill(FIXTURE_ACCOMMODATION_ID);
  await page.getByLabel('Guest de referência').fill('guest-e2e-playwright');
  await page.getByLabel('Data de check-in').fill(CHECK_OUT);
  await page.getByLabel('Data de check-out').fill(CHECK_IN);
  await page.getByLabel('Número de hóspedes').fill('2');

  await page.getByRole('button', { name: 'Solicitar reserva' }).click();

  // Erro associado a check-in/check-out; sem navegação para o resumo.
  await expect(page.getByText('O check-out deve ser posterior ao check-in.').first()).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Reserva solicitada' })).toBeHidden();
  expect(posts).toHaveLength(0);
});
