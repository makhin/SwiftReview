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
    files: ['src/shared/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [appImports, {
          regex: `${localImportPrefix}pages(?:/|$)`,
          message: 'Shared modules must not depend on pages.',
        }],
      }],
    },
  },
  {
    files: ['src/pages/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [appImports] }],
    },
  },
  ...pageSlices.map((slice) => ({
    files: [`src/pages/${slice}/**/*.{ts,tsx}`],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [appImports, ...pageSlices.filter((other) => other !== slice).map((other) => ({
          regex: `${localImportPrefix}(?:pages/)?${other.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:/|$)`,
          message: 'Page slices must be independent; move cross-page code into shared.',
        }))],
      }],
    },
  })),
])
