# Operations Reporting and Processing — Frontend

The frontend is the React and TypeScript client for Operations Reporting and Processing. It uses Vite, the shared `@smbc/ui` and `@smbc/devextreme-theme` packages, Tailwind, and TanStack Query for non-grid server state. DevExtreme is the implementation behind the shared components.

The implementation and UI conventions are documented in [`docs/DESIGN_GUIDE.md`](docs/DESIGN_GUIDE.md).

## Prerequisites

- Node.js 22.12+ or 24+ (CI uses Node.js 24).
- npm.
- .NET SDK 10.0.302 for contract generation.
- The backend running at <http://localhost:5080> for live API data and OpenAPI generation.

## Run locally

Install the locked dependencies and start Vite:

```bash
npm ci
npm run dev
```

Open the URL printed by Vite, normally <http://localhost:5173>.

Vite reads environment settings from the repository root. These optional variables can be placed in the root `.env` file:

```dotenv
VITE_API_PROXY_TARGET=http://localhost:5080
VITE_DEBUG_USER=admin
```

The development server proxies `/api` requests to `VITE_API_PROXY_TARGET`. In Development,
the frontend sends the current debug user in the backend's `X-Debug-User` header. Select a
user by ID or username in the browser URL, for example:

```text
http://localhost:5173/messages?user=5
http://localhost:5173/messages?user=admin
```

The selection is kept for navigation in the current browser tab. Without a URL selection,
the frontend uses `VITE_DEBUG_USER`, falling back to `admin`.

## Available commands

```bash
npm run dev            # Start the development server
npm run build          # Type-check and create a production build
npm run preview        # Preview the production build locally
npm run lint           # Run ESLint
npm run typecheck      # Run the TypeScript compiler
npm test               # Run the test suite once
npm run test:coverage  # Run tests with coverage thresholds
npm run test:watch     # Run tests in watch mode
npm run api:generate   # Regenerate DTO contracts from the backend OpenAPI document
npm run api:check      # Check that generated DTO contracts are current
npm run test:smbc      # Build and exercise real SMBC controls in Chromium
npm run refresh:smbc -- /path/to/smbc  # Check, pack and update the shared libraries
npm run test:refresh-smbc  # Test same-version archive replacement and clean installation
```

## Routes

- `/` — redirects to `/messages`.
- `/messages` — administrator-only grid of all accessible SWIFT messages, backed by
  `GET /api/messages/grid`.
- `/messages/assigned?scope=mine|departments` — messages assigned to the current
  user, active reviews owned by that user, or messages assigned within the current user's departments.
- `/me` — current debug-user details loaded through the typed API client.

All routes render inside `RootLayout`, which provides the global header and responsive DevExtreme navigation.

## Source structure

- `src/app` — application composition, providers, routing, and the root layout.
- `src/pages` — route-level slices and their page-specific API code.
- `src/shared/api` — generated DTO contracts, the shared HTTP client, current-user queries, and reference-data access.
- `src/shared/hooks`, `src/shared/lib`, and `src/shared/types` — proven cross-page abstractions.
- `src/styles` — the layered CSS entry, document baseline and scoped application interaction styles.
- `vendor` — versioned, built archives of the two SMBC packages.

Dependencies flow from `app` to `pages` to `shared`. ESLint checks static imports
and re-exports, including types: shared code must not import pages or app code;
page code must not import app code or another page slice. Imports within one slice
are allowed. Both message routes belong to the same `pages/messages` slice.
New page directories are included automatically in these checks. Update the ESLint
import prefix if module aliases are introduced. Tests are colocated with the code they cover.

Within `src/pages/messages`, the two route components stay at the slice root.
`api/` owns message and audit requests, audit query options, and the grid's
`CustomStore`; `model/` owns review rules and state-card presentation helpers.
`components/` groups the grid, stage indicator, and audit drawer with their local
styles and tests. Related message-action dialogs and buttons share
`components/message-actions/`, including their common popup styles.

