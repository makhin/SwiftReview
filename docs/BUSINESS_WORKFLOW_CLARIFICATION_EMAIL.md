# Business workflow clarification email

**Subject:** Clarification needed: review sequence and reviewer assignment rules

Hi team,

We are reviewing the workflow rules for assigning and approving SWIFT messages and would like to confirm the expected business behaviour.

Could you please clarify the following points?

1. **Review level sequence**

   Must every workflow start with review level 1 and proceed in ascending order: level 1, then level 2, then level 3?

2. **Optional review levels**

   Can an optional level be skipped? For example, is a workflow with required levels 1 and 3, while level 2 is optional, valid?

3. **Initial assignment model**

   How should an unassigned message receive its first reviewer?

   - Does the system assign it automatically?
   - Does a user select an unassigned message and assign it to themselves?
   - Does an administrator or team leader assign it?
   - Should more than one of these options be supported?

4. **Automatic assignment pool**

   If assignment is automatic, which users should be included in the eligible pool?

   Please confirm whether selection should consider:

   - the user's role and permission for the required review level;
   - access to the message's branch and department;
   - whether the user is active and available;
   - the user's current workload;
   - whether the user has already reviewed or approved the message;
   - any team, location, shift, or substitute-reviewer rules.

   Please also clarify how the system should choose between equally eligible users and what should happen when no eligible reviewer is available.

5. **Assignment after approval**

   When another review level is required, should the system automatically assign the message to the next reviewer, leave it unassigned for self-selection, or wait for an administrator to assign it?

6. **Manual reassignment and the four-eyes rule**

   If a user approved an earlier level, should the system prevent that user from being manually assigned to a later review level? They would otherwise be unable to perform the review because of the four-eyes rule.

   Should this restriction apply only to earlier approvals, or also to users whose earlier review was rejected or undone?

7. **Who may assign and reassign messages?**

   Should these actions be available only to administrators and team leaders, or may reviewers assign messages to themselves or transfer them to another reviewer?

8. **Reassignment during an active review**

   If a message is reassigned while a review is already in progress, should the active review remain with the user who started it, be transferred to the new assignee, or must reassignment be prohibited until the active review is completed?

9. **Workload and availability**

   How should reviewer workload be calculated: all assigned messages, only active reviews, or weighted by review level or message complexity? How should absence, shifts, temporary unavailability, and workload limits affect eligibility?

10. **Fallback assignment**

    If no eligible reviewer is available in the message's branch or department, should the message remain unassigned and be escalated, or may the system use a reviewer from another approved pool?

11. **Self-assignment and prioritisation**

    If reviewers may select work themselves, which unassigned messages should they see, how should those messages be prioritised, and what should happen if two users try to claim the same message at the same time?

12. **Rejected messages**

    Is rejection final, or should a rejected message be corrected and returned to the review process? If it can be reopened, who may do so and from which review level should processing continue?

13. **Undo and administrative overrides**

    Who may undo an approval, within what time period, and under what conditions? Should an administrator be able to override assignment or review restrictions, and how should such an override be approved and audited?

14. **Workflow configuration changes**

    When a workflow is changed or deactivated, should messages already registered under that workflow continue with the original configuration or adopt the new one?

15. **Escalation and service levels**

    Are there deadlines for assignment or review? If a message remains unassigned or overdue, who should be notified and when should it be escalated?

The current documentation and implementation contain some of these behaviours, but we would like to confirm the intended business rules before treating them as mandatory constraints.

Examples of valid workflows and assignment scenarios would also be very helpful.

Thank you.
