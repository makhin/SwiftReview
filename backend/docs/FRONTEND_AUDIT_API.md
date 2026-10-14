# Message audit response guide

Use `GET /api/messages/{id}/audit?skip=0&take=50` to read the history of one message. Replace `{id}` with its internal message ID. The request requires authentication, the `audit.view` permission, and access to that message.

`skip` is the number of events to skip, starting at `0`. `take` is the maximum number to return (`1`–`500`; server default `100`). There is no request body.

## Response structure

A successful response is HTTP `200` with an object containing `items` and `totalCount`:

```json
{
  "items": [
    {
      "id": 901,
      "eventType": "MessageAssigned",
      "timestamp": "2026-10-14T08:30:00+00:00",
      "oldState": "New",
      "newState": "Assigned",
      "actor": {
        "userId": 1,
        "userName": "admin",
        "displayName": "Administrator"
      },
      "details": {
        "previousAssigneeId": null,
        "assigneeId": 7
      },
      "correlationId": "example-assignment-request-001"
    }
  ],
  "totalCount": 123
}
```

This shortened example means: **Administrator assigned the message to user 7 at 08:30 UTC, changing its state from New to Assigned.** Event `901` identifies this action. The response contains one event, while `totalCount: 123` means the message has 123 events in total. Other optional detail fields are omitted from this example; actual responses can include them as `null`.

| Field | Meaning |
| --- | --- |
| `items` | Events in this page. Loop over this array to display the history. |
| `totalCount` | Total events for this message before pagination, not the number in this page. |

`{"items":[],"totalCount":0}` means no history. An empty `items` array with a positive total can mean your `skip` is beyond the available events.

## Read each event

| Field | Type | Interpretation |
| --- | --- | --- |
| `id` | Integer | Unique event ID. Use it to identify rows and remove duplicates. It is not the message ID. |
| `eventType` | String | What happened. Use the labels below. |
| `timestamp` | Date-time string with offset | When it happened. Parse with `new Date(value)` and format for the user's locale. Do not manually add a timezone offset. |
| `oldState` | String or `null` | Message state before the action. |
| `newState` | String or `null` | Message state after the action. |
| `actor` | Object or `null` | Who performed the action. Contains `userId`, `userName`, and `displayName`. Use “System” when no actor is available. |
| `details` | Object | Additional information about this action. It is already an object; do not call `JSON.parse` on it. |
| `correlationId` | String | Diagnostic reference for the operation that created the event. Several events can share it; do not use it as a unique row ID. |

**Actor and assignee are different:** `actor` performed the action; `details.assigneeId` received the assignment.

Display a state transition as `New → Assigned`. If both states are `null`, hide the transition. If one side is `null`, display `—` on that side. States can remain the same for an action that does not change the message's stage.

State codes are `New`, `Assigned`, `FirstReviewInProgress`, `WaitingForSecondReview`, `SecondReviewInProgress`, `WaitingForThirdReview`, `ThirdReviewInProgress`, `Completed`, and `Rejected`. Display readable labels when available, with the raw code as a fallback.

## Interpret optional details

All fields below can be `null`. Handle missing fields the same way: hide the corresponding row. A missing value does not mean zero or an empty user.

| Field | Meaning when present |
| --- | --- |
| `workflowDefinitionId` | Associated workflow, such as the new workflow after a change. |
| `previousWorkflowDefinitionId` | Workflow before the change. |
| `assigneeId` | New assignee's user ID. |
| `previousAssigneeId` | Previous assignee's user ID. |
| `reviewId` | Review associated with the event. |
| `reviewLevel` | Review level, normally `1`, `2`, or `3`. |
| `comment` | Comment associated with the action. Display as plain text. |
| `branchId` / `previousBranchId` | Branch associated with the change / previous branch. |
| `departmentId` / `previousDepartmentId` | Department associated with the change / previous department. |

Except for `comment`, these values are integers. IDs are references, not names. Resolve them using separately loaded reference data, or show a fallback such as `User #7`. Do not assume every event populates every field.

## Interpret event types

Match codes exactly, including capitalization. For an unknown future code, display the code instead of failing.

| `eventType` | Display label | Interpretation |
| --- | --- | --- |
| `MessageRegistered` | Message registered | The message entered the system. |
| `MessageAssigned` | Message assigned | An unassigned message received an assignee. |
| `MessageReassigned` | Message reassigned | The assignee changed. |
| `MessageUnassigned` | Message unassigned | The assignment was removed. |
| `ReviewStarted` | Review started | A review began. |
| `ReviewApproved` | Review approved | A review was approved. Further reviews may still be required. |
| `MessageCompleted` | Message completed | The message reached completion. |
| `ReviewRejected` | Review rejected | A review was rejected. |
| `ConfirmationUndone` | Confirmation undone | An earlier confirmation was undone. |
| `ReviewCancelled` | Review cancelled | A review was cancelled. |
| `MessageWorkflowChanged` | Workflow changed | The message's workflow changed. |

