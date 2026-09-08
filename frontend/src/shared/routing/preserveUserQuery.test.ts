import { afterEach, describe, expect, it, vi } from 'vitest';

import { preserveUserQuery } from './preserveUserQuery';

describe('preserveUserQuery', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('preserves the user and destination parameters in development', () => {
    vi.stubEnv('DEV', true);

    expect(
      preserveUserQuery('/messages/assigned?scope=mine', '?user=alex.morgan'),
    ).toBe('/messages/assigned?scope=mine&user=alex.morgan');
  });

  it('does not add the user in production', () => {
    vi.stubEnv('DEV', false);

    expect(preserveUserQuery('/messages', '?user=alex.morgan')).toBe('/messages');
  });
});
