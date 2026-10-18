import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { installLocalArchives } from './install-local-archives.mjs';

test('reinstalls rebuilt archives with unchanged versions and paths in both consumers', () => {
  const npmCli = process.env.npm_execpath;
  assert.ok(npmCli, 'Run through npm run test:refresh-smbc');
  const root = mkdtempSync(join(tmpdir(), 'smbc-archive-test-'));
  const environment = { ...process.env };
  Object.assign(process.env, {
    npm_config_cache: join(root, 'cache'),
    npm_config_offline: 'true',
    npm_config_audit: 'false',
    npm_config_fund: 'false',
  });
  const run = (cwd, ...args) => execFileSync(process.execPath, [npmCli, ...args], { cwd, stdio: 'pipe' });
  const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));
  const createProject = (directory, name) => {
    const path = join(root, directory);
    mkdirSync(path);
    writeFileSync(join(path, 'package.json'), JSON.stringify({ name, version: '0.1.0', files: ['marker.txt'] }));
    run(path, 'install', '--package-lock-only');
    return path;
  };
  const themeArchive = { name: '@smbc/devextreme-theme', path: '../theme/smbc-devextreme-theme-0.1.0.tgz' };
  const uiArchive = { name: '@smbc/ui', path: '../ui/smbc-ui-0.1.0.tgz' };
  const verify = (cwd, archive, marker) => {
    assert.equal(readFileSync(join(cwd, 'node_modules', archive.name, 'marker.txt'), 'utf8'), marker);
    const integrity = `sha512-${createHash('sha512').update(readFileSync(join(cwd, archive.path))).digest('base64')}`;
    assert.equal(readJson(join(cwd, 'package-lock.json')).packages[`node_modules/${archive.name}`].integrity, integrity);
  };
  try {
    const theme = createProject('theme', themeArchive.name);
    const ui = createProject('ui', uiArchive.name);
    const app = createProject('app', 'consumer');
    let firstIntegrity;
    for (const marker of ['original build', 'rebuilt at the same version']) {
      writeFileSync(join(theme, 'marker.txt'), marker);
      run(theme, 'pack', '--ignore-scripts');
      installLocalArchives(ui, [themeArchive], '--save-dev', npmCli);
      verify(ui, themeArchive, marker);
      // Model the UI build consuming its newly installed theme.
      writeFileSync(join(ui, 'marker.txt'), readFileSync(join(ui, 'node_modules/@smbc/devextreme-theme/marker.txt')));
      run(ui, 'pack', '--ignore-scripts');
      installLocalArchives(app, [themeArchive, uiArchive], '--save', npmCli);
      verify(app, themeArchive, marker);
      verify(app, uiArchive, marker);
      const integrity = readJson(join(app, 'package-lock.json')).packages['node_modules/@smbc/ui'].integrity;
      if (firstIntegrity) assert.notEqual(integrity, firstIntegrity);
      firstIntegrity = integrity;
    }
    assert.equal(readJson(join(ui, 'package.json')).devDependencies[themeArchive.name], `file:${themeArchive.path}`);
    assert.equal(readJson(join(app, 'package.json')).dependencies[uiArchive.name], `file:${uiArchive.path}`);
    // A separate empty cache must install exactly the newly packed contents.
    process.env.npm_config_cache = join(root, 'empty-cache');
    run(app, 'ci');
    verify(app, themeArchive, 'rebuilt at the same version');
    verify(app, uiArchive, 'rebuilt at the same version');
  } finally {
    for (const key of Object.keys(process.env)) {
      if (!(key in environment)) delete process.env[key];
    }
    Object.assign(process.env, environment);
    rmSync(root, { recursive: true, force: true });
  }
});
