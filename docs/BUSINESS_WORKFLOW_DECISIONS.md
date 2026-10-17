# SWIFT review workflow decisions

The routing and workflow policies are described in [Workflow configuration](WORKFLOW_CONFIGURATION.md).
Messages can belong to multiple departments. Visibility and processing rights are
checked within the same branch and department scope; departments marked view-only
do not grant processing rights.

Workflows are reusable and selected by the routing matrix. Their policies determine
whether confirmations are immediate, use review sessions, or are disabled; whether
an assignee is required; whether reviewers may participate again; when assignments
end; whether rejection is allowed; and which approvals may be undone.

Immediate confirmation submits the displayed SQL rowversion and performs start and
approval atomically. A conflict requires reload and another explicit user action.
Review sessions retain their explicit start, approve, reject and cancel operations.
Read-only viewing does not start a session. View-only messages cannot be changed.

The frontend follows server-authorized capabilities for each row. It does not infer
processing rights by merging permissions from different departments. Both server-backed
grids retain DevExtreme load operations and manual refresh.

## Business lifecycle and audit wording

The business labels shown in message grids and dashboard counts are `Unassigned`
for `New`, `Pending 1st Confirmation` for `Assigned` and
`FirstReviewInProgress`, `Pending 2nd Confirmation` for
`WaitingForSecondReview` and `SecondReviewInProgress`, and `Completed` for
`Completed`. `ViewOnly` has its own card and filter. `Rejected` and third-review
states remain available in the technical Stage filter and contribute to Total.
The routing matrix and configured workflow steps determine the number of levels;
the frontend does not hard-code department-specific limits.

The requested "what happened" names describe existing audit events; stored event
codes do not change:

| Business name | Stored audit event |
| --- | --- |
| MessageAvailable | `MessageRegistered` |
| MessageAssigned | `MessageAssigned` |
| MessageReassigned | `MessageReassigned` |
| First/SecondConfirmationStarted | `ReviewStarted`, distinguished by `details.reviewLevel` |
| First/SecondConfirmationCompleted | `ReviewApproved`, distinguished by `details.reviewLevel` |
| FirstConfirmationUndone | `ConfirmationUndone` with `details.reviewLevel = 1` |
| MessageCompleted | `MessageCompleted` |
| MessageRejected | `ReviewRejected` |

`ReviewCancelled`, `MessageUnassigned` and `MessageWorkflowChanged` remain in
history when those actions occur.

### Changing a message workflow

The assignments page is available with `message.assign` or `workflow.manage`. Changing a workflow requires `workflow.manage` in the message's branch and department, with message visibility; global administrators bypass permission checks. Assignment itself still requires `message.assign`.

The Change workflow action displays the workflow returned by the server routing
resolver, and PUT revalidates it when applied. It is allowed before review or after
all review attempts have been cancelled or undone. Active, approved or rejected
attempts prevent a change, including for administrators. History records the
previous and new workflow IDs and actor; routing also updates processing departments.

### Full review queue

The single review queue lists all messages within the user's `message.view` scope,
including completed and rejected messages, so their content and audit remain
reachable after decisions. Department and personal tabs are removed. Only actions
currently permitted by message state, scoped level rights and ownership expose a
Review button. View remains read-only. Pending reviews actionable by the assigned
current user additionally show Ready for review.

Review-level row colours apply to all messages at that stage, independently of the
viewer, permissions or assignment. A separate Traditional Green leading stripe and
Assigned to you badge identify the current user's assignments without changing the
stage colour. Ready for review remains a personal action cue. Completed, rejected
and new messages have no review-level fill.
