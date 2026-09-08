import { describe, expect, it, vi } from 'vitest';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('./currentUserApi', () => ({ getCurrentUser }));

import { currentUserQueryOptions } from './currentUserQueries';

describe('currentUserQueryOptions', () => {
  it('defines the current-user key and a five-minute stale time', () => {
    const options = currentUserQueryOptions();

    expect(options.queryKey).toEqual(['current-user']);
    expect(options.staleTime).toBe(5 * 60 * 1000);
  });
});