One user action can produce several events. In particular, approval and message completion are separate event types. Use each event's state fields to show its recorded transition rather than guessing the state from its label.

## Example details for each event type

These are shortened `details` objects, not complete events. IDs and comments are illustrative. Fields omitted here can still appear as `null` in the response. Comments are optional; their presence is not guaranteed by the event type.

| `eventType` | Example `details` |
| --- | --- |
| `MessageRegistered` | `{"workflowDefinitionId":3}` |
| `MessageAssigned` | `{"previousAssigneeId":null,"assigneeId":7}` |
| `MessageReassigned` | `{"previousAssigneeId":7,"assigneeId":12}` |
| `MessageUnassigned` | `{"previousAssigneeId":12,"assigneeId":null}` |
| `ReviewStarted` | `{"reviewId":501,"reviewLevel":1,"comment":null}` |
| `ReviewApproved` | `{"reviewId":501,"reviewLevel":1,"comment":"Checked and approved."}` |
| `MessageCompleted` | `{"reviewId":502,"reviewLevel":2,"comment":"Final review approved."}` |
| `ReviewRejected` | `{"reviewId":501,"reviewLevel":1,"comment":"Incorrect beneficiary details."}` |
| `ConfirmationUndone` | `{"reviewId":501,"reviewLevel":1,"comment":"Needs another check."}` |
| `ReviewCancelled` | `{"reviewId":502,"reviewLevel":2,"comment":null}` |
| `MessageWorkflowChanged` | `{"previousWorkflowDefinitionId":3,"workflowDefinitionId":5}` |

- `MessageRegistered` shows the registration shape used by the supplied SQL seed. Registration details depend on the process that created the record; do not require `workflowDefinitionId` to be present.
- `MessageCompleted` refers to the review whose approval completed the message. It carries that review's ID, level, and approval comment.
- `ConfirmationUndone` refers to the approval being undone. If undo also cancels a later review, a separate `ReviewCancelled` event refers to that later review.
- The current assignment and review actions do not populate branch or department details. Display those fields only when they are actually present in a stored event.

## Format an event with TypeScript

This standalone example uses only the fields needed to produce display text. The complete response includes the additional fields described above.

```ts
interface AuditEvent {
  id: number;
  eventType: string;
  timestamp: string;
  oldState: string | null;
  newState: string | null;
  actor: {
    userId: number;
    userName: string;
    displayName: string;
  } | null;
  details: {
    assigneeId?: number | null;
    comment?: string | null;
  };
}

interface AuditPage {
  items: AuditEvent[];
  totalCount: number;
}

function formatEvent(event: AuditEvent): string[] {
  const lines = [
    event.eventType, // Replace with a label from the table above.
    new Date(event.timestamp).toLocaleString(),
    event.actor?.displayName || event.actor?.userName || 'System',
  ];

  if (event.oldState !== null || event.newState !== null) {
    lines.push(`${event.oldState ?? '—'} → ${event.newState ?? '—'}`);
  }
  if (event.details.assigneeId != null) {
    lines.push(`Assignee: User #${event.details.assigneeId}`);
  }
  if (event.details.comment) {
    lines.push(`Comment: ${event.details.comment}`);
  }
  return lines;
}
```

Display the resulting strings as text, for example with `textContent`. A TypeScript interface does not validate incoming JSON at runtime.

The server sends integer IDs as JSON numbers; message, event, and review IDs can be 64-bit values. This example assumes they fit within JavaScript's safe integer range. If IDs can exceed `Number.MAX_SAFE_INTEGER`, use an agreed lossless parsing or string serialization approach. Converting a rounded number to text cannot restore its original value.

## Preserve order and load more

Events arrive **newest first**, sorted by `timestamp` descending and then `id` descending. Preserve that order.

For a page size of 50, request offsets `0`, `50`, `100`, and so on. Advance the offset by the number of events actually returned. Stop when the accumulated count reaches `totalCount`, or when a page is empty.

New events can appear between requests and shift offsets. Remove duplicate displayed events by `id`, but calculate the next offset from raw page lengths, including duplicates. Refresh from `skip=0` after an action changes history. Keep histories separate when switching messages or users.

## Distinguish errors from empty history

| Status | Meaning | UI behavior |
| --- | --- | --- |
| `400` | Invalid request parameters. | Correct the request. |
| `401` | Missing or invalid authentication. | Handle authentication. |
| `403` | Audit access denied. | Show an access-denied message. |
| `404` | Message missing or outside the user's access scope. | Show “Message not found or unavailable.” |
| `500` | Unexpected server error. | Show an error and allow retry. |

Handled errors use Problem Details JSON with fields such as `status`, `title`, and `detail`. Check the HTTP status even if the body is empty or cannot be parsed. With `fetch`, check `response.ok`: HTTP errors do not automatically throw.

Never turn a failed request into an empty history. If loading a later page fails, retain the events already displayed and let the user retry that page.
