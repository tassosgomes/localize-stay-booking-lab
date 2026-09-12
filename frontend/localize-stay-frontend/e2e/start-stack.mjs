#!/usr/bin/env node
/**
 * Starts an isolated Catalog (Docker Postgres + migrations + Kestrel) and Vite
 * for Playwright. Does not use MSW. Does not migrate postgres-main (that would
 * drop Fundação's catalog.__bootstrap_check). Infra failure exits 2.
 */
import { spawn, spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = resolve(frontendRoot, '../..');
const vitePort = process.env.E2E_VITE_PORT ?? '5175';
const catalogUrl = process.env.E2E_CATALOG_URL ?? 'http://127.0.0.1:5111';
const frontendOrigin = `http://localhost:${vitePort}`;
// Booking é externo ao harness E2E (sobe via scripts/dev-up.sh, que injeta as
// origens E2E no Booking via env Cors__AllowedOrigins — ver F1 de
// tasks/prd-solicitacao-reserva/full_review.md). O default de produto permite
// só http://localhost:5173; sem o env do harness, o preflight de :5175 volta
// sem Access-Control-Allow-Origin e o browser bloqueia o POST /v1/reservations.
const bookingUrl = (
  process.env.E2E_BOOKING_URL ??
  process.env.VITE_BOOKING_URL ??
  'http://localhost:5102'
).replace(/\/+$/, '');
const bookingApiUrl =
  process.env.VITE_BOOKING_API_URL ?? `${bookingUrl}/v1`;
const e2eOrigins = [frontendOrigin, `http://127.0.0.1:${vitePort}`];
const pgContainer = process.env.E2E_PG_CONTAINER ?? 'localize-stay-e2e-pg';
const pgPort = process.env.E2E_PG_PORT ?? '54332';
const catalogConnection =
  process.env.ConnectionStrings__Catalog ??
  `Host=127.0.0.1;Port=${pgPort};Database=localize_stay;Username=postgres;Password=postgres`;
const catalogProject = resolve(
  repoRoot,
  'services/catalog/src/1-Services/LocalizeStay.Catalog.Api/LocalizeStay.Catalog.Api.csproj',
);
const infraProject = resolve(
  repoRoot,
  'services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/LocalizeStay.Catalog.Infra.csproj',
);

const children = [];

function failInfra(message) {
  console.error(`E2E_INFRA_UNAVAILABLE: ${message}`);
  process.exit(2);
}

function sleep(ms) {
  return new Promise((resolvePromise) => {
    setTimeout(resolvePromise, ms);
  });
}

async function httpOk(url, timeoutMs = 3000) {
  try {
    const response = await fetch(url, { signal: AbortSignal.timeout(timeoutMs) });
    return response.ok;
  } catch {
    return false;
  }
}

function runSync(command, args, options = {}) {
  const result = spawnSync(command, args, {
    encoding: 'utf8',
    ...options,
  });
  if (result.error) {
    failInfra(`${command} failed to start: ${result.error.message}`);
  }
  return result;
}

function spawnChild(command, args, options) {
  const child = spawn(command, args, { stdio: 'inherit', ...options });
  children.push(child);
  child.on('error', (error) => {
    failInfra(`${command} failed to start: ${error.message}`);
  });
  return child;
}

function shutdown(code = 0) {
  for (const child of children) {
    if (!child.killed) {
      child.kill('SIGTERM');
    }
  }
  process.exit(code);
}

process.on('SIGTERM', () => shutdown(0));
process.on('SIGINT', () => shutdown(0));

async function waitFor(predicate, timeoutMs, message) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) {
      return;
    }
    await sleep(1000);
  }
  failInfra(message);
}

async function ensurePostgres() {
  const docker = runSync('docker', ['info']);
  if (docker.status !== 0) {
    failInfra('Docker daemon is unavailable; E2E cannot start an isolated Postgres');
  }

  const inspect = runSync('docker', ['inspect', '-f', '{{.State.Running}}', pgContainer]);
  if (inspect.status !== 0 || inspect.stdout.trim() !== 'true') {
    runSync('docker', ['rm', '-f', pgContainer], { stdio: 'ignore' });
    const started = runSync('docker', [
      'run',
      '-d',
      '--name',
      pgContainer,
      '-e',
      'POSTGRES_USER=postgres',
      '-e',
      'POSTGRES_PASSWORD=postgres',
      '-e',
      'POSTGRES_DB=localize_stay',
      '-p',
      `${pgPort}:5432`,
      'postgres:16-alpine',
    ]);
    if (started.status !== 0) {
      failInfra(`failed to start Postgres container: ${started.stderr || started.stdout}`);
    }
  }

  await waitFor(async () => {
    const ready = runSync('docker', [
      'exec',
      pgContainer,
      'pg_isready',
      '-U',
      'postgres',
      '-d',
      'localize_stay',
    ]);
    return ready.status === 0;
  }, 60_000, `Postgres container ${pgContainer} did not become ready`);
}

