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

const terminal = createInterface({ input: process.stdin, output: process.stdout });

// All HTTP calls go through this function so we can print them and pause.
async function request(method: string, path: string, body?: object) {
  console.log(`\n→ ${method} ${API_URL}${path}`);
  if (body) console.log(JSON.stringify(body, null, 2));
  if (PAUSE) await terminal.question('Press Enter to send (Ctrl+C to stop): ');

  const response = await fetch(API_URL + path, {
    method,
    headers: { 'X-Debug-User': USER, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  console.log(`← HTTP ${response.status}`);

  // Stop on an error. Never automatically retry an approval.
  if (!response.ok) throw new Error(await response.text());
  if (response.status === 204) return; // Successful command, no JSON body.

  const data = await response.json();
  console.log(JSON.stringify(data, null, 2));
  return data;
}

async function main() {
  // 1. Get our user ID and find the workflow selected in the settings.
  const me = await request('GET', '/me');
  const workflows = await request('GET', '/workflows');
  const workflow = workflows.find((w: any) =>
    w.code === WORKFLOW || w.name === WORKFLOW || String(w.id) === WORKFLOW);
  if (!workflow) throw new Error('Workflow not found. Check WORKFLOW.');
  if (workflow.confirmationMode !== 'Immediate') throw new Error('Choose an Immediate workflow for this demo.');

  // 2. Load one page of new messages and show those belonging to our workflow.
  const grid = await request('POST', '/messages/grid', {
    status: 'New', page: PAGE, pageSize: PAGE_SIZE,
  });
  const messages = grid.items.filter((m: any) => m.workflowDefinitionId === workflow.id);
  console.table(messages.map((m: any) => ({ id: m.id, reference: m.externalId })));

  const messageId = MESSAGE_ID || await terminal.question('Enter a message ID from this list: ');
  const selected = messages.find((m: any) => String(m.id) === messageId.trim());
  if (!selected) throw new Error('Message not found on this page. Change PAGE or PAGE_SIZE.');
  const path = `/messages/${selected.id}`;
  let message = await request('GET', path);

  while (message.state !== 'Completed') {
    // 3. Show eligible reviewers and assign ourselves if assignment is needed.
    if (message.currentAssigneeId === null) {
      await request('GET', `${path}/assignment-candidates`);
      await request('POST', `${path}/assign`, { assignedTo: me.userId });
      message = await request('GET', path);
    }

    // 4. The backend tells us the current level. Do not calculate them here.
    if (!message.canConfirm) throw new Error('Confirmation is unavailable. See the last message response.');
    await request('POST', `${path}/reviews/confirm`, {
      level: message.confirmationLevel,
      comment: COMMENT,
    });

    // 5. Read the new state and available action before the next confirmation.
    const previousState = message.state;
    message = await request('GET', path);
    console.log(`State: ${previousState} → ${message.state}`);
  }

  // 6. Show approvals, audit history and updated counters.
  await request('GET', `${path}/view`);
  await request('GET', '/messages/state-counts');
  console.log(`\nCompleted: message ${selected.id}, workflow ${workflow.code}, user ${me.userName}.`);
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
}).finally(() => terminal.close());
