# Access administration

`/admin` (Users & access) is available only to global administrators. The API
also enforces this restriction on every `/api/admin` endpoint.

## Access model

- Business permissions are granted only through roles. A user has no direct grants.
- Each assignment is `(user, branch, department, role)`. Multiple roles may be
  assigned to the same exact branch/department pair; their permissions are combined
  only within that pair. Separate branch and department lists are not access grants.
- Viewing a message requires `message.view` in its pair. Assigning, auditing and
  reviewing additionally require the corresponding permission in the same pair.
- `review.level1`, `review.level2` and `review.level3` each allow both approval
  and rejection at that level, only by the active review's owner. There is no
  separate global rejection permission.
- `Users.IsGlobalAdministrator` controls access administration independently of
  business roles. It neither grants message access nor bypasses review ownership.
  Set this flag through trusted provisioning/seed data; no UI or API changes it.

## Screens and changes

The Users tab searches existing users and edits their scoped role assignments.
Removing all scopes removes business access. The Roles tab edits permission sets
of existing roles; changes apply to every assignment of that role. Neither screen
creates users or changes global administrator status.

The original initial migration creates the fixed permission catalog and five
starting business roles (CS Reviewer, TFO Reviewer, DC Reviewer, DC Senior Reviewer,
Operations manager). Their permissions can subsequently be edited in the UI.
Users, branches, departments and global administrators remain provisioning data.
Mock mode supplies demo reference data and an `admin` identity with explicit
business assignments; that demo access is not an implicit administrator bypass.

Each saved user/role change appends an `AccessAuditEvents` record containing the
actor, target, timestamp, before/after state and correlation ID in the same database
transaction. Changes that remove the access needed to complete an active review
are rejected with HTTP 409. Finish the review before revoking that access.
Review start and administrative changes use serializable database transactions
to coordinate permission reads with review creation. No row-version field is used.

The initial migration was rewritten, not extended with an upgrade migration.
It is intended for a new database, not one with the previous schema already applied.
Sync is outside this feature's scope.
