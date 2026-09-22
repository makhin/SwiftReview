# SwiftReview Architecture

This document explains the current architecture in practical terms. It is written for a junior developer who needs to understand where code belongs and how one user action travels through the system.

## 1. What the system does

SwiftReview, also called ORP (Operations Reporting and Processing), is a web application for reviewing SWIFT messages. Users can find messages, assign them to reviewers, complete one to three review stages, reject or undo decisions, and inspect an audit trail. Access is limited by permission, branch, and department.

The main runtime parts are:

```text
Browser
  React + TypeScript + DevExtreme
              |
              | JSON over HTTP (/api)
              v
ASP.NET Core Minimal API
              |
              v
Application use cases and Domain rules
              |
              v
Entity Framework Core -> SQL Server (schema: orp)
```

The backend targets .NET 10. The frontend is a React 19 single-page application built by Vite. SQL Server is the persistent store.

## 2. Repository map

| Path | Responsibility |
| --- | --- |
| `backend/src/ORP.Domain` | Business entities and rules. It does not know about HTTP or SQL. |
| `backend/src/ORP.Application` | Use-case handlers, validation, DTOs, authorization rules, and interfaces required from infrastructure. |
| `backend/src/ORP.Infrastructure` | EF Core, SQL queries, transactions, identity/access persistence, and implementations of Application interfaces. |
| `backend/src/ORP.Api` | HTTP endpoints, dependency injection, authentication, error responses, OpenAPI, health checks, and telemetry. |
| `backend/tests` | Domain, Application, SQL integration, and Scheduler tests. The SQL suite is local-only in this workspace. |
| `frontend/src/app` | Application startup, providers, routing, and global layout. |
| `frontend/src/pages` | Route-level feature slices such as messages and administration. |
| `frontend/src/shared` | Reusable API, authorization, routing, and UI code. |
| `tools/OpenApiTsContracts` | Generates frontend TypeScript DTOs from backend OpenAPI schemas. |
| `backend/src/ORP.Scheduler` | Separate .NET Framework 4.7.2 text-to-PDF library; it is not part of the web application runtime. |

The important backend dependency direction is:

```text
API ------> Application <------ Infrastructure
 |               |                    |
 +---------------+--------------------+
                 v
               Domain
```

Dependencies point toward business rules. For example, Application declares `IORPStore`; Infrastructure implements it with EF Core. This is dependency inversion: business code asks for an interface and does not depend directly on the database implementation.

## 3. Backend architecture

### Domain layer

The Domain layer contains the rules that must stay true regardless of whether a request comes from HTTP, a test, or another future interface.

The central aggregate is `Message`. It owns the current `MessageState`, assignee, and workflow ID. The Stateless library implements its state machine. Typical transitions are:

```text
New -> Assigned -> FirstReviewInProgress
                         |
                         +-> Completed                 (one-level workflow)
                         +-> WaitingForSecondReview    (multi-level workflow)
                         +-> Rejected

WaitingForSecondReview -> SecondReviewInProgress
WaitingForThirdReview  -> ThirdReviewInProgress -> Completed
```

The entity methods enforce rules rather than exposing public setters. For example, starting a review checks the active level, assignment ownership, and the four-eyes rule. The four-eyes rule prevents a normal user who already approved one level from approving another level of the same message. A global administrator has controlled overrides.

Other important domain objects are:

- `WorkflowDefinition` and `WorkflowStep`: define one to three ordered review levels.
- `Review`: records an attempt and its status (`InProgress`, `Approved`, `Rejected`, `Undone`, or `Cancelled`).
- `Assignment`: keeps assignment history; ending an assignment sets `EndedAt` instead of deleting it.
- `AuditEvent`: immutable business history with the actor, old/new state, details, and correlation ID.
- `User`, `Role`, `Permission`, `Branch`, and `Department`: model scoped access.

Invalid business operations throw `DomainRuleViolationException`. The API converts it to HTTP `409 Conflict`.

### Application layer

The Application layer implements one class per use case, for example `AssignMessageHandler`, `StartReviewHandler`, and `ApproveReviewHandler`. A handler normally performs these steps:

1. Start a database transaction through `ITransactionExecutor`.
2. Load the message and authorize the current user inside that transaction.
3. Validate the request with FluentValidation.
4. Call Domain methods to apply business rules.
5. Add assignment/review/audit records.
6. Save changes and commit.

Example, simplified from the approve flow:

```csharp
var access = await authorization.RequireAsync(messageId, permission, ct);
var review = RequireActiveReview(access.Reviews, request.ReviewId, request.Level);
access.Message.Approve(review, workflow, access.Reviews, user.UserId,
    request.Comment, clock.UtcNow, access.IsGlobalAdministrator);
store.AddAudit(...);
await store.SaveChangesAsync(ct);
```

