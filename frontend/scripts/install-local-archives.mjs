import { execFileSync } from 'node:child_process';
import { readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

// npm can reuse an installed file: dependency and its old integrity when the
// archive is rebuilt without changing its name or package version.
export function installLocalArchives(cwd, archives, saveFlag, npmCli) {
  const lockPath = join(cwd, 'package-lock.json');
  const lock = JSON.parse(readFileSync(lockPath, 'utf8'));
  for (const { name } of archives) {
    const packagePath = `node_modules/${name}`;
    for (const key of Object.keys(lock.packages)) {
      if (key === packagePath || key.startsWith(`${packagePath}/`))
        delete lock.packages[key];
    }
    rmSync(join(cwd, packagePath), { recursive: true, force: true });
  }
  writeFileSync(lockPath, `${JSON.stringify(lock, null, 2)}\n`);
  // This is npm's disposable cache of the installed tree, not the project lock.
  rmSync(join(cwd, 'node_modules/.package-lock.json'), { force: true });
  execFileSync(
    process.execPath,
    [npmCli, 'install', saveFlag, ...archives.map(({ path }) => `file:${path}`)],
    { cwd, stdio: 'inherit' },
  );
}
