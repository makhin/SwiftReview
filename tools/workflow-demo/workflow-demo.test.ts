import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { CONFIG, runWorkflow } from './workflow-demo.ts';

type Options = { levels?: number[]; eligible?: boolean; conflict?: boolean; administrator?: boolean; assignee?: number };
async function fixture(options: Options = {}) {
  const levels = options.levels ?? [1, 2];
  const calls: { path: string; method: string; body: any; user: string | undefined }[] = [];
  let index = 0;
  let version = 1;
  let assignee: number | null = options.assignee ?? null;
  const logs: string[] = [];
  const state = () => index === levels.length ? 'Completed'
    : index > 0 ? `WaitingFor${levels[index] === 3 ? 'Third' : 'Second'}Review` : assignee ? 'Assigned' : 'New';
  const server = createServer(async (req, res) => {
    let raw = '';
    for await (const chunk of req) raw += chunk;
    const body = raw ? JSON.parse(raw) : undefined;
    const path = req.url!;
    calls.push({ path, method: req.method!, body, user: req.headers['x-debug-user'] as string });
    const json = (data: unknown, status = 200) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); };
    const empty = () => { res.writeHead(204); res.end(); };
    if (path === '/api/me') return json({ userId: 42, userName: 'ordinary', isGlobalAdministrator: options.administrator ?? false });
    if (path === '/api/workflows') return json([{ id: 8, code: 'CUSTOM', name: 'Custom workflow', isActive: true,
      confirmationMode: 'Immediate', steps: levels.map((reviewLevel, order) => ({ order, reviewLevel, required: true })) }]);
    if (path === '/api/messages/grid') {
      // The matching message is deliberately on page two.
      return json({ totalCount: 21, items: body.page === 1
        ? [{ id: 4, workflowDefinitionId: 999, externalId: 'OTHER', state: 'New', currentAssigneeId: null }]
        : [{ id: 99, workflowDefinitionId: 8, externalId: 'TEST', state: state(), currentAssigneeId: assignee }] });
    }
    if (path === '/api/messages/99') return json({ id: 99, state: state(), currentAssigneeId: assignee,
      confirmationMode: 'Immediate', canConfirm: assignee === 42 && index < levels.length,
      confirmationLevel: levels[index] ?? null, version: `version-${version}`, body: 'Synthetic message' });
    if (path.endsWith('/assignment-candidates')) return json(options.eligible === false ? [] : [{ id: 42, displayName: 'Ordinary' }]);
    if (path.endsWith('/assign')) { assignee = body.assignedTo; version++; return empty(); }
    if (path.endsWith('/reviews/confirm')) {
      if (options.conflict) return json({ title: 'Conflict', detail: 'Reload before confirming.' }, 409);
      if (body.level !== levels[index] || body.expectedVersion !== `version-${version}`) return json({ detail: 'Wrong level/version' }, 400);
      index++; version++;
      if (index === levels.length) assignee = null;
      return empty();
    }
    if (path.endsWith('/view')) return json({ approvals: levels.map(level => ({ level, status: 'approved' })), auditLog: [] });
    if (path.endsWith('/state-counts')) return json({ items: [], total: { count: 21 } });
    json({ detail: 'Unknown path' }, 404);
  });
  await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  assert(address && typeof address !== 'string');
  return { calls, logs,
    config: { ...CONFIG, apiUrl: `http://127.0.0.1:${address.port}/api`, user: 'ordinary', workflow: 'Custom workflow', messageId: '99', pause: false },
    io: { log: (text: string) => logs.push(text), question: async () => '' },
    close: () => new Promise<void>((resolve, reject) => server.close(error => error ? reject(error) : resolve())),
  };
}

for (const levels of [[1], [1, 2], [1, 3]]) {
  test(`self-assign and confirm dynamic levels ${levels}`, async () => {
    const f = await fixture({ levels });
    try {
      await runWorkflow(f.config, f.io);
      assert.equal(f.calls.filter(c => c.path.endsWith('/grid')).length, 2);
      assert.deepEqual(f.calls.find(c => c.path.endsWith('/assign'))?.body, { assignedTo: 42 });
      const confirmations = f.calls.filter(c => c.path.endsWith('/confirm'));
      assert.deepEqual(confirmations.map(c => c.body.level), levels);
      assert.deepEqual(confirmations.map(c => c.body.expectedVersion), levels.map((_, i) => `version-${i + 2}`));
      assert(f.calls.every(c => c.user === 'ordinary'));
      assert(f.logs.some(line => line.includes('→ Completed')));
    } finally { await f.close(); }
  });
}

for (const [name, options, error] of [
  ['ineligible self-assignment', { eligible: false }, /not an assignment candidate/],
  ['administrator', { administrator: true }, /ordinary user/],
  ['other assignee', { assignee: 7 }, /another assignee/],
] as const) {
  test(`stop before mutation: ${name}`, async () => {
    const f = await fixture(options);
    try {
      await assert.rejects(runWorkflow(f.config, f.io), error);
      assert(!f.calls.some(c => c.path.endsWith('/assign') || c.path.endsWith('/confirm')));
    } finally { await f.close(); }
  });
}

test('a 409 stops the flow without retrying or advancing to the next level', async () => {
  const f = await fixture({ conflict: true });
  try {
    await assert.rejects(runWorkflow(f.config, f.io), /HTTP 409/);
    assert.equal(f.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
    assert(!f.calls.some(c => c.path.endsWith('/view')));
  } finally { await f.close(); }
});

test('q stops before sending the displayed request', async () => {
  const f = await fixture();
  try {
    await assert.rejects(runWorkflow({ ...f.config, pause: true }, { ...f.io, question: async () => 'q' }), /Stopped/);
    assert.equal(f.calls.length, 0);
  } finally { await f.close(); }
});

test('interactive selection resolves a workflow ID and uses the selected message', async () => {
  const f = await fixture({ levels: [1] });
  const prompts: string[] = [];
  try {
    await runWorkflow({ ...f.config, workflow: '8', messageId: '', pause: true }, {
      ...f.io,
      question: async prompt => { prompts.push(prompt); return prompt.startsWith('Message ID') ? '99' : ''; },
    });
    assert(prompts.some(prompt => prompt.startsWith('Message ID')));
    assert.equal(f.calls.filter(c => c.path.endsWith('/assign')).length, 1);
    assert.equal(f.calls.filter(c => c.path.endsWith('/confirm')).length, 1);
  } finally { await f.close(); }
});