Request and response records in `Application/Contracts` are also the public API DTOs. They are deliberately separate from EF entities, so database internals are not returned directly to the browser.

### Infrastructure layer

`ORPDbContext` maps Domain entities to SQL Server with EF Core. Configuration classes define table names, relationships, lengths, indexes, string enum storage, and delete behavior. Query services use projections and `AsNoTracking()` for read-only work, avoiding the cost and risk of loading editable entities.

Two persistence styles are used intentionally:

- Commands use `IORPStore` and tracked Domain entities because they change state.
- Queries project directly to DTO/read models because they only read data and should remain efficient.

Mutations run at `Serializable` isolation. `TransactionExecutor` reloads state and permissions on an EF retry, commits once, and does not replay a mutation if the commit result is unknown. Database constraints provide a final concurrency guard, such as one active assignment and one active review per message/level. Conflicts become HTTP `409` responses.

### API layer

`Program.cs` is the composition root. It registers Application and Infrastructure services, authentication, authorization, OpenAPI, error handling, health checks, and OpenTelemetry. Minimal API modules expose authenticated endpoints under `/api`:

- `/api/messages`: grid, details, assignment, workflow, audit, and counts.
- `/api/messages/{id}/reviews`: start, approve, reject, cancel, and undo.
- `/api/admin`: user access and role administration; global administrator only.
- `/api/me`, `/api/dashboard`, and reference-data endpoints.

Errors use RFC-style Problem Details:

| Status | Meaning |
| --- | --- |
| `400` | Invalid input or malformed grid options. |
| `401` | No valid authentication. |
| `403` | Authenticated user lacks access. |
| `404` | Requested resource was not found or cannot be returned. |
| `409` | Domain rule or concurrent update conflict. |
| `500` | Unexpected server failure; internal details are not exposed. |

Every request receives an `X-Correlation-ID`. The same value is placed in request logs and audit events, making one operation traceable across layers. `/health` checks the database, `/openapi/v1.json` publishes the contract, `/scalar` provides an interactive API reference, and OpenTelemetry can export traces and metrics through OTLP.

