import { execFileSync } from 'node:child_process';
import { copyFileSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { installLocalArchives } from './install-local-archives.mjs';

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = process.argv[2];
if (!source) throw new Error('Usage: npm run refresh:smbc -- /path/to/smbc');
const smbc = resolve(source);
const npmCli = process.env.npm_execpath;
if (!npmCli) throw new Error('Run this command through npm run refresh:smbc.');
const run = (cwd, ...args) =>
  execFileSync(process.execPath, [npmCli, ...args], { cwd, stdio: 'inherit' });
const archive = (cwd) => {
  const { name, version } = JSON.parse(
    readFileSync(join(cwd, 'package.json'), 'utf8'),
  );
  return `${name.replace(/^@/, '').replaceAll('/', '-')}-${version}.tgz`;
};
const theme = join(smbc, 'devextreme-theme');
const ui = join(smbc, 'smbc-ui');
for (const project of [theme, ui]) readFileSync(join(project, 'package.json'));
run(theme, 'ci');
run(theme, 'run', 'check');
run(theme, 'pack', '--ignore-scripts');
const themeArchive = archive(theme);
installLocalArchives(
  ui,
  [{
    name: '@smbc/devextreme-theme',
    path: relative(ui, join(theme, themeArchive)).replaceAll('\\', '/'),
  }],
  '--save-dev',
  npmCli,
);
run(ui, 'run', 'check');
run(ui, 'pack', '--ignore-scripts');
const uiArchive = archive(ui);
mkdirSync(join(frontend, 'vendor'), { recursive: true });
for (const [project, filename] of [
  [theme, themeArchive],
  [ui, uiArchive],
]) {
  copyFileSync(join(project, filename), join(frontend, 'vendor', filename));
}
installLocalArchives(
  frontend,
  [
    { name: '@smbc/devextreme-theme', path: `vendor/${themeArchive}` },
    { name: '@smbc/ui', path: `vendor/${uiArchive}` },
  ],
  '--save',
  npmCli,
);