function applyMigrations() {
  const restore = runSync('dotnet', ['tool', 'restore'], { cwd: repoRoot });
  if (restore.status !== 0) {
    failInfra(`dotnet tool restore failed: ${restore.stderr || restore.stdout}`);
  }

  const update = runSync(
    'dotnet',
    [
      'ef',
      'database',
      'update',
      '--project',
      infraProject,
      '--startup-project',
      catalogProject,
    ],
    {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Catalog: catalogConnection,
      },
    },
  );
  if (update.status !== 0) {
    failInfra(`dotnet ef database update failed: ${update.stderr || update.stdout}`);
  }
}

async function ensureCatalog() {
  const readyUrl = `${catalogUrl.replace(/\/+$/, '')}/health/ready`;
  if (await httpOk(readyUrl)) {
    console.log(`E2E: reusing Catalog at ${catalogUrl}`);
    return;
  }

  await ensurePostgres();
  applyMigrations();

  const catalog = spawnChild(
    'dotnet',
    ['run', '--project', catalogProject, '--no-launch-profile', '--urls', catalogUrl],
    {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Catalog: catalogConnection,
        Cors__AllowedOrigins__0: frontendOrigin,
        Cors__AllowedOrigins__1: `http://127.0.0.1:${vitePort}`,
      },
    },
  );

  await waitFor(async () => {
    if (catalog.exitCode !== null) {
      failInfra(`Catalog exited with code ${catalog.exitCode} before becoming ready`);
    }
    return httpOk(readyUrl);
  }, 120_000, `Catalog did not become ready at ${readyUrl}`);

  console.log(`E2E: Catalog ready at ${catalogUrl}`);
}

await ensureCatalog();

// Guarda de diagnóstico (nunca fatal): confere que o Booking externo permite
// as origens do harness E2E. Se o Booking estiver desatualizado (subido antes
// do fix F1, sem Cors__AllowedOrigins de :5175), o spec de reservation-request
// falharia com "Erro inesperado" no browser; o aviso abaixo diz exatamente
// como corrigir. PropertyUpdate não chama o Booking, então a ausência dele
// (ou o CORS pendente) nunca derruba este harness.
async function probeBookingCors() {
  for (const origin of e2eOrigins) {
    let allowOrigin = null;
    try {
      const response = await fetch(`${bookingUrl}/v1/reservations`, {
        method: 'OPTIONS',
        headers: {
          Origin: origin,
          'Access-Control-Request-Method': 'POST',
        },
        signal: AbortSignal.timeout(5000),
      });
      allowOrigin = response.headers.get('access-control-allow-origin');
    } catch (error) {
      console.warn(
        `E2E: Booking em ${bookingUrl} inalcançável (${error.message}). ` +
          `PropertyUpdate não precisa dele; reservation-request exige o Booking do scripts/dev-up.sh no ar.`,
      );
      return false;
    }
    if (allowOrigin !== origin) {
      console.warn(
        `E2E: Booking em ${bookingUrl} NÃO permite a origem E2E ${origin} ` +
          `(Access-Control-Allow-Origin: ${allowOrigin ?? '<ausente>'}). ` +
          `Suba o Booking via scripts/dev-up.sh (que injeta Cors__AllowedOrigins de :5175) ` +
          `ou reinicie-o com Cors__AllowedOrigins__0=${e2eOrigins[0]} Cors__AllowedOrigins__1=${e2eOrigins[1]}.`,
      );
      return false;
    }
  }
  console.log(`E2E: Booking CORS ok em ${bookingUrl} para ${e2eOrigins.join(' + ')}`);
  return true;
}

await probeBookingCors();

const vite = spawnChild(
  process.execPath,
  [
    resolve(frontendRoot, 'node_modules/vite/bin/vite.js'),
    '--port',
    vitePort,
    '--strictPort',
    '--host',
    '0.0.0.0',
  ],
  {
    cwd: frontendRoot,
    env: {
      ...process.env,
      VITE_CATALOG_URL: catalogUrl,
      VITE_BOOKING_URL: process.env.VITE_BOOKING_URL ?? bookingUrl,
      VITE_BOOKING_API_URL: bookingApiUrl,
      VITE_PAYMENT_URL: process.env.VITE_PAYMENT_URL ?? 'http://localhost:5103',
    },
  },
);

vite.on('exit', (code) => {
  shutdown(code ?? 0);
});
