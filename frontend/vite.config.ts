import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      include: [
        'src/api/client.ts',
        'src/app/**/*.{ts,tsx}',
        'src/me/**/*.{ts,tsx}',
        'src/messages/**/*.{ts,tsx}',
      ],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/design-system/**',
        'src/theme/**',
      ],
      thresholds: {
        branches: 80,
        functions: 90,
        lines: 90,
        statements: 90,
      },
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        headers: {
          'X-Debug-User': 'supervisor',
        },
      },
    },
  },
});
