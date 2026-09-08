# ORP — speaker notes

Audience: leadership and future users. Suggested duration: 12–15 minutes plus discussion.

Screenshots show the current English interface with synthetic data.

## 01 — SWIFT reviews. Clear ownership.

Today I will show how Operations Reporting and Processing, or ORP, supports the review of SWIFT messages. The central idea is simple: each message has a visible stage, an accountable reviewer when assigned, and a record of the decisions made. For leadership, this creates a clearer operating process. For users, it brings the queue and review actions together. We will follow a message through the application, look at the controls behind that journey, and finish with the decisions needed for a pilot. The screens use synthetic demonstration data. This presentation describes the current implementation, rather than a claim of production readiness.

## 02 — One workflow. Three practical benefits.

There are three practical benefits. First, teams can identify the current stage and the person assigned to a message. Second, reviewers have a personal work queue, so they can find their assigned messages and take the permitted next action. Third, authorised users can follow the history of assignments and decisions. These are capabilities supported by the current application. We should validate the resulting time savings and operating improvements in a pilot; there are no measured productivity or cost-saving figures behind this presentation. The objective is to make the process easier to manage and use, with evidence of what happened when questions arise.

## 03 — Every message follows a visible path

Messages arrive through the configured source integration and are registered against a review workflow. An authorised person then assigns an eligible reviewer. The reviewer reads the message and records a decision. When another review level is required, the message waits for a new manual assignment to a different eligible reviewer. Once every required level is approved, the review is complete. A rejection stops the review workflow and is currently final. In this product, completed means that the configured review has finished; it should not be described as execution or release of a payment. This distinction keeps the business promise aligned with the implemented scope.

## 04 — A shared queue for the team

This is the current message workspace, using demonstration data. The table brings together message type, organisational context, current stage, received date, amount and assignee. Users can filter the available messages and inspect the actions offered for each row. Visibility and actions depend on their permissions and organisational scope. A personal My work view is available for reviewers, together with a My departments view. The current interface is in English. For a live walkthrough, start with the team queue, point out a waiting message and its assignee, then switch to a reviewer’s personal queue. The numbers visible here are synthetic examples, not business performance figures.

## 05 — Ownership is explicit at each review stage

Assignment is a deliberate business action, not automatic workload distribution. An authorised assigner chooses a reviewer from the candidates the application permits for this message. Eligibility combines organisational access, permission for the required review level, and independence from earlier approvals on the same message. Users cannot assign a message to themselves. Responsibility can be transferred before a review starts, while reassignment during an active review is blocked. Approval clears the assignment; if another stage is required, an authorised person assigns the next reviewer. These controls make ownership explicit, but staffing cover and the person responsible for monitoring unassigned work still need to be agreed for the pilot.

## 06 — Match review depth to the process

Not every message needs the same review depth. ORP supports workflows with one, two or three required levels. Each approval moves the message to the next required level, or completes the review if no level remains. In a multi-level workflow, a user who approved an earlier level cannot perform a later one on the same message. This is the four-eyes principle in practical terms: later approval requires another eligible person. These are configured workflows; this presentation does not claim a business-user workflow designer exists. The specific message types and conditions that allow optional levels to be skipped remain a business decision to confirm before pilot configuration.

## 07 — Review the message. Record the decision.

For the reviewer, the everyday journey is short. Open My work, select a message assigned to you, and choose Review. The review window shows the message content and an optional comment field. An authorised reviewer can approve or reject. The application verifies ownership and the required stage before accepting the action. Approval either completes the review or sends it to the waiting state for the next required level. Rejection is final in the current version, so training should make that consequence clear. An API capability also exists to withdraw the reviewer’s own latest approval under defined conditions; there is no demonstrated Undo button in this screen, so do not promise a user-facing undo journey during the demo.

## 08 — A decision comes with a traceable history

When a question arises, the message’s audit history provides the sequence of recorded actions. It includes registration, assignment, review start, approval or rejection, and related changes in workflow state. Events carry the actor and timestamp, with comments or assignment details when applicable. This supports investigation and operational handover without relying only on someone’s recollection. Access to audit history is permission-controlled. The implementation preserves audit entries through its persistence boundary, but this should not be presented as a separately certified immutable archive. Retention, external audit requirements and operating controls need to be confirmed with the organisation before production use.

## 09 — One workspace. A shared review record.

At a high level, the architecture has five parts. The existing SWIFT source is read by a scheduled integration component. That component registers new messages in the application’s shared database. The application service reads and updates that same store while enforcing workflow and access rules. Users work in the browser. This is a modular application, rather than a fleet of independent microservices. The underlying technologies are React for the interface, .NET for the service, and SQL Server for persistent storage. Repeated imports are designed to avoid registering duplicate source messages and to preserve existing review progress. For deployment, the approved source connector, corporate sign-in, routing configuration and operational arrangements must be completed and verified.

## 10 — A working workflow, with a clear path to pilot

The repository demonstrates message queues, manual assignment, multi-level review, organisational access rules and audit history. These are the functions you have seen today. A pilot in the organisation’s environment still needs preparation. The current sign-in is a development mechanism, so corporate authentication must be integrated. The source integration requires the approved library and the organisation’s routing rules. We also need agreed roles, review-level mappings and a process for handling unassigned or rejected messages. Finally, deployment, recovery and usability should be exercised with the people who will operate the system. Automatic assignment, workload balancing and SLA escalation are not demonstrated capabilities of the current version.

## 11 — Use a focused pilot to measure the difference

I suggest a focused pilot with a named business owner, a representative user group and a bounded set of message types. First agree the scope and the current baseline. Then configure the environment, access and workflow rules. Next, exercise normal work and exceptions: reassignment before review, a further review level, rejection and investigation through audit history. Evaluate three areas: time to first assignment, end-to-end review turnaround and task completion with user feedback. Compare like-for-like message types and review depths; a three-stage process should not be judged against a one-stage baseline. These are proposed pilot measures, not an existing analytics dashboard. Targets and dates should be agreed by the sponsor and operational team.

## 12 — The next step: agree a focused pilot

The next decision is to sponsor a focused pilot. We need a named business owner and a representative group of future users. We need to agree the departments, message types, workflow depth and exception cases to include. And we need success criteria and a review date, so the pilot ends with a concrete decision. Leadership can help define ownership and the level of evidence needed to proceed. Future users can show us where the process is clear, where it slows them down and which exceptions need more attention. My suggested closing question is: which team and message scope should we use to prove the workflow first?
