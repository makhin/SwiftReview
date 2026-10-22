import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { test } from 'node:test';

// Run the actual CLI against a small HTTP server. No dev database is changed.
async function runDemo(levels: number[], failAt = '') {
  const calls: { path: string; body: any; user: string | undefined }[] = [];
  let assigned = false;
  let completedLevels = 0;
  let version = 1;
  const server = createServer(async (req, res) => {
    let text = '';
    for await (const chunk of req) text += chunk;
    const body = text ? JSON.parse(text) : undefined;
    const path = req.url!;
    calls.push({ path, body, user: req.headers['x-debug-user'] as string });
    if (failAt && path.endsWith(failAt)) {
      res.writeHead(409);
      res.end('Reload before confirming.');
      return;
    }
    let data: any;
    if (path === '/api/me') data = { userId: 42, userName: 'ordinary' };
    if (path === '/api/workflows') data = [{ id: 8, code: 'CUSTOM', name: 'Custom workflow', confirmationMode: 'Immediate' }];
    if (path === '/api/messages/grid') data = { items: [{ id: 99, workflowDefinitionId: 8, externalId: 'TEST' }], totalCount: 1 };
    if (path === '/api/messages/99') data = {
      state: completedLevels === levels.length ? 'Completed' : assigned ? 'Assigned' : 'New',
      currentAssigneeId: assigned ? 42 : null,
      canConfirm: assigned && completedLevels < levels.length,
      confirmationLevel: levels[completedLevels], version: `v${version}`,
    };
    if (path.endsWith('/assignment-candidates')) data = [{ id: 42 }];
    if (path.endsWith('/assign')) { assigned = true; version++; res.writeHead(204); res.end(); return; }
    if (path.endsWith('/confirm')) {
      if (body.level !== levels[completedLevels]) {
        res.writeHead(400); res.end('Wrong level'); return;
      }
      completedLevels++; version++;
      res.writeHead(204); res.end(); return;
    }
    if (path.endsWith('/view')) data = { approvals: [] };
    if (path.endsWith('/state-counts')) data = { items: [] };
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify(data));
  });
  await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  assert(address && typeof address !== 'string');
  try {
    const child = spawn(process.execPath, [new URL('./workflow-demo.ts', import.meta.url).pathname], {
      env: { ...process.env, DEMO_API_URL: `http://127.0.0.1:${address.port}/api`,
        DEMO_USER: 'ordinary', DEMO_WORKFLOW: 'CUSTOM', DEMO_MESSAGE_ID: '99',
        DEMO_PAUSE: 'false', DEMO_COMMENT: 'Looks good', DEMO_PAGE: '1', DEMO_PAGE_SIZE: '20' },
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    let output = '';
    child.stdout.on('data', chunk => { output += chunk; });
    child.stderr.on('data', chunk => { output += chunk; });
    const code = await new Promise<number | null>((resolve, reject) => {
      child.on('error', reject);
      child.on('close', resolve);
    });
    return { code, output, calls };
  } finally {
    await new Promise<void>(resolve => server.close(() => resolve()));
  }
}

for (const levels of [[1], [1, 2], [1, 3]]) {
  test(`CLI assigns itself and confirms backend levels ${levels}`, { timeout: 5000 }, async () => {
    const result = await runDemo(levels);
    assert.equal(result.code, 0, result.output);
    assert.deepEqual(result.calls.find(c => c.path.endsWith('/assign'))?.body, { assignedTo: 42 });
    const confirmations = result.calls.filter(c => c.path.endsWith('/confirm'));
    assert.deepEqual(confirmations.map(c => c.body.level), levels);
    assert.deepEqual(confirmations.map(c => c.body), levels.map(level => ({ level, comment: "Looks good" })));
    assert(result.calls.every(c => c.user === 'ordinary'));
    assert.match(result.output, /Completed: message 99/);
  });
}

test('CLI stops on a conflict and does not retry', { timeout: 5000 }, async () => {
  const result = await runDemo([1, 2], '/confirm');
  assert.equal(result.code, 1);
  assert.match(result.output, /Reload before confirming/);
  assert.equal(result.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
  assert(!result.calls.some(c => c.path.endsWith('/view')));
});
