import CustomStore from 'devextreme/data/custom_store';
import { apiFetch } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type {
  AccessCatalogDto, UserAccessDetailsDto, UpdateUserAccessRequest, UpdateRolePermissionsRequest,
} from '../../shared/api/generated/contracts.generated';

export type AdminUser = { id: number; userName: string; displayName: string };

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiFetch(`/api/admin${path}`, init);
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
    throw new ApiError(problem?.detail ?? problem?.title ?? 'Unable to update access.', response.status);
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}

export const getAccessCatalog = (signal?: AbortSignal) => request<AccessCatalogDto>('/catalog', { signal });
export const getUserAccess = (id: number, signal?: AbortSignal) => request<UserAccessDetailsDto>(`/users/${id}/access`, { signal });
const put = (path: string, body: unknown) => request<void>(path, {
  method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
});
export const updateUserAccess = (id: number, body: UpdateUserAccessRequest) => put(`/users/${id}/access`, body);
export const updateRolePermissions = (id: number, body: UpdateRolePermissionsRequest) => put(`/roles/${id}/permissions`, body);

export function createAdminUsersStore(search: string) {
  return new CustomStore<AdminUser, number>({
    key: 'id',
    load: (options) => {
      const query = new URLSearchParams({ skip: String(options.skip ?? 0), take: String(options.take ?? 20), search });
      if (options.sort) query.set('sort', JSON.stringify(options.sort));
      return request<{ data: AdminUser[]; totalCount: number }>(`/users/grid?${query}`);
    },
  });
}
