# Operations Reporting and Processing — Frontend

The frontend is the React and TypeScript client for Operations Reporting and Processing. It uses Vite, DevExtreme, TanStack Query for non-grid server state, and a custom SMBC ThemeBuilder theme.

The implementation and UI conventions are documented in [`docs/DESIGN_GUIDE.md`](docs/DESIGN_GUIDE.md).

## Prerequisites

- Node.js 20.19 or any supported newer LTS release (22.12+ or 24+).
- npm.
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
VITE_DEBUG_USER=supervisor
```

The development server proxies `/api` requests to `VITE_API_PROXY_TARGET` and sends `VITE_DEBUG_USER` in the backend's `X-Debug-User` header.

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
npm run api:generate   # Regenerate types from the backend OpenAPI document
npm run theme:build    # Regenerate the DevExtreme theme
```

## Routes

- `/` — redirects to `/messages`.
- `/messages` — SWIFT messages grid backed by `GET /api/messages/grid`.
- `/me` — current debug-user details loaded through the typed API client.

All routes render inside `RootLayout`, which provides the global header and responsive DevExtreme navigation.

## Source structure

- `src/app` — application composition, providers, routing, and the root layout.
- `src/pages` — route-level slices and their page-specific API code.
- `src/shared/api` — generated OpenAPI types, the shared HTTP client, and reference-data access.
- `src/shared/hooks`, `src/shared/lib`, and `src/shared/types` — proven cross-page abstractions.
- `src/theme` and `src/styles` — design tokens, generated DevExtreme theme files, and application styles.

Dependencies flow from `app` to `pages` to `shared`; page code must not import from `app`. Tests are colocated with the code they cover.

## API access

Run the backend before regenerating the OpenAPI types:

```bash
npm run api:generate
```

This command reads <http://localhost:5080/openapi/v1.json> and updates `src/shared/api/schema.d.ts`. Use the typed client in `src/shared/api/client.ts` for ordinary API requests.

The messages grid is intentionally different: it uses a DevExtreme `CustomStore` backed by the server's `DevExtreme.AspNet.Data` endpoint. Do not route grid load operations through TanStack Query.

## Styling and theme generation

`src/theme/tokens.css` is the source of truth for colours, typography, spacing, radii, shadows, and motion. Global styles are loaded in this order:

1. Local fonts, application tokens, and shared patterns.
2. Generated DevExtreme theme.
3. SMBC DevExtreme overrides.
4. DevExtreme visualisation palette.
5. Minimal application-root CSS.

Run the theme build after changing mapped design tokens or ThemeBuilder settings:

```bash
npm run theme:build
```

The command synchronizes ThemeBuilder metadata and regenerates `src/theme/dx.smbc.css`. Keep both generated files under source control and do not edit the generated CSS manually.

Myriad Pro is the default interface typeface. Capitolium 2 is reserved for explicit display headings. Fonts, the SMBC logo, and the favicon are bundled locally.

## Verification

```bash
npm run lint
npm run typecheck
npm test
npm run build
```
