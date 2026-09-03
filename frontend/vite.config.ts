import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

// https://vite.dev/config/
const envDir = fileURLToPath(new URL('..', import.meta.url));

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, envDir);

  return {
    envDir,
    plugins: [react()],
    test: {
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts',
      coverage: {
        provider: 'v8',
        reporter: ['text', 'html'],
        include: [
          'src/shared/api/client.ts',
          'src/app/**/*.{ts,tsx}',
          'src/pages/current-user/**/*.{ts,tsx}',
          'src/pages/messages/**/*.{ts,tsx}',
        ],
        exclude: [
          'src/**/*.test.{ts,tsx}',
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
          target: env.VITE_API_PROXY_TARGET || 'http://localhost:5080',
          headers: {
            'X-Debug-User': env.VITE_DEBUG_USER || 'supervisor',
          },
        },
      },
    },
  };
});
