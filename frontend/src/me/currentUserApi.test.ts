import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get } = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../api/client', () => ({ apiClient: { GET: get } }));

import { ApiError } from '../api/errors';
import { getCurrentUser } from './currentUserApi';

const currentUser = {
  userId: 42,
  userName: 'Alex Morgan',
  permissions: ['messages.read'],
  branches: [10],
  departments: [20],
};

describe('getCurrentUser', () => {
  beforeEach(() => {
    get.mockReset();
  });

  it('returns the current user and forwards the abort signal', async () => {
    const controller = new AbortController();
    get.mockResolvedValue({ data: currentUser, response: { status: 200 } });

    await expect(getCurrentUser(controller.signal)).resolves.toEqual(currentUser);
    expect(get).toHaveBeenCalledWith('/api/me', { signal: controller.signal });
  });

  it('throws an ApiError containing the response status', async () => {
    get.mockResolvedValue({ data: undefined, response: { status: 403 } });

    await expect(getCurrentUser()).rejects.toEqual(
      new ApiError('Unable to load the current user (403).', 403),
    );
  });

  it('normalizes a non-Error request failure', async () => {
    get.mockRejectedValue('network unavailable');

    await expect(getCurrentUser()).rejects.toThrow('Unable to load the current user.');
  });
});
