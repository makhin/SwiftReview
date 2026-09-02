import { readFile, writeFile } from 'node:fs/promises';

const tokensPath = new URL('../src/theme/tokens.css', import.meta.url);
const metadataPath = new URL('../src/theme/smbc-theme.metadata.json', import.meta.url);
const packagePath = new URL('../node_modules/devextreme-themebuilder/package.json', import.meta.url);

const css = await readFile(tokensPath, 'utf8');
const metadata = JSON.parse(await readFile(metadataPath, 'utf8'));
const themeBuilderPackage = JSON.parse(await readFile(packagePath, 'utf8'));

const declarations = new Map();
const declarationPattern = /--([a-z0-9-]+):\s*([^;]+);/gi;

for (const match of css.matchAll(declarationPattern)) {
  declarations.set(match[1], match[2].trim());
}

function resolveToken(name, stack = []) {
  if (stack.includes(name)) {
    throw new Error(`Circular design token reference: ${[...stack, name].join(' -> ')}`);
  }

  const value = declarations.get(name);
  if (!value) {
    throw new Error(`Missing required design token: --${name}`);
  }

  return value.replace(/var\(--([a-z0-9-]+)\)/gi, (_, dependency) =>
    resolveToken(dependency, [...stack, name]));
}

const themeBuilderTokens = {
  '$base-accent': 'color-action-primary',
  '$base-text-color': 'color-text-primary',
  '$base-bg': 'color-surface-default',
  '$base-border-color': 'color-border-default',
  '$base-border-radius': 'radius-sm',
  '$base-font-family': 'font-sans',
  '$base-success': 'color-feedback-success',
  '$base-warning': 'color-feedback-warning',
  '$base-danger': 'color-feedback-danger',
  '$base-hover-color': 'color-text-primary',
  '$base-hover-bg': 'color-surface-hover',
  '$base-focus-color': 'color-text-primary',
  '$base-focus-bg': 'color-surface-hover',
  '$base-link-color': 'color-text-link',
  '$button-normal-hover-bg': 'color-surface-hover',
  '$button-normal-focused-bg': 'color-surface-hover',
  '$button-normal-active-bg': 'color-surface-selected',
  '$button-normal-selected-bg': 'color-surface-selected',
  '$button-default-hover-bg': 'color-action-primary-hover',
  '$button-default-focused-bg': 'color-action-primary-hover',
  '$button-default-active-bg': 'color-action-primary-active',
  '$button-default-selected-bg': 'color-action-primary-active',
  '$calendar-cell-selected-bg': 'color-action-primary',
  '$datagrid-selection-bg': 'color-surface-selected',
  '$datagrid-row-selected-border-color': 'color-border-default',
  '$datagrid-row-selected-color': 'color-text-primary',
  '$datagrid-row-focused-color': 'color-text-primary',
  '$datagrid-row-focused-bg': 'color-surface-selected',
  '$datagrid-row-alternation-bg': 'color-surface-alternate',
  '$list-item-hover-bg': 'color-surface-hover',
  '$list-item-selected-bg': 'color-surface-selected',
  '$list-item-active-bg': 'color-surface-selected',
  '$menu-item-selected-bg': 'color-surface-selected',
  '$tagbox-tag-color': 'color-text-primary',
  '$tagbox-tag-bg': 'color-surface-hover',
  '$tagbox-tag-active-color': 'color-text-primary',
  '$tagbox-tag-button-remove-bg': 'color-text-secondary',
  '$accordion-title-color': 'color-text-primary',
  '$fluent-accordion-title-font-size': 'font-size-sm',
  '$pagination-page-selected-color': 'color-text-inverse',
  '$pagination-page-selected-bg': 'color-action-primary',
  '$load-indicator-segment-bg-color': 'color-action-primary',
  '$load-indicator-inner-segment-border-color': 'color-border-default',
  '$progressbar-range-bg': 'color-action-primary',
  '$chat-bubble-color-primary': 'color-text-primary',
  '$chat-bubble-background-color-primary': 'color-surface-selected',
  '$cardview-fluent-card--selected-bg-color': 'color-surface-selected',
  '$cardview-fluent-card--selected-border-color': 'color-border-strong',
  '$cardview-fluent-card--selected-divider-color': 'color-border-default',
  '$cardview-fluent-card--selected-text-color': 'color-text-primary',
  '$cardview-fluent-header-panel__dropzone-bg-color': 'color-surface-selected',
  '$cardview-fluent-header-panel__dropzone-border-color': 'color-action-primary',
  '$cardview-fluent-header-panel__dropzone-text-color': 'color-action-primary',
  '$cardview-fluent-header-panel__dropzone__icon-color': 'color-action-primary',
  '$scheduler-appointment-base-color': 'color-surface-selected',
  '$scheduler-appointment-text-color': 'color-text-primary',
  '$scheduler-appointment-start-color': 'color-action-primary-muted',
  '$scheduler-workspace-focused-cell-color': 'color-surface-selected',
};

const metadataItems = new Map(metadata.items.map((item) => [item.key, item]));

for (const [key, tokenName] of Object.entries(themeBuilderTokens)) {
  const value = resolveToken(tokenName).replaceAll('"', '');
  const item = metadataItems.get(key);

  if (item) {
    item.value = value;
  } else {
    metadata.items.push({ key, value });
  }
}

metadata.version = themeBuilderPackage.version;

await writeFile(metadataPath, `${JSON.stringify(metadata, null, 2)}\n`);
