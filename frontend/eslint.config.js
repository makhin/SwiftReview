import { readdirSync } from 'node:fs'
import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import query from '@tanstack/eslint-plugin-query'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

// Relative imports and Vite's /src paths; update this prefix if aliases are added.
const localImportPrefix = String.raw`^(?:(?:\.\.?/)+(?:src/)?|/?src/)`
const smbcImports = {
  group: ['devextreme', 'devextreme/*', 'devextreme-react', 'devextreme-react/*'],
  message: 'Use @smbc/ui and @smbc/devextreme-theme public APIs.',
}
const appImports = {
  regex: `${localImportPrefix}app(?:/|$)`,
  message: 'Pages and shared modules must not import application composition.',
}
const pageSlices = readdirSync(new URL('./src/pages/', import.meta.url), { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)

export default defineConfig([
  globalIgnores(['coverage', 'dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
      ...query.configs['flat/recommended'],
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    rules: { 'no-restricted-imports': ['error', { patterns: [smbcImports] }] },
  },
  {
    files: ['src/shared/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [smbcImports, appImports, {
          regex: `${localImportPrefix}pages(?:/|$)`,
          message: 'Shared modules must not depend on pages.',
        }],
      }],
    },
  },
  {
    files: ['src/pages/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [smbcImports, appImports] }],
    },
  },
  {
    files: ['src/test/smbcMock.tsx'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  ...pageSlices.map((slice) => ({
    files: [`src/pages/${slice}/**/*.{ts,tsx}`],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [smbcImports, appImports, ...pageSlices.filter((other) => other !== slice).map((other) => ({
          regex: `${localImportPrefix}(?:pages/)?${other.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:/|$)`,
          message: 'Page slices must be independent; move cross-page code into shared.',
        }))],
      }],
    },
  })),
])
