# SwiftReview UI guide

SwiftReview follows the `smbc-style` reference application. DevExtreme 26.1.4 is
an implementation dependency of `@smbc/ui`; application code consumes the two
SMBC packages instead of importing vendor controls, data types or utilities.

## Ownership and application boundaries

- `@smbc/devextreme-theme` owns canonical tokens, fonts, logo, favicon, generated
  theme, corporate vendor corrections and the semantic Tailwind bridge.
- `@smbc/ui` owns reusable controls, accessibility integration, primitives and
  its compiled `ui:` utility styles. Add missing reusable functionality there.
- Application JSX owns layout, responsive composition, routing, permissions,
  workflow presentation and data access. Use ordinary flex/grid utilities, not
  new Stack/Row/Grid abstractions or a parallel set of layout helper classes.
- Application CSS owns complex local interactions and geometry. Keep scoped
  vendor hooks in `src/styles/vendor-contexts.css`; place reusable corporate
  corrections in the theme and private component integration in UI.

`app` composes providers, layout and routes. `pages` contains independent feature
slices. `shared` cannot import pages or app composition; pages cannot import one
another. ESLint preserves these boundaries and prohibits direct imports of
`devextreme`, `devextreme-react` and their subpaths, including type imports.

## Style loading and tokens

`main.tsx` imports only `src/styles/app.css`. The entry declares:

```css
@layer theme, base, vendor, components, utilities;
@import "tailwindcss/theme.css" layer(theme);
@import "@smbc/devextreme-theme/styles.css" layer(vendor);
@import "@smbc/ui/styles.css" layer(components);
/* Local interaction CSS is also imported into components. */
@import "tailwindcss/utilities.css" layer(utilities) source(none);
@reference "@smbc/devextreme-theme/tailwind.css";
@source "..";
```

Use the official Tailwind v4 Vite plugin. Do not add Preflight, a stock vendor
theme, a second token scale, external font services or a UI node_modules scan.
UI's archive already contains its compiled styles. Never edit generated CSS or
installed package files. Rebuild and reinstall the archives after library changes.

Use semantic utilities such as `bg-surface`, `text-fg-muted`, `border-border`,
`bg-primary`, `font-body` and `font-brand`. Myriad Pro is the default; Capitolium
is reserved for explicit editorial/display headings. Operational headings stay
in the body family. Brand assets are imported from
`@smbc/devextreme-theme/assets`, including favicon setup in application startup.

Use standard `sm` 640px, `md` 768px, `lg` 1024px and `xl` 1280px breakpoints.
The shell's JavaScript media query matches the `md` mobile boundary. Shared
colours, radii, typography, spacing, focus and motion come from theme tokens.
One-off content geometry can remain local. Respect reduced-motion preferences.

## Controls, forms and primitives

Import controls from `@smbc/ui`. Buttons use string children, `variant`, `submit`
and `onClick()`; icon-only buttons require `ariaLabel`. Editors use controlled
`value` / `onChange(value)` or `defaultValue`, without switching modes. `Select`
and `MultiSelect` use `options`, `optionLabel`, `optionValue`, `searchable` and
`clearable`. Preserve nullable selection values and existing commit timing;
use `devExtremeProps.valueChangeEvent = 'input'` for intentional live search.

Use `Field` for label/help/error associations, `Card` and its sections for
surfaces, `TableShell` for grids, `Callout` for feedback, and `FilterPanel`,
`StatusBadge`, `KpiCard` and `EmptyState` where appropriate. Field owns editor
accessibility, while page JSX owns field placement. Do not inspect private
`smbc-ui-*` or `ui:` implementation classes from application CSS.

`devExtremeProps` provides typed advanced configuration. Do not duplicate
wrapper-owned props there, inject raw vendor widgets or bypass the shared API.
Import validation controls from `@smbc/ui/validation` when needed; each field has
one validation owner. Keep payload validation and mutation locks intact.

## Server tables and workflow behaviour

Import configuration components, `DataGridRef`, `CustomStore`, `LoadOptions`
and `LoadResultObject` through `@smbc/ui/data-grid`. Grid rows continue loading
through CustomStore and backend `DevExtreme.AspNet.Data`, including remote
paging, filtering and sorting. Never use TanStack Query for grid load operations.
TanStack Query owns reference data, detail requests, summaries and mutations.

DataGrid forwards its typed ref and retains `instance()` for refresh, filtering
and resetting paging. Keep lookup-ID sorting restrictions and stable keys.
Message-stage calculations and permissions remain feature-owned. Shared review
colour roles come from the theme; text and progress accompany colour cues.

`Dialog` uses `open` and `onClose`. Keep pending/uncertain writes locked and
preserve the existing review-session state machine, review IDs and recovery
behaviour. `ConfirmDialog` protects unsaved administration changes; ignore other
transitions while a confirmation is pending.

`Drawer` uses `open`, `onOpenChange` and a `panel` renderer. The application owns
modal Escape, inert background, focus containment and returning focus to the
trigger. Button refs expose `focus()`. `Stepper readOnly` displays progress
without accepting user selection; controlled progress updates still work.

## Notifications and accessibility

`NotificationProvider` is mounted above routes. Use `useNotify()` to enqueue
Toast messages, including after the originating dialog closes. Messages show in
order. Pass the callback into non-React refresh helpers. A failed refresh after
a successful write must report that changes were saved and request a refresh;
it must not turn the committed write into an apparent failed mutation.

Retain skip navigation, landmarks, keyboard navigation, accessible names,
field labels, textual loading/error states and focus return. Keep navigation
permission-aware, preserve the selected debug user in URLs, and do not expose
business actions beyond their existing server/client permission checks.

## Package updates and verification

Both built archives live in `frontend/vendor` with relative `file:` dependencies.
`npm ci` works independently of the source checkout. Update with
`npm run refresh:smbc -- /path/to/smbc`, then review library sources, archives,
manifest and lockfile together. See [the frontend README](../README.md).

After frontend changes run `npm test`, `npm run typecheck`, `npm run lint` and
`npm run build`. `npm run test:smbc` runs the production application against
controlled API fixtures with real installed controls. It checks remote grids,
actions, dialogs, audit focus, PDF, administrative confirmation, notifications,
local assets and responsive layouts. Inspect its screenshots in
`artifacts/smbc`. Library changes additionally require each package's check and
the `smbc-style` lint, typecheck and production reference browser suite.
