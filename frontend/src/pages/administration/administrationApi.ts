import CustomStore from 'devextreme/data/custom_store';
import { apiRequest } from '../../shared/api/client';
import type {
  AccessCatalogDto, UserAccessDetailsDto, UpdateUserAccessRequest, UpdateRolePermissionsRequest,
} from '../../shared/api/generated/contracts.generated';

export type AdminUser = { id: number; userName: string; displayName: string };

export const getAccessCatalog = (signal?: AbortSignal) => apiRequest<AccessCatalogDto>('/api/admin/catalog', {
  signal, errorMessage: 'Unable to load the access catalog',
});
export const getUserAccess = (id: number, signal?: AbortSignal) => apiRequest<UserAccessDetailsDto>(`/api/admin/users/${id}/access`, {
  signal, errorMessage: 'Unable to load user access',
});
const put = (path: string, body: unknown) => apiRequest(`/api/admin${path}`, {
  method: 'PUT', body, responseType: 'none', errorMessage: 'Unable to update access',
});
export const updateUserAccess = (id: number, body: UpdateUserAccessRequest) => put(`/users/${id}/access`, body);
export const updateRolePermissions = (id: number, body: UpdateRolePermissionsRequest) => put(`/roles/${id}/permissions`, body);

export function createAdminUsersStore(search: string) {
  return new CustomStore<AdminUser, number>({
    key: 'id',
    load: (options) => {
      const query = new URLSearchParams({ skip: String(options.skip ?? 0), take: String(options.take ?? 20), search });
      if (options.sort) query.set('sort', JSON.stringify(options.sort));
      return apiRequest<{ data: AdminUser[]; totalCount: number }>(`/api/admin/users/grid?${query}`, {
        errorMessage: 'Unable to load users',
      });
    },
  });
}
