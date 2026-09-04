import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';

const check = process.argv.slice(2).includes('--check');
const unknownArguments = process.argv.slice(2).filter((argument) => argument !== '--check');

if (unknownArguments.length > 0) {
  throw new Error(`Unknown argument: ${unknownArguments[0]}`);
}

const frontendDirectory = fileURLToPath(new URL('..', import.meta.url));
const repositoryDirectory = fileURLToPath(new URL('../..', import.meta.url));
const generatorProject = join(
  repositoryDirectory,
  'tools',
  'OpenApiTsContracts',
  'OpenApiTsContracts.csproj',
);
const outputPath = join(
  frontendDirectory,
  'src',
  'shared',
  'api',
  'generated',
  'contracts.generated.ts',
);
const temporaryDirectory = await mkdtemp(join(tmpdir(), 'openapi-ts-contracts-'));
const inputPath = join(temporaryDirectory, 'openapi.json');

try {
  const response = await fetch('http://localhost:5080/openapi/v1.json');
  if (!response.ok) {
    throw new Error(`Unable to load backend OpenAPI document (${response.status}).`);
  }

  await writeFile(inputPath, await response.text(), 'utf8');

  const argumentsList = [
    'run',
    '--project',
    generatorProject,
    '--',
    '--input',
    inputPath,
    '--output',
    outputPath,
  ];
  if (check) {
    argumentsList.push('--check');
  }

  const exitCode = await new Promise((resolve, reject) => {
    const child = spawn('dotnet', argumentsList, { stdio: 'inherit' });
    child.once('error', reject);
    child.once('exit', (code, signal) => {
      if (signal) {
        reject(new Error(`Contract generator terminated by signal ${signal}.`));
      } else {
        resolve(code ?? 4);
      }
    });
  });

  process.exitCode = exitCode;
} finally {
  await rm(temporaryDirectory, { recursive: true, force: true });
}