The optional `features` layer is reserved for business actions that need independent
reuse across page slices. It is not used yet; keep current message workflows in
`pages/messages` until that need appears.

## API access

Run the backend before regenerating the OpenAPI contracts:

```bash
npm run api:generate
```

This command reads <http://localhost:5080/openapi/v1.json>, runs the repository's .NET 10
`OpenApiTsContracts` tool, and updates
`src/shared/api/generated/contracts.generated.ts`. The generated file contains data contracts
only; `src/shared/api/client.ts` is the handwritten HTTP wrapper. Use `npm run api:check` in CI
to verify the checked-in contracts without modifying them.

The messages grid is intentionally different: it uses a DevExtreme `CustomStore` backed by the server's `DevExtreme.AspNet.Data` endpoint. Do not route grid load operations through TanStack Query.

All endpoints use `apiRequest<T>`: pass the URL, method, JSON body, optional abort signal,
and a fallback error message. JSON results are returned directly; commands explicitly use
`responseType: 'none'` to accept empty successful responses (including 204).
Missing or malformed required JSON is an error, not a successful `undefined` value.
The generic result type is a TypeScript contract, not runtime schema validation.

- `ApiError` means an HTTP error response was received. It preserves the HTTP status
  and Problem Details (including validation extensions); detail/title supply the message.
- `ApiRequestError` distinguishes network failures, cancellation, and invalid responses.
  `outcomeUnknown` is true when a dispatched mutation has no usable confirmation.
  Cancellation does not roll back a server operation. Check current state before retrying;
  review decisions retain their state-reconciliation flow.

The transport never retries requests. An HTTP error (especially 5xx) is not a guarantee
that the server rolled back an operation. A pre-aborted signal prevents dispatch.

## Shared UI packages and styling

`vendor/` contains the built SMBC archives referenced by `package.json` and
`package-lock.json`. A fresh clone needs only `npm ci`; it does not need a
checkout of the library sources or a private registry. Keep the two archives,
manifest and lockfile together when updating or rolling back a release.

To develop the libraries, check out `devextreme-theme` and `smbc-ui` below one
`smbc` directory and install Chromium once (`npx playwright install chromium`).
From `frontend`, run:

```bash
npm run refresh:smbc -- /path/to/smbc
# Windows example:
npm run refresh:smbc -- C:/Projects/smbc
```

The command installs and checks the theme, packs it, refreshes the UI's local
theme dependency, checks and packs UI, then copies both archives here and
refreshes their installed files and lockfile integrity. A failure stops the
sequence. Before installing rebuilt archives, it removes only their old lockfile
entries and installed package directories, and invalidates npm's hidden tree
cache. This also applies to the theme installed in `smbc-ui`, so reusing a
package version and archive filename cannot silently retain an older build.
Review library source changes separately and commit the resulting
archives and dependency files with the consumer changes. Normal frontend
builds never regenerate the theme. No absolute source paths are saved in
SwiftReview dependencies. If a package version changes, remove its superseded
archive from `vendor` after the update.

`src/styles/app.css` is the sole style entry, with layers
`theme, base, vendor, components, utilities`. It loads the theme and UI CSS,
uses the theme's semantic Tailwind bridge, and scans only application source.
There is no Preflight or scan of `node_modules`. Tokens, fonts, logo and favicon
come from the theme package; common controls and semantic primitives come from
UI. Keep only complex local interactions and scoped vendor contexts in CSS.

See [the UI guide](docs/DESIGN_GUIDE.md) for API boundaries and ownership.

## Verification

```bash
npm run lint
npm run typecheck
npm test
npm run build
npx playwright install chromium  # first browser setup
npm run test:smbc
```

Browser checks use controlled HTTP fixtures and the production build, without SQL or a running API. Screenshots and request evidence are written to ignored `artifacts/smbc/`. Live SQL integration remains a backend responsibility.
