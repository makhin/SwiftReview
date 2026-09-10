import { describe, expect, it, vi } from 'vitest';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('./currentUserApi', () => ({ getCurrentUser }));

import { currentUserQueryOptions } from './currentUserQueries';

describe('currentUserQueryOptions', () => {
  it('refreshes access on mount and periodically', () => {
    const options = currentUserQueryOptions();

    expect(options.queryKey).toEqual(['current-user']);
    expect(options.staleTime).toBe(0);
    expect(options.refetchInterval).toBe(30_000);
  });
});