Authentication differs between Development and Production and is described in [section 5](#5-authentication-and-authorization).

## 4. Database design

All application tables use the `orp` schema. The main relationships are:

```text
SwiftMessages 1 --- 1 Messages --- * Assignments
                         |
                         +--------- * Reviews
                         +--------- * AuditEvents
                         |
                         * --- 1 WorkflowDefinitions --- * WorkflowSteps

Users --- * UserRoles * --- Roles --- * RolePermissions * --- Permissions
              |   |
              |   +--- Department
              +------- Branch
```

Key tables and implementation details:

| Table | Purpose |
| --- | --- |
| `SwiftMessages` | Source SWIFT content and routing metadata. `WarehouseId` is the unique external key. Application code treats these rows as read-only. |
| `Messages` | ORP workflow state, current assignee, and workflow reference. Its key is also the related `SwiftMessages.MessageId`. |
| `WorkflowDefinitions`, `WorkflowSteps` | Active workflow selected by message type, department, and optionally branch. |
| `Assignments` | Complete assignment history. A filtered unique index allows only one row with `EndedAt IS NULL` per message. |
| `Reviews` | Review attempts. A filtered unique index allows only one non-cancelled/non-undone attempt per message and level. |
| `AuditEvents` | Append-only message history. Updates and deletes are rejected by `ORPDbContext`. |
| `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions` | Role-based access. `UserRoles` includes branch and department, so permissions are scoped. |
| `AccessAuditEvents` | Append-only history of access-administration changes. |

Migrations live in `ORP.Infrastructure/Persistence/Migrations`. In Development they run at API startup only when `BootstrapDatabase=true`; other environments apply them explicitly.

The repository currently has no production SWIFT importer. An external integration must populate the source record, resolve a workflow, and create the related `Messages` row. The checked-in SQL seed is for development/testing, not production ingestion.

## 5. Authentication and authorization

Authentication answers “who is this user?” Authorization answers “may this user perform this action on this exact message?” These are separate steps: Microsoft Entra ID will authenticate the person in Production, while ORP remains responsible for business permissions and data scopes.

### Development: debug-user authentication (implemented)

Development deliberately avoids a dependency on Entra ID so developers and automated tests can switch between seeded users quickly:

```text
?user=admin or VITE_DEBUG_USER
        |
        v
frontend sessionStorage -> X-Debug-User header
        |
        v
DebugAuthenticationHandler -> Users table -> ASP.NET claims
        |
        v
HttpCurrentUser -> Application authorization
```

The browser user is selected in this order:

1. The `user` URL parameter, for example `/messages?user=5` or `/messages?user=admin`. The frontend remembers it in the current tab's `sessionStorage`.
2. The previously remembered value.
3. `VITE_DEBUG_USER` from frontend configuration.
4. The fallback value `admin`.

`shared/api/client.ts` adds that value to every API request as `X-Debug-User`. Grid requests also use this client, so they follow the same rule. Vite only proxies `/api` to the backend; it does not authenticate the request.

The API's `DebugAuthenticationHandler` first checks that the ASP.NET environment is `Development`. It accepts a numeric local user ID or a username, loads the user from SQL Server, and creates these claims:

- `NameIdentifier`: the local ORP user ID.
- `Name`: the ORP username.
- `display_name`: the display name.
- `global_admin=true`: added only for a global administrator.

`HttpCurrentUser` reads the verified claims and exposes them to Application handlers through `ICurrentUser`. All `/api` endpoints require an authenticated principal. An unknown or missing debug user therefore produces `401`; insufficient ORP access produces `403`.

The debug header is not a security credential. The handler rejects it outside Development, and a production deployment must never enable the Development environment to make this mechanism work.

### Production: Microsoft Entra ID (planned, not implemented yet)

Production will use an Entra tenant with a registered SPA client and protected API (either two app registrations or an equivalent client/API registration). The API exposes a delegated scope such as `access_as_user`, and only approved client applications may request it.

The intended request flow is:

```text
1. React redirects the user to Microsoft Entra ID.
2. Entra authenticates the user, including tenant policies such as MFA.
3. MSAL obtains an API access token with Authorization Code Flow + PKCE.
4. apiRequest sends: Authorization: Bearer <access-token>
5. ASP.NET validates the token before an endpoint runs.
6. The API maps the Entra identity to a local ORP user.
7. Existing ORP branch, department, role, and workflow checks run unchanged.
```

The frontend should use `@azure/msal-browser` and `@azure/msal-react`. It should acquire tokens silently when possible and start an interactive sign-in only when required. `apiRequest` will attach an **access token intended for the ORP API**, never an ID token. The SPA contains a public client ID and tenant/authority configuration but no client secret. MSAL owns token caching and renewal; application code must not store raw tokens in its own `localStorage` or `sessionStorage` entries. Microsoft recommends Authorization Code Flow with PKCE for SPAs rather than the older implicit flow.

The API should replace the Debug scheme outside Development with JWT bearer authentication, normally configured through `Microsoft.Identity.Web`. The middleware must validate the token's signature, issuer/tenant, audience, lifetime, and required delegated scope. The frontend cannot make a request trusted by merely sending a username or copying `X-Debug-User`.

ORP should identify a production user by the immutable Entra `oid` (object ID) together with `tid` (tenant ID), not by email, display name, or UPN because those values can change. This requires adding Entra object/tenant identifiers and a unique constraint to the local `Users` model. After token validation, the API resolves `(tid, oid)` to the local user ID used by `ICurrentUser`. A valid Entra user with no active ORP user record should receive `403`, not automatic business access.

Entra proves identity and grants permission to call the API; the existing ORP database remains the source of truth for application authorization:

- `UserRoles` continues to connect a user, role, branch, and department.
- `RolePermissions` continues to grant `message.view`, `message.assign`, review levels, audit, and workflow permissions.
- `IsGlobalAdministrator` remains an explicit ORP business privilege; an Entra tenant administrator is not automatically an ORP global administrator.
- Frontend permission checks only control presentation. The backend always repeats authorization using the authenticated local user.

For deployment, serving the SPA and API behind one HTTPS origin is the simplest model. If they use different origins, the API must use an explicit CORS allow-list; wildcard origins must not be combined with authenticated requests. Signing out should clear the MSAL session/cache and use the Entra logout flow.

Implementation references: [MSAL authentication flows](https://learn.microsoft.com/en-us/entra/identity-platform/msal-authentication-flows), [configure a protected ASP.NET Core API](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-app-configuration), and [validate Entra claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation).

### ORP authorization rules

A normal user needs both `message.view` and the action permission in the message's exact branch/department scope. Action permissions include `message.assign`, `review.level1` to `review.level3`, `audit.view`, and `workflow.manage`. Authorization also checks current state, assignee/reviewer ownership, and four-eyes separation. The backend is the security boundary; frontend permission checks only hide or disable UI controls.

A global administrator bypasses scoped permission checks and can use administration and undo operations. Sensitive authorization reads happen inside the same transaction as writes, which prevents a permission change racing with a business action.

## 6. Frontend architecture

`main.tsx` loads global styles and renders `App`. `AppProviders` supplies one TanStack Query client and preloads reference data. React Router renders lazy route components inside `RootLayout`.

Frontend dependencies flow in one direction:

```text
app -> pages -> shared
```

ESLint enforces this rule. `shared` cannot import a page or `app`; a page slice cannot import another page slice. Tests are colocated beside the code they cover.

### Server state and HTTP

`shared/api/client.ts` is the single low-level `fetch` wrapper. It serializes JSON, adds the development user header, parses Problem Details, and separates HTTP errors from network/cancellation/invalid-response errors. It intentionally does not retry mutations because their server outcome may be unknown.

TanStack Query manages non-grid server state such as the current user, reference data, message details, audit, and administration data. Query keys are centralized so mutations can invalidate all affected caches. Reference data is considered fresh for 30 minutes; current-user and message counts refresh every 30 seconds.

The DevExtreme grids are different. A `CustomStore` sends DevExtreme paging, sorting, filtering, grouping, and summary options directly to `/api/messages/grid`. Backend `DevExtreme.AspNet.Data` translates them into SQL. Grid loads must not use TanStack Query because DevExtreme owns this remote-load protocol.

Example grid request:

```text
GET /api/messages/grid?skip=0&take=20&sort=[{"selector":"receivedAt","desc":true}]
```

The server first applies authorization and assignment scope, projects allowed fields, and only then applies DevExtreme operations. This keeps large datasets and access filtering on the server.

### UI feature slices

`pages/messages` contains:

- `api/`: HTTP functions, Query hooks, mutations, audit calls, and grid `CustomStore`.
- `model/`: presentation rules and the review-session state machine.
- `components/`: the grid, review/assignment/workflow popups, stage indicator, and audit drawer.
- `MessagesPage.tsx` and `AssignedMessagesPage.tsx`: route-level composition.

The review popup uses a reducer-based client state machine. It keeps the returned `reviewId`, blocks duplicate actions, handles conflicts, and reloads server state when a mutation's outcome is uncertain. The database remains the source of truth.

Styles are split between design tokens, a generated DevExtreme theme, SMBC overrides, and page/component CSS. `tokens.css` is the source of truth; generated theme CSS is not edited manually.

## 7. End-to-end example: approving a message

1. The grid loads accessible rows from its `CustomStore`.
2. The user opens Review. The UI posts `{ "level": 1 }` to `/api/messages/42/reviews/start`.
3. The API calls `StartReviewHandler`. Inside a serializable transaction it checks scoped permission, assignment ownership, message state, and four-eyes rules.
4. `Message.StartReview` changes the state; EF inserts a `Review` and an `AuditEvent`; the transaction commits.
5. The API returns `{ "reviewId": 123 }`. The popup keeps that ID so a later decision targets the exact attempt.
6. Approve posts `{ "level": 1, "reviewId": 123, "comment": "Checked" }`.
7. `Message.Approve` marks the review approved and moves the message to the next waiting state or `Completed`. The active assignment ends, and audit rows are appended in the same transaction.
8. The frontend closes the popup, invalidates related Query data, and refreshes the DevExtreme grid.

## 8. Shared contracts and tests

Backend DTOs appear in OpenAPI. `tools/OpenApiTsContracts` converts the OpenAPI schemas into `frontend/src/shared/api/generated/contracts.generated.ts`. Only data types are generated; endpoint functions and hooks remain handwritten. Generated contracts must not be edited directly.

Testing follows the architectural boundaries:

- Domain tests exercise state transitions and entity rules without SQL Server.
- Application tests exercise handlers with test doubles.
- Local API integration tests use a real SQL Server and a fresh database per fixture.
- Frontend Vitest tests use jsdom and React Testing Library.
- The separate Scheduler suite tests text-to-PDF conversion on .NET Framework 4.7.2.

For the current Linux/Docker workspace, use `./test-backend.docker.sh -m:1` for backend changes. Frontend changes require `npm --prefix frontend run lint`, `typecheck`, `test`, and `build`. When an HTTP contract changes, regenerate it with `api:generate` and verify it with `api:check` while the API runs on port 5080.

## 9. Where to add new code

- A business invariant or state transition belongs in Domain.
- A new user action belongs in an Application handler, with an interface for external dependencies.
- SQL/EF implementation belongs in Infrastructure.
- HTTP mapping and transport-only concerns belong in API.
- Route-specific React code belongs in its `pages/<slice>` directory.
- Reusable, proven frontend code belongs in `shared`.
- A message grid load stays in a DevExtreme `CustomStore`; other server state normally uses TanStack Query.

Keeping these boundaries makes each rule testable and prevents UI, HTTP, and database details from leaking into the core business model.
