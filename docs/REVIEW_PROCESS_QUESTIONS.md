# Review process: questions for BA and PO

Could you please clarify the intended approval process for a message that requires two reviews?

## 1. Does the order of reviewers matter?

If reviewers A and B both need to approve a message, are these two sequences equally valid?

```text
A approves → B approves → Message approved
B approves → A approves → Message approved
```

Or must a particular reviewer or reviewer role always approve first?

## 2. Should the two review steps require different permissions?

For example, should we have separate groups:

- **Step 1:** only users A, B, and C can approve.
- **Step 2:** only users X, Y, and Z can approve.

Or should any authorized reviewer be able to approve either step, as long as **two different people** approve the message?

If permissions are separate, can a user have permission for both steps, while still being allowed to approve only one step per message?

## 3. Should there be an exception for a Senior Reviewer?

Should a Senior Reviewer be allowed to:

- Approve **either step**, but still require another person to approve the other step; or
- Approve **both steps of the same message**, without a second person?

```text
Option A: Senior Reviewer → Another reviewer → Message approved
Option B: Senior Reviewer → Same Senior Reviewer → Message approved
```

If Option B is allowed, should the Senior Reviewer perform two separate approvals, or should a single approval complete the entire review process?
