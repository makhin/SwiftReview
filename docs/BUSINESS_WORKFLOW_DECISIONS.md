# SWIFT review workflow decisions

This document records the assignment and review rules confirmed on 8 September 2026.

- An authorised user is a user with `message.assign` and access to the message's branch and department; no role name is hard-coded.
- Assignment is manual. The system does not automatically assign the first or subsequent review levels.
- Self-assignment is prohibited. Only eligible reviewers are offered: they must be able to view the message, access its scope, hold the required review-level permission, and must not have approved an earlier level.
- Review levels start at level 1 and proceed in ascending order. A configured optional second review may be skipped for applicable Data Control message types.
- Only the current assignee may start a review. Only the user who started it may approve or reject it.
- Reassignment is allowed before a review starts and prohibited while a review is active.
- Every approval or rejection closes the current assignment. A message requiring another level remains unassigned until an authorised user assigns the next reviewer. Completed and rejected messages have no current assignee.
- Rejection is final in the current implementation; reopening rejected messages is not supported.
- Undoing an approval closes any next-level assignment and clears the assignee in the same transaction. The reopened level requires a new manual assignment with eligibility checked for that level.

Availability, workload balancing, fallback pools, escalation/SLA rules, and the Data Control message types and conditions that permit skipping level 2 remain subject to business confirmation.
