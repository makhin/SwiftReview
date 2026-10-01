# Permissions and roles in ORP

## Access model

Ordinary users receive permissions through roles assigned to an exact **branch and department pair**. Each stored assignment is `(userId, branchId, departmentId, roleId)`. Permissions from multiple roles are combined within that pair.

For example, assignments to London/CS and Dublin/TFO grant access to those two pairs only. They do not grant access to London/TFO or Dublin/CS. There are no wildcard scopes, direct user permissions, role inheritance or explicit deny rules.

A role assignment grants access; a message assignment makes a user the current assignee of a particular message. These are separate operations.

## Permission catalog

The catalog is fixed in application code. Administrators can change which permissions existing roles contain.

| Permission | Purpose |
|---|---|
| `message.view` | View messages in the assigned scope. |
| `message.assign` | Assign or reassign a message. |
| `review.level1` | Start, approve, reject or cancel a level 1 review. |
| `review.level2` | Perform the same actions at level 2. |
| `review.level3` | Perform the same actions at level 3. |
| `review.undo` | Undo an approval under the workflow policy. |
| `audit.view` | Read a message's audit history. |
| `workflow.manage` | Change a message's workflow. |

For ordinary users, message actions require `message.view` and the action permission in the message's exact scope. Approval, rejection and cancellation share the permission for their review level. Access administration requires global administrator status.

## Initial business roles

The initial migration creates these defaults. Their permission sets can subsequently be edited.

| Role | Initial permissions |
|---|---|
| `CS Reviewer` | `message.view`, `review.level1`, `audit.view` |
| `TFO Reviewer` | `message.view`, `review.level1`, `review.level2`, `audit.view` |
| `DC Reviewer` | `message.view`, `review.level1`, `audit.view` |
| `DC Senior Reviewer` | `message.view`, `review.level2`, `review.level3`, `review.undo`, `audit.view` |
| `Operations manager` | All eight permissions. |

Role names do not enforce department membership or review levels. The assignment defines the scope, and permissions define allowed actions. `Operations manager` remains a scoped business role and does not grant access administration or administrator overrides.

The demo seed uses a different catalog: `Reviewer` has all permissions except `workflow.manage`, and `Operations manager` has all eight. The seed replaces application data and is intended for test databases, not for updating access in an existing business database.

## Global administrators

`Users.IsGlobalAdministrator` is a separate user flag set through trusted provisioning. A global administrator can manage access, see every scope, assign messages to themselves and act on another user's review. Permission, scope, review ownership and reviewer reuse checks are bypassed, even with no role assignments.

Domain rules still apply, including valid workflow levels, state transitions and current review IDs. Undo has specific administrator exceptions described below. The current UI and API do not create users, create or rename roles, or change administrator status.

## Assigning roles to users

As a global administrator, open **Users & access** at `/admin`:

1. On **Users**, find the existing user and select **Edit access**.
2. Select **Add scope**, then choose **Branch**, **Department** and one or more **Roles**.
3. Check **Effective permissions** and add any other required scopes.
4. Select **Save access**.

Use one row per branch/department pair. Remove a role from its row to revoke it; use **Remove scope** when no roles should remain in that pair. Removing all scopes removes ordinary business access, subject to active review protection.

To change a role, open **Roles**, select the role, edit **Role permissions** and select **Save permissions**. The change affects every user assigned that role, across all its scopes.

## Action rules

The server checks current rights and business state independently of frontend buttons.

| Action | Additional rules for ordinary users |
|---|---|
| Assign a message | The state must allow assignment. The recipient needs visibility and the required review level in the same scope, must differ from the assigning user and current assignee, and must not have already approved a level of that message. |
| Start a review | The user must be the message's current assignee, have the required level permission and be allowed to review the current stage. |
| Approve, reject or cancel | The user must own the active review and supply its current `reviewId`. |
| Review a later level | A user who already approved a level of the message cannot review another level under the four eyes rule. |
| Change workflow | The selected workflow must be accessible and match the message direction. All existing attempts must be `Cancelled` or `Undone`, or no attempts must exist. |

Assignment requires `review.level1` for recipients of `New` or `Assigned` messages, `review.level2` for `WaitingForSecondReview`, and `review.level3` for `WaitingForThirdReview`. Other states do not permit assignment through this mechanism. Global administrator recipients bypass ordinary eligibility restrictions, but assignment to the current assignee remains invalid.

The assignments page `/messages` requires `message.assign` or `workflow.manage`; the review queue `/messages/assigned` requires `message.view`. The queue includes visible messages throughout their lifecycle.

### Undo policy

Ordinary users need scoped `message.view`, `review.undo` and access to the message's workflow. Undo also follows three workflow settings:

| Setting | Values |
|---|---|
| `UndoApprovalMode` | `Disabled`, `LatestNonFinal`, `Latest` |
| `UndoActorMode` | `OriginalReviewer`, `AnyAuthorizedUser` |
| `UndoActiveReviewMode` | `Block`, `Cancel` |

