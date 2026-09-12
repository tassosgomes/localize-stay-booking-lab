#!/usr/bin/env node
/**
 * Regenerates Catalog types from the approved OpenAPI contract into a temp file
 * and compares them with src/services/api/generated/catalog.ts.
 * Do not edit catalog.ts by hand — fix the contract and rerun api:generate.
 */
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const contractPath = join(
  frontendRoot,
  '../../tasks/prd-cadastro-property/api-contract.yaml',
);
const committedPath = join(frontendRoot, 'src/services/api/generated/catalog.ts');
const cliPath = join(frontendRoot, 'node_modules/openapi-typescript/bin/cli.js');

const tempDir = mkdtempSync(join(tmpdir(), 'catalog-types-'));
const generatedPath = join(tempDir, 'catalog.ts');

function normalize(text) {
  return text.replace(/\r\n/g, '\n');
}

try {
  const result = spawnSync(process.execPath, [cliPath, contractPath, '-o', generatedPath], {
    cwd: frontendRoot,
    encoding: 'utf8',
  });

  if (result.status !== 0) {
    process.stderr.write(result.stdout ?? '');
    process.stderr.write(result.stderr ?? '');
    process.stderr.write('api:check: openapi-typescript failed\n');
    process.exit(result.status === null ? 1 : result.status);
  }

  const committed = normalize(readFileSync(committedPath, 'utf8'));
  const generated = normalize(readFileSync(generatedPath, 'utf8'));

  if (committed !== generated) {
    process.stderr.write(
      'api:check: catalog.ts drifted from tasks/prd-cadastro-property/api-contract.yaml.\n' +
        'Run `npm run api:generate` and commit the generated file without manual edits.\n',
    );
    process.exit(1);
  }
} finally {
  rmSync(tempDir, { recursive: true, force: true });
}
