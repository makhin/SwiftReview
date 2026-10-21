import { createInterface } from 'node:readline/promises';
import { pathToFileURL } from 'node:url';

// Edit these defaults or override them with environment variables. No npm install is needed.
export const CONFIG = {
  apiUrl: process.env.DEMO_API_URL ?? 'http://localhost:5080/api',
  user: process.env.DEMO_USER ?? 'dc.reviewer1', // Username or numeric user ID (Development).
  workflow: process.env.DEMO_WORKFLOW ?? 'SINGLE_CONFIRMATION', // Code, exact name or ID.
  messageId: process.env.DEMO_MESSAGE_ID ?? '', // Empty: select from the accessible queue.
  comment: process.env.DEMO_COMMENT ?? 'Confirmed through the API walkthrough',
  pause: process.env.DEMO_PAUSE !== 'false', // false requires an explicit messageId.
};

type Config = typeof CONFIG;
type Id = number | string;
type Workflow = {
  id: Id; code: string; name: string; isActive: boolean; confirmationMode: string;
  steps: { order: number; reviewLevel: number; required: boolean }[];
};
type Row = {
  id: Id; externalId: string; workflowDefinitionId: Id; state: string;
  currentAssigneeId: Id | null;
};
type Message = {
  id: Id; state: string; currentAssigneeId: Id | null; confirmationMode: string;
  canConfirm: boolean; confirmationLevel: number | null; version: string; body: string | null;
};
type IO = { log: (text: string) => void; question: (prompt: string) => Promise<string> };
const sameId = (left: Id | null, right: Id | null) => String(left) === String(right);

