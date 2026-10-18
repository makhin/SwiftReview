import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';
vi.mock('../shared/notifications/notifications', async (original) => ({
  ...await original<typeof import('../shared/notifications/notifications')>(),
  useNotify: () => () => undefined,
}));
vi.mock('@smbc/ui', () => import('./smbcMock'));
afterEach(cleanup);
