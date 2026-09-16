import { queryOptions, useQueryClient } from '@tanstack/react-query';
import { administrationKeys, currentUserKey, messageKeys } from '../../shared/api/queryKeys';
import { referenceDataKeys } from '../../shared/api/referenceDataQueries';
import { ApiError, ApiRequestError } from '../../shared/api/errors';
import { refreshAfterMutation } from '../../shared/api/refreshAfterMutation';
import { useExclusiveMutation } from '../../shared/api/useExclusiveMutation';
import { getAccessCatalog, getUserAccess, updateRolePermissions, updateUserAccess } from './administrationApi';
import type { UpdateRolePermissionsRequest, UpdateUserAccessRequest } from '../../shared/api/generated/contracts.generated';

export const accessCatalogQueryOptions = () => queryOptions({
  queryKey: administrationKeys.catalog, queryFn: ({ signal }) => getAccessCatalog(signal),
});
export const userAccessQueryOptions = (id: number | undefined) => queryOptions({
  queryKey: administrationKeys.user(id), queryFn: ({ signal }) => getUserAccess(id!, signal), enabled: id !== undefined,
});

type Options = { onSuccess: () => void; onError: (error: Error) => void };
function useAccessMutation<T>(mutationFn: (input: T) => Promise<unknown>, keys: readonly (readonly unknown[])[], options: Options) {
  const client = useQueryClient();
  // Reference lists are scope-filtered by the backend; message-state definitions are static.
  const filters = [
    ...keys.map((queryKey) => ({ queryKey })),
    ...[currentUserKey, referenceDataKeys.users, referenceDataKeys.branches,
      referenceDataKeys.departments, referenceDataKeys.workflows, referenceDataKeys.messageTypes,
      messageKeys.all, messageKeys.counts].map((queryKey) => ({ queryKey })),
  ];
  return useExclusiveMutation({ mutationFn,
    onError: async (error) => {
      options.onError(error);
      if ((error instanceof ApiError && error.status === 409) || (error instanceof ApiRequestError && error.outcomeUnknown))
        await refreshAfterMutation(client, filters, undefined, false);
    },
    onSuccess: async () => {
      options.onSuccess();
      await refreshAfterMutation(client, filters, undefined, true);
    },
  });
}
export function useUpdateUserAccess(id: number, options: Options) {
  return useAccessMutation((input: UpdateUserAccessRequest) => updateUserAccess(id, input), [administrationKeys.user(id)], options);
}
export function useUpdateRolePermissions(id: number, options: Options) {
  return useAccessMutation((input: UpdateRolePermissionsRequest) => updateRolePermissions(id, input),
    [administrationKeys.catalog, administrationKeys.users], options);
}
