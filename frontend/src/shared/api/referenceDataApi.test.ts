import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get } = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('./client', () => ({ apiClient: { GET: get } }));

import { ApiError } from './errors';
import {
  getBranches,
  getDepartments,
  getMessageTypes,
  getUsers,
  getWorkflows,
} from './referenceDataApi';

describe('reference data API', () => {
  beforeEach(() => {
    get.mockReset();
  });

  it.each([
    ['/api/users', getUsers],
    ['/api/branches', getBranches],
    ['/api/departments', getDepartments],
    ['/api/message-types', getMessageTypes],
    ['/api/workflows', getWorkflows],
  ] as const)('loads %s and forwards the abort signal', async (path, load) => {
    const controller = new AbortController();
    get.mockResolvedValue({ data: [], response: { status: 200 } });

    await expect(load(controller.signal)).resolves.toEqual([]);
    expect(get).toHaveBeenCalledWith(path, { signal: controller.signal });
  });

  it('throws an ApiError containing the response status', async () => {
    get.mockResolvedValue({ data: undefined, response: { status: 403 } });

    await expect(getBranches()).rejects.toEqual(
      new ApiError('Unable to load branches (403).', 403),
    );
  });

  it('normalizes network failures', async () => {
    const networkError = new TypeError('fetch failed');
    get.mockRejectedValue(networkError);

    await expect(getDepartments()).rejects.toMatchObject({
      message: 'Unable to load departments.',
      cause: networkError,
    });
  });

  it('preserves abort errors', async () => {
    const controller = new AbortController();
    const abortError = new DOMException('aborted', 'AbortError');
    controller.abort();
    get.mockRejectedValue(abortError);

    await expect(getUsers(controller.signal)).rejects.toBe(abortError);
  });
});