`LatestNonFinal` allows the latest approval of a required step except the final required step. `Latest` includes the final approval. `OriginalReviewer` limits Undo to its original approver; `Cancel` permits cancelling the active next review in the same operation. Rejected messages cannot be reopened through Undo.

Without an active review, a global administrator may undo the latest approval, including the final one, even if ordinary Undo is disabled. With an active review, the workflow's approval and cancellation policy applies; scope and original reviewer restrictions are bypassed.

## API and JSON examples

All `/api/admin` endpoints require global administrator status.

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/api/me` | Current identity, summary permissions and exact scopes. |
| `GET` | `/api/admin/catalog` | Roles, their current permissions, permission catalog, branches and departments. |
| `GET` | `/api/admin/users/grid` | Search, sort and page through users. |
| `GET` | `/api/admin/users/{id}/access` | User assignments and effective scoped permissions. |
| `PUT` | `/api/admin/users/{id}/access` | Replace all role assignments for a user. |
| `PUT` | `/api/admin/roles/{id}/permissions` | Replace the complete permission set of a role. |

### User assignment request

Body for `PUT /api/admin/users/101/access`:

```json
{
  "assignments": [
    {"branchId": 1, "departmentId": 10, "roleIds": [1]},
    {"branchId": 2, "departmentId": 20, "roleIds": [2]}
  ]
}
```

This replaces all existing assignments. Read the current assignments first and send the complete desired result. `{"assignments": []}` removes them all.

### Role permission request

Body for `PUT /api/admin/roles/2/permissions`:

```json
{
  "permissions": [
    "message.view",
    "review.level1",
    "review.level2",
    "audit.view"
  ]
}
```

Permissions omitted from this request are removed from the role. An empty permission set is allowed unless active review protection blocks it.

### Descriptive model structure

This example connects users, roles, stored assignments and effective scopes. It is an explanatory model, not a single API payload or an import file.

```json
{
  "user": {
    "id": 101,
    "userName": "reviewer.example",
    "displayName": "Example Reviewer",
    "isGlobalAdministrator": false
  },
  "roles": [
    {
      "id": 1,
      "name": "CS Reviewer",
      "permissions": ["message.view", "review.level1", "audit.view"]
    },
    {
      "id": 2,
      "name": "TFO Reviewer",
      "permissions": ["message.view", "review.level1", "review.level2", "audit.view"]
    }
  ],
  "userRoles": [
    {"userId": 101, "branchId": 1, "departmentId": 10, "roleId": 1},
    {"userId": 101, "branchId": 2, "departmentId": 20, "roleId": 2}
  ],
  "scopes": [
    {
      "branchId": 1,
      "departmentId": 10,
      "roleIds": [1],
      "permissions": ["audit.view", "message.view", "review.level1"]
    },
    {
      "branchId": 2,
      "departmentId": 20,
      "roleIds": [2],
      "permissions": ["audit.view", "message.view", "review.level1", "review.level2"]
    }
  ]
}
```

`scopes` contains computed permissions for each exact pair. In `/api/me`, the top-level `permissions` combines all scopes for navigation; `branches` and `departments` are summaries. Message authorization must use the exact scope. Administrators receive the full summary permission catalog even when `scopes` is empty.

### Validation and responses

User updates allow up to 200 unique scopes, each with 1–50 unique role IDs. IDs must be positive and reference existing records. Role updates accept unique permissions from the fixed eight-item catalog. Request collections cannot be `null`, and unknown JSON fields are rejected.

Successful updates return `204 No Content`. Errors use Problem Details: `400` for invalid input, `401` for missing or failed authentication, `403` for denied access, `404` for missing resources and `409` for business or concurrency conflicts.

## Revocation and audit

An access change is rejected with `409` if it would remove `message.view` or the required review level from an ordinary user's active review scope. Other roles in that same scope can preserve the required rights. Finish or cancel the active review before revoking its required access. This protection applies to `InProgress` reviews, not every message assignment; administrator reviews are exempt.

Each saved user or role change writes an `AccessAuditEvents` entry with actor, target user or role, timestamp, before and after JSON, and correlation ID. Access updates and audit records are committed together. Review start and access changes use `Serializable` transactions.

The server checks updated rights on subsequent requests. The frontend refreshes related data after access changes and polls current user data every 30 seconds; changes are not pushed instantly to every open client.

## Authentication and implementation references

The current API registers Development-only Debug authentication. `X-Debug-User` selects an existing user by ID or login; identity and administrator status are read from the database. No alternative production authentication scheme is currently registered, and corporate identity synchronization requires separate integration.

For implementation details, see the [identity model](../backend/src/ORP.Domain/Identity/IdentityEntities.cs), [administration service](../backend/src/ORP.Infrastructure/Identity/UserAdministrationService.cs), [message authorization](../backend/src/ORP.Application/Authorization/MessageAuthorizationService.cs) and [access administration reference](ACCESS_ADMINISTRATION.md).
