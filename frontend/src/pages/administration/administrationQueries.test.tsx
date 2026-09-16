import { QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import { administrationKeys, currentUserKey, messageKeys } from '../../shared/api/queryKeys';
import { referenceDataKeys } from '../../shared/api/referenceDataQueries';
import { useUpdateRolePermissions, useUpdateUserAccess } from './administrationQueries';

const { updateRolePermissions, updateUserAccess } = vi.hoisted(() => ({ updateRolePermissions: vi.fn(), updateUserAccess: vi.fn() }));
vi.mock('./administrationApi', () => ({ updateRolePermissions, updateUserAccess }));
vi.mock('devextreme/ui/notify', () => ({ default: vi.fn() }));
beforeEach(() => { vi.resetAllMocks(); updateRolePermissions.mockResolvedValue(undefined); updateUserAccess.mockResolvedValue(undefined); });

it.each(['user', 'role'] as const)('invalidates only access-dependent data after updating %s', async (kind) => {
  const client = createTestQueryClient();
  const affected = [currentUserKey, referenceDataKeys.users, referenceDataKeys.branches, referenceDataKeys.departments,
    referenceDataKeys.workflows, referenceDataKeys.messageTypes, messageKeys.detail(42), messageKeys.audit(42),
    messageKeys.candidates(42), messageKeys.counts, administrationKeys.user(1),
    ...(kind === 'role' ? [administrationKeys.catalog, administrationKeys.user(2)] : [])];
  const untouched = [referenceDataKeys.messageStates, ['unrelated'],
    ...(kind === 'user' ? [administrationKeys.catalog, administrationKeys.user(2)] : [])];
  [...affected, ...untouched].forEach((key) => client.setQueryData(key, {}));
  const options = { onSuccess: vi.fn(), onError: vi.fn() };
  const { result } = renderHook(() => ({ user: useUpdateUserAccess(1, options), role: useUpdateRolePermissions(3, options) }), {
    wrapper: ({ children }: PropsWithChildren) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
  });
  await act(async () => {
    if (kind === 'user') await result.current.user.mutateAsync({ assignments: [] });
    else await result.current.role.mutateAsync({ permissions: ['message.view'] });
  });
  for (const key of affected) expect(client.getQueryState(key)?.isInvalidated).toBe(true);
  for (const key of untouched) expect(client.getQueryState(key)?.isInvalidated).toBe(false);
  expect(options.onSuccess).toHaveBeenCalledOnce();
  expect(options.onError).not.toHaveBeenCalled();
});
