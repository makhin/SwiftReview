import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
const apiFetch = vi.fn();
import { createAdminUsersStore, updateUserAccess, updateRolePermissions, getAccessCatalog, getUserAccess } from './administrationApi';

describe('administrationApi', () => {
  beforeEach(() => { apiFetch.mockReset(); vi.stubGlobal('fetch', apiFetch); });
  afterEach(() => vi.unstubAllGlobals());
  it.each([
    ['/api/admin/catalog', (signal: AbortSignal) => getAccessCatalog(signal)],
    ['/api/admin/users/1/access', (signal: AbortSignal) => getUserAccess(1, signal)],
  ] as const)('loads %s with cancellation', async (path, load) => {
    const signal = new AbortController().signal;
    apiFetch.mockResolvedValue(new Response('{}'));
    await expect(load(signal)).resolves.toEqual({});
    expect(apiFetch).toHaveBeenCalledWith(path, expect.objectContaining({ signal }));
  });

  it('accepts an empty successful user access update', async () => {
    apiFetch.mockResolvedValue(new Response(null, { status: 204 }));
    await expect(updateUserAccess(1, { assignments: [] })).resolves.toBeUndefined();
    expect(apiFetch).toHaveBeenCalledWith('/api/admin/users/1/access', expect.objectContaining({
      method: 'PUT', body: '{"assignments":[]}',
    }));
  });

  it('preserves an unknown outcome for a role update', async () => {
    apiFetch.mockRejectedValue(new TypeError('Response lost'));
    await expect(updateRolePermissions(1, { permissions: [] })).rejects.toMatchObject({
      kind: 'network', outcomeUnknown: true,
    });
  });
  it('loads grid pages through CustomStore with remote search and sort', async () => {
    const result = { data: [{ id: 1, userName: 'test', displayName: 'Test' }], totalCount: 1 };
    apiFetch.mockResolvedValue(new Response(JSON.stringify(result)));
    const store = createAdminUsersStore('Test & team');
    expect(await store.load({ skip: 20, take: 10, sort: [{ selector: 'displayName', desc: true }] })).toEqual(result);
    const url = new URL(apiFetch.mock.calls[0][0] as string, 'http://localhost');
    expect(url.pathname).toBe('/api/admin/users/grid');
    expect(url.searchParams.get('search')).toBe('Test & team');
    expect(url.searchParams.get('skip')).toBe('20');
    expect(JSON.parse(url.searchParams.get('sort')!)).toEqual([{ selector: 'displayName', desc: true }]);
  });
  it('preserves server validation messages and status', async () => {
    apiFetch.mockResolvedValue(new Response(JSON.stringify({ detail: 'Unknown branch, department or role.' }), { status: 400 }));
    await expect(updateUserAccess(1, { assignments: [] })).rejects.toMatchObject({ status: 400, message: 'Unknown branch, department or role.' });
  });
});
