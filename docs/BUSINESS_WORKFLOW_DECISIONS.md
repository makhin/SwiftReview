# SWIFT review workflow decisions

This document records the assignment and review rules confirmed on 8 September 2026.

- An authorised user is a user with `message.assign` and access to the message's branch and department; no role name is hard-coded.
- Assignment is manual. The system does not automatically assign the first or subsequent review levels.
- Self-assignment is prohibited. Only eligible reviewers are offered: they must be able to view the message, access its scope, hold the required review-level permission, and must not have approved an earlier level.
- Review levels start at level 1 and proceed in ascending order. A configured optional second review may be skipped for applicable Data Control message types.
- Only the current assignee may start a review. Only the user who started it may approve or reject it.
- Reassignment is allowed before a review starts and prohibited while a review is active.
- Opening the review action starts the review before displaying its content or enabling decisions. Closing the window leaves the review active and reassignment locked; the assignee can reopen it to continue. Read-only viewing does not start a review.
- The active reviewer may explicitly use **Cancel review**, with the review-level permission in the message's scope. This returns level 1 to Assigned, level 2 to WaitingForSecondReview and level 3 to WaitingForThirdReview. The assignment stays open, but reassignment becomes available again. The cancelled attempt remains in history with status Cancelled and a ReviewCancelled audit event; previous approvals are unchanged. A new attempt at this level is allowed. Approved or rejected reviews cannot be cancelled.
- If a cancellation response is lost, the UI checks the message state and does not automatically start another review. If that check also fails, the user must close the window and refresh the grid to see the outcome.
- Retrying start for the same active reviewer and level returns the existing review, without duplicating the review or its audit event. Permissions and ownership are checked on every request.
- After a failed decision request, the UI checks the current message state before allowing another decision. If the review has ended, the user is directed to the refreshed grid and audit trail; if the state cannot be verified, the user must resume the review first.
- Message grids and the administrator user grid provide a manual Refresh action that preserves the current grid filters, sorting and page. Refreshing users does not discard unsaved access edits.
- Every approval or rejection closes the current assignment. A message requiring another level remains unassigned until an authorised user assigns the next reviewer. Completed and rejected messages have no current assignee.
- Rejection is final in the current implementation; reopening rejected messages is not supported.
- Undoing an approval closes any next-level assignment and clears the assignee in the same transaction. The reopened level requires a new manual assignment with eligibility checked for that level.

Availability, workload balancing, fallback pools, escalation/SLA rules, and the Data Control message types and conditions that permit skipping level 2 remain subject to business confirmation.
