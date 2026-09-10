import { beforeEach, describe, expect, it, vi } from 'vitest';
const { apiFetch } = vi.hoisted(() => ({ apiFetch: vi.fn() }));
vi.mock('../../shared/api/client', () => ({ apiFetch }));
import { createAdminUsersStore, updateUserAccess } from './administrationApi';

describe('administrationApi', () => {
  beforeEach(() => apiFetch.mockReset());
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
