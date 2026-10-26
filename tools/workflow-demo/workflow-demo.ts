import { createInterface } from 'node:readline/promises';

// Change these settings here, or pass environment variables when starting Node.
const API_URL = process.env.DEMO_API_URL ?? 'http://localhost:5080/api';
const USER = process.env.DEMO_USER ?? 'dc.reviewer1'; // Username or user ID.
const WORKFLOW = process.env.DEMO_WORKFLOW ?? 'SINGLE_CONFIRMATION'; // Code, name or ID.
const MESSAGE_ID = process.env.DEMO_MESSAGE_ID ?? ''; // Empty: choose from the list.
const COMMENT = process.env.DEMO_COMMENT ?? 'Looks good';
const PAUSE = process.env.DEMO_PAUSE !== 'false';
const PAGE = Number(process.env.DEMO_PAGE ?? 1);
const PAGE_SIZE = Number(process.env.DEMO_PAGE_SIZE ?? 20);
const STATUS = process.env.DEMO_STATUS ?? ''; // Empty: all states, including assigned messages.
const ACTIONS = process.env.DEMO_ACTIONS ?? ''; // Empty: interactive commands.

const terminal = createInterface({ input: process.stdin, output: process.stdout });
let user = USER;

// All HTTP calls go through this function so we can print them and pause.
async function request(method: string, path: string, body?: object) {
  console.log(`\n→ ${method} ${API_URL}${path} (user: ${user})`);
  if (body) console.log(JSON.stringify(body, null, 2));
  if (PAUSE) await terminal.question('Press Enter to send (Ctrl+C to stop): ');

  const response = await fetch(API_URL + path, {
    method,
    headers: { 'X-Debug-User': user, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  console.log(`← HTTP ${response.status}`);
  if (!response.ok) throw new Error(await response.text());
  if (response.status === 204) return;

  const data = await response.json();
  console.log(JSON.stringify(data, null, 2));
  return data;
}

function positiveNumber(value: string) {
  const number = Number(value);
  if (!Number.isSafeInteger(number) || number <= 0) throw new Error('Enter a positive integer.');
  return number;
}

async function main() {
  let me = await request('GET', '/me');
  const workflows = await request('GET', '/workflows');
  const workflow = workflows.find((w: any) =>
    w.code === WORKFLOW || w.name === WORKFLOW || String(w.id) === WORKFLOW);
  if (!workflow) throw new Error('Workflow not found. Check WORKFLOW.');

  const grid = await request('POST', '/messages/grid', {
    ...(STATUS ? { status: STATUS } : {}), page: PAGE, pageSize: PAGE_SIZE,
  });
  const messages = grid.items.filter((m: any) => m.workflowDefinitionId === workflow.id);
  console.table(messages.map((m: any) => ({ id: m.id, reference: m.externalId, state: m.state })));
  const messageId = MESSAGE_ID || await terminal.question('Enter a message ID from this list: ');
  const selected = messages.find((m: any) => String(m.id) === messageId.trim());
  if (!selected) throw new Error('Message not found on this page. Change PAGE, PAGE_SIZE or STATUS.');
  const path = `/messages/${selected.id}`;
  let message = await request('GET', `${path}/view`);
  let review: { reviewId: number; level: number } | undefined;
  const actions = ACTIONS.split(',').map(a => a.trim()).filter(Boolean);

  console.log(`
Commands:
  confirm          Confirm the available level (automatically assigns yourself).
  confirm:1        Send a specific level, e.g. to check a duplicate or forbidden action.
  complete         Confirm all available levels; stop if another reviewer is needed.
  candidates       Show eligible assignees for the current level.
  assign           Assign the current level to yourself.
  assign:21        Assign the current level to another user ID.
  user:username    Switch caller and read /me and /view again.
  start            Start/resume a ReviewSession and remember its reviewId.
  approve          Approve the session started by this script.
  reject           Reject that session, if allowed by the workflow.
  cancel           Cancel that session.
  refresh          Read /view again.
  quit             Stop.
`);

  while (true) {
    const input = ACTIONS ? actions.shift() : await terminal.question('Action: ');
    if (input === undefined || input.trim() === 'quit') break;
    const [action, argument] = input.trim().split(':');
    try {
      if (action === 'user') {
        if (!argument) throw new Error('Use user:username or user:ID.');
        user = argument;
        message = undefined; // Never use another caller's capabilities after a switch.
        review = undefined;
        me = undefined;
        me = await request('GET', '/me');
      } else if (action === 'refresh') {
        // The common read below refreshes the view.
      } else {
        if (!message || !me) throw new Error('Select a user who can read this message first.');
        if (action === 'confirm' || action === 'complete') {
          do {
            if (action === 'complete' && message.approvals.length > 0 &&
                message.approvals.every((a: any) => a.status === 'approved')) break;
            const level = argument ? positiveNumber(argument) : message.confirmationLevel;
            if (!argument && (!message.canConfirm || level == null))
              throw new Error('Confirmation unavailable. Check permissions, assignment and reviewer reuse.');
            await request('POST', `${path}/reviews/confirm`, { level, comment: COMMENT });
            message = await request('GET', `${path}/view`);
          } while (action === 'complete');
        } else if (action === 'candidates') {
          await request('GET', `${path}/assignment-candidates`);
        } else if (action === 'assign') {
          await request('POST', `${path}/assign`, { assignedTo: argument ? positiveNumber(argument) : me.userId });
          review = undefined;
        } else if (action === 'start') {
          const level = message.approvals.find((a: any) => a.status === 'pending')?.level;
          if (!message.canReview || level == null) throw new Error('Review session unavailable.');
          const result = await request('POST', `${path}/reviews/start`, { level });
          review = { reviewId: result.reviewId, level };
        } else if (['approve', 'reject', 'cancel'].includes(action)) {
          if (!review) throw new Error('Run start first to obtain the reviewId.');
          await request('POST', `${path}/reviews/${action}`, {
            ...review, ...(action === 'cancel' ? {} : { comment: COMMENT }),
          });
          review = undefined;
        } else {
          throw new Error('Unknown action. Use one of the commands above.');
        }
      }
      if (action !== 'confirm' && action !== 'complete')
        message = await request('GET', `${path}/view`);
      console.log(`Status: ${message.status}; canConfirm: ${message.canConfirm}; level: ${message.confirmationLevel}; canReview: ${message.canReview}`);
    } catch (error) {
      // Scripted scenarios stop on any error. Interactive mode lets you choose another action.
      // No mutation is retried automatically, even after an uncertain network result.
      if (ACTIONS) throw error;
      console.error((error as Error).message);
      message = undefined;
      review = undefined;
      console.log('Use refresh or user:username before the next action.');
    }
  }
  await request('GET', '/messages/state-counts');
  console.log(`\nFinished: message ${selected.id}.`);
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
}).finally(() => terminal.close());
