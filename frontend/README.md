# SMBC Application UI

React application shell and design-system reference built with DevExtreme and a custom SMBC ThemeBuilder theme.

The implementation standard and UI rules live in [`DESIGN_GUIDE.md`](DESIGN_GUIDE.md).

## Commands

```bash
npm run dev
npm run lint
npm run typecheck
npm run theme:build
npm run build
```

Run `theme:build` after changing mapped design tokens or ThemeBuilder settings. It synchronizes metadata from the canonical values in `src/theme/tokens.css`, then regenerates `src/theme/dx.smbc.css`. Keep the generated metadata and CSS under source control; do not edit the generated CSS manually.

## Routes

- `/` — redirects to the design-system reference
- `/design-system` — component and token reference

All routes render inside `RootLayout`, which provides the shared EMEA-style global header. The header uses semantic HTML and React Router links; DevExtreme is reserved for interactive widgets inside the shell rather than the structural navigation itself.

## Style order

Global styles are loaded in this order from `src/main.tsx`:

1. Local fonts, application tokens, and shared patterns
2. Generated DevExtreme theme
3. SMBC DevExtreme overrides
4. DevExtreme visualization palette
5. Minimal application-root CSS

## Typography

- Myriad Pro is the default family for application text, controls, navigation, and operational headings.
- Capitolium 2 is reserved for explicit brand/display headings via `app-display-title` or `app-display-heading`.
- DevExtreme widgets inherit Myriad Pro from the generated theme and SMBC overrides.
- Fonts, the SMBC logo, and the favicon are bundled locally; the application must not load brand assets from Typekit or the SMBC website at runtime.

## Design tokens

`src/theme/tokens.css` is the source of truth for the application and DevExtreme integration:

- palette primitives define raw brand, neutral, feedback, and data-visualisation values;
- semantic colour roles describe intent such as text, surface, border, action, navigation, and feedback;
- components consume semantic roles rather than palette primitives;
- typography, spacing, radius, outline, shadow, and motion scales live alongside the colour tokens;
- `scripts/sync-devextreme-theme.mjs` resolves the relevant semantic values into ThemeBuilder metadata;
- `smbc-viz-palette.ts` reads chart colours from the same CSS tokens at runtime.

One-off content dimensions and responsive breakpoints remain local to their components because they are layout constraints, not reusable design decisions.