/** The demonstration uses real HTTP. IO is injectable so tests need no terminal. */
export async function runWorkflow(config: Config, io: IO): Promise<void> {
  if (!config.user.trim() || !config.workflow.trim()) throw new Error('Set user and workflow first.');
  if (!config.pause && !config.messageId) throw new Error('Non-interactive mode requires DEMO_MESSAGE_ID.');
  const base = config.apiUrl.replace(/\/$/, '');
  let step = 0;

  async function request<T>(method: string, path: string, reason: string, body?: unknown,
    display: (value: T) => unknown = value => value): Promise<T> {
    io.log(`\n${++step}. ${reason}\n→ ${method} ${base}${path}\nX-Debug-User: ${config.user}`);
    if (body !== undefined) io.log(`Content-Type: application/json\n${JSON.stringify(body, null, 2)}`);
    if (config.pause) {
      const answer = await io.question('Enter to send; q to stop: ');
      if (answer.trim().toLowerCase() === 'q') throw new Error('Stopped. Earlier successful changes remain saved.');
    }
    let response: Response;
    try {
      response = await fetch(`${base}${path}`, {
        method,
        headers: { 'X-Debug-User': config.user, 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(15_000),
      });
    } catch (error) {
      throw new Error('Network error. A mutation may have been saved. Check the message and audit before retrying.', { cause: error });
    }
    io.log(`← HTTP ${response.status} ${response.statusText}`);
    const correlation = response.headers.get('X-Correlation-ID');
    if (correlation) io.log(`X-Correlation-ID: ${correlation}`);
    const text = await response.text();
    if (!response.ok) {
      io.log(text);
      throw new Error(`HTTP ${response.status}: stopped without retrying. Reload the message before another decision.`);
    }
    if (response.status === 204) {
      io.log('(No response body; do not call response.json().)');
      return undefined as T;
    }
    const result = JSON.parse(text) as T;
    io.log(JSON.stringify(display(result), null, 2));
    return result;
  }

  const me = await request<{ userId: Id; userName: string; isGlobalAdministrator: boolean }>(
    'GET', '/me', 'Resolve the authenticated user. assignedTo will use me.userId.');
  if (me.isGlobalAdministrator) throw new Error('Choose an ordinary user: administrator exceptions would hide workflow restrictions.');

  const workflows = await request<Workflow[]>('GET', '/workflows', 'Resolve the configured workflow from the backend.');
  const matches = workflows.filter(w => sameId(w.id, config.workflow) || w.code === config.workflow || w.name === config.workflow);
  if (matches.length !== 1) throw new Error('Workflow was not found or its name is ambiguous. Use an exact code or ID.');
  const workflow = matches[0]!;
  if (!workflow.isActive || workflow.confirmationMode !== 'Immediate') {
    throw new Error('This walkthrough requires an active Immediate workflow. ReviewSession uses start/approve instead.');
  }
  const requiredLevels = workflow.steps.filter(s => s.required).sort((a, b) => a.order - b.order).map(s => s.reviewLevel);
  if (!requiredLevels.length) throw new Error('The workflow has no required confirmation levels.');

  // /messages/grid has no workflow filter: inspect pages and match workflowDefinitionId locally.
  // This is a console walkthrough. In the UI, grid loading belongs in DevExtreme CustomStore.
  const choices: Row[] = [];
  for (let page = 1; ; page++) {
    const result = await request<{ items: Row[]; totalCount: number }>('POST', '/messages/grid',
      'Load accessible messages; match the selected workflow by ID.',
      { ...(config.messageId ? {} : { status: 'New' }), page, pageSize: 20 },
      value => ({ totalCount: value.totalCount, items: value.items.map(row => ({
        id: row.id, externalId: row.externalId, state: row.state,
        workflowDefinitionId: row.workflowDefinitionId, currentAssigneeId: row.currentAssigneeId,
      })) }));
    choices.push(...result.items.filter(row => sameId(row.workflowDefinitionId, workflow.id)
      && (!config.messageId || sameId(row.id, config.messageId))));
    if ((config.messageId && choices.length) || result.items.length === 0 || page * 20 >= result.totalCount) break;
  }
  if (!choices.length) throw new Error('No matching accessible message. Use a fresh test message or check user/workflow/message settings.');
  io.log(`\nMatching messages:\n${choices.map(row => `${row.id}: ${row.externalId} (${row.state})`).join('\n')}`);
  const selectedId = config.messageId || (await io.question('Message ID to process (q to stop): ')).trim();
  const row = choices.find(item => sameId(item.id, selectedId));
  if (!row) throw new Error('No message selected. Nothing was changed.');
  const path = `/messages/${encodeURIComponent(String(row.id))}`;
  const read = () => request<Message>('GET', path, 'Read the current state, capability, level and version.');
  let message = await read();
  const confirmed = new Set<number>();

  while (message.state !== 'Completed') {
    if (message.confirmationMode !== 'Immediate') throw new Error('Workflow mode changed; reload the demo.');
    if (!['New', 'Assigned', 'WaitingForSecondReview', 'WaitingForThirdReview'].includes(message.state)) {
      throw new Error(`Cannot confirm from state ${message.state}.`);
    }
    if (message.currentAssigneeId === null) {
      const candidates = await request<{ id: Id; displayName: string }[]>('GET', `${path}/assignment-candidates`,
        'Check that the current user is eligible for self-assignment in this scope.');
      if (!candidates.some(candidate => sameId(candidate.id, me.userId))) {
        throw new Error('You are not an assignment candidate. Check AllowSelfAssignment, processing scope and permissions.');
      }
      await request('POST', `${path}/assign`, 'Assign the message to yourself using me.userId.', { assignedTo: me.userId });
      message = await read();
      if (!sameId(message.currentAssigneeId, me.userId)) throw new Error('Assignment changed. Reload before continuing.');
    } else if (!sameId(message.currentAssigneeId, me.userId)) {
      throw new Error('This message belongs to another assignee. Choose another message; this demo does not reassign others’ work.');
    }
    const level = message.confirmationLevel;
    if (!message.canConfirm || level === null || !requiredLevels.includes(level) || !message.version) {
      throw new Error('Backend does not allow confirmation at this step. Check scope, level permissions and workflow policy.');
    }
    if (confirmed.has(level)) throw new Error('A previously confirmed level reopened. Stop and inspect the audit.');
    const previousState = message.state;
    const previousVersion = message.version;
    await request('POST', `${path}/reviews/confirm`,
      'Confirm using confirmationLevel and version from the latest GET (never increment the level locally).',
      { level, expectedVersion: previousVersion, comment: config.comment || null });
    confirmed.add(level);
    message = await read();
    io.log(`\nState: ${previousState} → ${message.state}`);
    if (message.version === previousVersion) throw new Error('Version did not change after confirmation. Inspect the saved result.');
  }
  if (message.currentAssigneeId !== null || message.canConfirm) throw new Error('Unexpected completion state. Inspect the message.');
  await request('GET', `${path}/view`, 'Inspect approvals and the full audit history (requires audit.view).');
  await request('GET', '/messages/state-counts', 'Refresh the counters after the saved changes.');
  io.log(`\nCompleted: ${workflow.code}, message ${row.id}, user ${me.userName}. No /complete call is required.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const terminal = createInterface({ input: process.stdin, output: process.stdout });
  try {
    if (CONFIG.pause && !process.stdin.isTTY) throw new Error('Use an interactive terminal, or DEMO_PAUSE=false with DEMO_MESSAGE_ID.');
    console.log('This walkthrough changes the selected message. Use a development test message.');
    await runWorkflow(CONFIG, { log: console.log, question: prompt => terminal.question(prompt) });
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  } finally {
    terminal.close();
  }
}
