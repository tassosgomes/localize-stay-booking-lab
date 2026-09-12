import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

async function createProperty(
  page: import('@playwright/test').Page,
  host: string,
  name: string,
  location: string,
): Promise<string> {
  await page.goto('/properties');
  await page.getByRole('heading', { name: 'Cadastro de hospedagem' }).waitFor();
  await page.getByLabel('Identificador fictício do Host').fill(host);
  await page.getByLabel('Nome da hospedagem').fill(name);
  await page.getByLabel('Localização').fill(location);
  await page.getByRole('button', { name: 'Cadastrar hospedagem' }).click();
  await expect(page.getByRole('heading', { name: 'Hospedagem cadastrada' })).toBeVisible();
  const identifier = page.getByLabel('Identificador', { exact: true });
  await expect(identifier).toHaveValue(/^[0-9a-f-]{36}$/i);
  return identifier.inputValue();
}

test.describe('PropertyUpdate', () => {
  test('creates a property then edits only the location while preserving id, host and status', async ({
    page,
  }) => {
    const host = randomUUID();
    const name = `Pousada E2E ${host.slice(0, 8)}`;
    const location = `Cumbuco inicial ${host.slice(0, 8)}`;
    const updatedLocation = `Praia atualizada ${host.slice(0, 8)}`;

    const propertyId = await createProperty(page, host, name, location);

    await page.getByRole('button', { name: 'Editar hospedagem' }).click();
    await expect(page.getByRole('heading', { name: 'Editar hospedagem' })).toBeVisible();
    await page.getByLabel('Localização').fill(updatedLocation);
    await page.getByRole('button', { name: 'Salvar alterações' }).click();

    await expect(page.getByRole('status', { name: 'Alteração confirmada' })).toBeVisible();
    await expect(page.getByLabel('Identificador', { exact: true })).toHaveValue(propertyId);
    await expect(page.getByRole('definition').filter({ hasText: host })).toBeVisible();
    await expect(page.getByText('active', { exact: true })).toBeVisible();
    await expect(page.getByText(updatedLocation)).toBeVisible();
    await expect(page.getByText(location)).toHaveCount(0);
    await expect(page.getByText(name)).toBeVisible();
  });

  test('rejects an edit sent with a different host and keeps the draft', async ({ page }) => {
    const hostA = randomUUID();
    const hostB = randomUUID();
    const name = `Pousada ownership ${hostA.slice(0, 8)}`;
    const location = `Local host A ${hostA.slice(0, 8)}`;
    const attemptedLocation = `Local host B ${hostB.slice(0, 8)}`;

    const propertyId = await createProperty(page, hostA, name, location);

    await page.getByRole('button', { name: 'Editar hospedagem' }).click();
    await page.getByLabel('Identificador fictício do Host').fill(hostB);
    await page.getByLabel('Localização').fill(attemptedLocation);
    await page.getByRole('button', { name: 'Salvar alterações' }).click();

    const alert = page.getByRole('alert', { name: 'Operação não permitida' });
    await expect(alert).toBeVisible();
    await expect(alert).toContainText('Somente o Host responsável pode editar esta Property.');
    await expect(alert).toContainText('Identificador de correlação:');
    await expect(page.getByLabel('Identificador', { exact: true })).toHaveValue(propertyId);
    await expect(page.getByRole('definition').filter({ hasText: hostA })).toBeVisible();
    await expect(page.getByText('active', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Localização')).toHaveValue(attemptedLocation);
    await expect(page.getByLabel('Identificador fictício do Host')).toHaveValue(hostB);
    await expect(page.getByRole('heading', { name: 'Editar hospedagem' })).toBeVisible();
  });
});
