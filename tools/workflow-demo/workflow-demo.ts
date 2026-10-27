import { createInterface } from 'node:readline/promises';

// This example assumes valid settings and commands. The API checks permissions.
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
let currentUser = USER;

// All HTTP calls go through this function so we can print them and pause.
async function request(method: string, path: string, body?: object) {
  console.log(`\n→ ${method} ${API_URL}${path} (user: ${currentUser})`);
  if (body) console.log(JSON.stringify(body, null, 2));
  if (PAUSE) await terminal.question('Press Enter to send (Ctrl+C to stop): ');

  const response = await fetch(API_URL + path, {
    method,
    headers: { 'X-Debug-User': currentUser, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  console.log(`← HTTP ${response.status}`);
  // Any HTTP error ends the demo. No automatic retries.
  if (!response.ok) {
    throw new Error(await response.text());
  }
  if (response.status === 204) return; // Success without a JSON response.

  const data = await response.json();
  console.log(JSON.stringify(data, null, 2));
  return data;
}

async function main() {
  // 1. Find the current user and the workflow selected in the settings above.
  let currentUserDetails = await request('GET', '/me');
  const workflows = await request('GET', '/workflows');
  const workflow = workflows.find((workflow: any) => {
    return workflow.code === WORKFLOW ||
      workflow.name === WORKFLOW ||
      String(workflow.id) === WORKFLOW;
  });

  // 2. Load one page of messages. Keep only messages belonging to this workflow.
  const gridFilter: { page: number; pageSize: number; status?: string } = {
    page: PAGE,
    pageSize: PAGE_SIZE,
  };
  if (STATUS) {
    gridFilter.status = STATUS;
  }
  const grid = await request('POST', '/messages/grid', gridFilter);
  const messages = grid.items.filter((message: any) => message.workflowDefinitionId === workflow.id);
  console.table(messages.map((message: any) => ({
    id: message.id,
    reference: message.externalId,
    state: message.state,
  })));

  // 3. Choose a message from that page and read its state and available actions.
  let messageId = MESSAGE_ID;
  if (!messageId) {
    messageId = await terminal.question('Enter a message ID from this list: ');
  }
  const messagePath = `/messages/${messageId.trim()}`;
  let message = await request('GET', `${messagePath}/view`);

  // Only /reviews/start gives us the reviewId needed by approve/reject/cancel.
  let activeReview: { reviewId: number; level: number };

  // Both confirm and complete use this same pair of requests: confirm, then read.
  async function confirmMessage(level: number) {
    await request('POST', `${messagePath}/reviews/confirm`, {
      level,
      comment: COMMENT,
    });
    message = await request('GET', `${messagePath}/view`);
  }

  // 4. Read one command, run its requests, then show the resulting state.
  // DEMO_ACTIONS supplies commands for an automated run. Otherwise ask the user.
  const scriptedMode = ACTIONS !== '';
  const pendingCommands = ACTIONS.split(',');
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
    let input: string | undefined;
    if (scriptedMode) {
      input = pendingCommands.shift();
    } else {
      input = await terminal.question('Action: ');
    }
    if (input === undefined || input.trim() === 'quit') break;

    // Example: "assign:21" becomes command = "assign", argument = "21".
    const [command, argument] = input.trim().split(':');
    switch (command) {
      case 'confirm': {
        // Confirm once. With no argument, use the level returned by /view.
        if (argument) {
          await confirmMessage(Number(argument));
        } else {
          await confirmMessage(message.confirmationLevel);
        }
        break;
      }

      case 'complete': {
        // The server tells us whether this user can confirm another level.
        while (message.canConfirm) {
          await confirmMessage(message.confirmationLevel);
        }
        break;
      }

      case 'candidates': {
        await request('GET', `${messagePath}/assignment-candidates`);
        message = await request('GET', `${messagePath}/view`);
        break;
      }

      case 'assign': {
        let assignedTo = currentUserDetails.userId;
        if (argument) {
          assignedTo = Number(argument);
        }
        await request('POST', `${messagePath}/assign`, { assignedTo });
        message = await request('GET', `${messagePath}/view`);
        break;
      }

      case 'user': {
        currentUser = argument;
        currentUserDetails = await request('GET', '/me');
        message = await request('GET', `${messagePath}/view`);
        break;
      }

      case 'start': {
        const pendingApproval = message.approvals.find((approval: any) => approval.status === 'pending');
        const level = pendingApproval.level;
        const startedReview = await request('POST', `${messagePath}/reviews/start`, { level });
        activeReview = { reviewId: startedReview.reviewId, level };
        message = await request('GET', `${messagePath}/view`);
        break;
      }

      case 'approve':
      case 'reject':
      case 'cancel': {
        const body: { reviewId: number; level: number; comment?: string } = {
          reviewId: activeReview.reviewId,
          level: activeReview.level,
        };
        if (command !== 'cancel') {
          body.comment = COMMENT;
        }
        await request('POST', `${messagePath}/reviews/${command}`, body);
        message = await request('GET', `${messagePath}/view`);
        break;
      }

      case 'refresh': {
        message = await request('GET', `${messagePath}/view`);
        break;
      }
    }

    console.log(`Status: ${message.status}; canConfirm: ${message.canConfirm}; level: ${message.confirmationLevel}; canReview: ${message.canReview}`);
  }

  // 5. Show updated counters and finish.
  await request('GET', '/messages/state-counts');
  console.log(`\nFinished: message ${messageId.trim()}.`);
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
}).finally(() => terminal.close());
