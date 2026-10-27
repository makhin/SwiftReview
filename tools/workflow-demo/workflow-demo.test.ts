import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { test } from 'node:test';

// Run the actual CLI against a small HTTP server. No dev database is changed.
async function runDemo(levels: number[], actions: string, options: {
  failAt?: string; mode?: string; reuse?: boolean; owner?: number; forbiddenUser?: string; interactive?: boolean;
} = {}) {
  const calls: { path: string; body: any; user: string }[] = [];
  let owner = options.owner ?? null;
  let completed = 0;
  let active = false;
  let rejected = false;
  const reviewers: number[] = [];
  const mode = options.mode ?? 'Immediate';
  const server = createServer(async (req, res) => {
    let text = '';
    for await (const chunk of req) text += chunk;
    const body = text ? JSON.parse(text) : undefined;
    const path = req.url!;
    const user = req.headers['x-debug-user'] as string;
    const actor = user === 'second' ? 43 : 42;
    calls.push({ path, body, user });
    function error(status: number, text: string) { res.writeHead(status); res.end(text); }
    function ok() { res.writeHead(204); res.end(); }
    if (options.failAt && path.endsWith(options.failAt)) return error(409, 'Reload before confirming.');
    let data: any;
    if (path === '/api/me') data = { userId: actor, userName: user };
    else if (path === '/api/workflows') data = [{ id: 8, code: 'CUSTOM', confirmationMode: mode }];
    else if (path === '/api/messages/grid') data = { items: [{ id: 99, workflowDefinitionId: 8, externalId: 'TEST' }] };
    else if (path === '/api/messages/99/view') {
      if (user === options.forbiddenUser) return error(403, 'Cannot read this message.');
      const eligible = !rejected && completed < levels.length &&
        (owner === null || owner === actor) && (options.reuse !== false || !reviewers.includes(actor));
      data = {
        id: '99', status: completed === levels.length ? 'Done (display label)' : 'Awaiting approval',
        currentAssigneeId: owner, confirmationMode: mode,
        canConfirm: mode === 'Immediate' && eligible,
        confirmationLevel: mode === 'Immediate' && eligible ? levels[completed] : null,
        canReview: mode === 'ReviewSession' && eligible && owner === actor,
        approvals: levels.map((level, i) => ({ level,
          status: i < completed ? 'approved' : i === completed ? (rejected ? 'rejected' : 'pending') : 'waiting' })),
        auditLog: [],
      };
    } else if (path.endsWith('/assignment-candidates')) data = [{ id: 42 }, { id: 43 }];
    else if (path.endsWith('/assign')) { owner = body.assignedTo; return ok(); }
    else if (path.endsWith('/confirm')) {
      if (mode !== 'Immediate') return error(409, 'Wrong mode.');
      if (owner !== null && owner !== actor) return error(403, 'Assigned to another reviewer.');
      if (options.reuse === false && reviewers.includes(actor)) return error(409, 'Reviewer reuse disabled.');
      if (body.level !== levels[completed]) return error(409, 'Wrong level.');
      completed++;
      reviewers.push(actor);
      owner = null; // PerConfirmation: another unassigned level may follow.
      return ok();
    } else if (path.endsWith('/start')) {
      if (mode !== 'ReviewSession' || owner !== actor || body.level !== levels[completed])
        return error(409, 'Cannot start.');
      active = true;
      data = { reviewId: 501 };
    } else if (/\/(approve|reject|cancel)$/.test(path)) {
      if (!active || owner !== actor || body.reviewId !== 501 || body.level !== levels[completed])
        return error(409, 'Wrong session.');
      if (path.endsWith('/approve')) { completed++; owner = null; }
      if (path.endsWith('/reject')) { rejected = true; owner = null; }
      active = false;
      return ok();
    } else if (path.endsWith('/state-counts')) data = { items: [] };
    else return error(404, 'Unexpected endpoint.'); // In particular, /messages/99 is forbidden here.
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify(data));
  });
  await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  assert(address && typeof address !== 'string');
  try {
    const child = spawn(process.execPath, [new URL('./workflow-demo.ts', import.meta.url).pathname], {
      env: { ...process.env, DEMO_API_URL: `http://127.0.0.1:${address.port}/api`,
        DEMO_USER: 'ordinary', DEMO_WORKFLOW: 'CUSTOM', DEMO_MESSAGE_ID: '99', DEMO_ACTIONS: options.interactive ? '' : actions,
        DEMO_STATUS: '', DEMO_PAUSE: 'false', DEMO_COMMENT: 'Looks good', DEMO_PAGE: '1', DEMO_PAGE_SIZE: '20' },
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    let output = '';
    const commands = actions.split(',');
    child.stdout.on('data', chunk => {
      output += chunk;
      if (options.interactive && chunk.toString().includes('Action: '))
        child.stdin.write((commands.shift() ?? 'quit') + '\n');
    });
    child.stderr.on('data', chunk => { output += chunk; });
    const code = await new Promise<number | null>((resolve, reject) => {
      child.on('error', reject);
      child.on('close', resolve);
    });
    assert(!calls.some(c => c.path === '/api/messages/99'));
    return { code, output, calls };
  } finally {
    await new Promise<void>(resolve => server.close(() => resolve()));
  }
}

for (const levels of [[1], [1, 2], [1, 3]]) {
  test(`complete follows /view levels ${levels} without a separate assign`, { timeout: 5000 }, async () => {
    const result = await runDemo(levels, 'complete');
    assert.equal(result.code, 0, result.output);
    assert(!result.calls.some(c => c.path.endsWith('/assign')));
    assert.deepEqual(result.calls.filter(c => c.path.endsWith('/confirm')).map(c => c.body),
      levels.map(level => ({ level, comment: 'Looks good' })));
  });
}

test('explicit assignment, confirmation and switching reviewer', async () => {
  const result = await runDemo([1, 2], 'candidates,assign,confirm,user:second,confirm', { reuse: false });
  assert.equal(result.code, 0, result.output);
  assert.deepEqual(result.calls.find(c => c.path.endsWith('/assign'))?.body, { assignedTo: 42 });
  assert.deepEqual(result.calls.filter(c => c.path.endsWith('/confirm')).map(c => c.user), ['ordinary', 'second']);
});

test('another assignee is not taken over by confirm', async () => {
  const result = await runDemo([1], 'assign:43,confirm:1');
  assert.equal(result.code, 1);
  assert.match(result.output, /Assigned to another reviewer/);
  assert.equal(result.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
});

test('complete stops when reviewer reuse is disabled', async () => {
  const result = await runDemo([1, 2], 'complete', { reuse: false });
  assert.equal(result.code, 0, result.output);
  assert.equal(result.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
});

test('duplicate explicit level is sent once and stops the scenario', async () => {
  const result = await runDemo([1, 2], 'confirm,confirm:1,complete');
  assert.equal(result.code, 1);
  assert.match(result.output, /Wrong level/);
  assert.deepEqual(result.calls.filter(c => c.path.endsWith('/confirm')).map(c => c.body.level), [1, 1]);
});

for (const action of ['approve', 'reject', 'cancel']) {
  test(`ReviewSession start + ${action} uses returned reviewId`, async () => {
    const result = await runDemo([1], `assign,start,${action}`, { mode: 'ReviewSession' });
    assert.equal(result.code, 0, result.output);
    assert.deepEqual(result.calls.find(c => c.path.endsWith(`/${action}`))?.body,
      { reviewId: 501, level: 1, ...(action === 'cancel' ? {} : { comment: 'Looks good' }) });
  });
}

test('switching to a user without access stops before a mutation', async () => {
  const result = await runDemo([1], 'user:second,confirm', { forbiddenUser: 'second' });
  assert.equal(result.code, 1);
  assert(!result.calls.some(c => c.path.endsWith('/confirm')));
});

test('HTTP conflict is never retried', async () => {
  const result = await runDemo([1, 2], 'complete', { failAt: '/confirm' });
  assert.equal(result.code, 1);
  assert.equal(result.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
});

test('interactive mode stops on an ownership error', { timeout: 5000 }, async () => {
  const result = await runDemo([1], 'confirm:1,user:second,confirm,quit', { owner: 43, interactive: true });
  assert.equal(result.code, 1);
  assert.match(result.output, /Assigned to another reviewer/);
  assert.deepEqual(result.calls.filter(c => c.path.endsWith('/confirm')).map(c => c.user), ['ordinary']);
  assert(!result.calls.some(c => c.user === 'second'));
});

test('interactive mode can switch reviewers after complete stops', { timeout: 5000 }, async () => {
  const result = await runDemo([1, 2], 'complete,user:second,complete,quit', { reuse: false, interactive: true });
  assert.equal(result.code, 0, result.output);
  assert.deepEqual(result.calls.filter(c => c.path.endsWith('/confirm')).map(c => ({ user: c.user, level: c.body.level })),
    [{ user: 'ordinary', level: 1 }, { user: 'second', level: 2 }]);
});
