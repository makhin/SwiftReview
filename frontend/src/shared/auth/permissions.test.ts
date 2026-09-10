import { describe, expect, it } from 'vitest';
import { permissionsForScope } from './permissions';
import type { CurrentUserResponse } from '../api/generated/contracts.generated';

const user: CurrentUserResponse = {
  userId: 1, userName: 'test', displayName: 'Test', isGlobalAdministrator: true,
  permissions: ['message.view', 'review.level1', 'review.level2'], branches: [1, 2], departments: [1, 2],
  scopes: [
    { branchId: 1, departmentId: 1, roleIds: [1], permissions: ['message.view', 'review.level1'] },
    { branchId: 2, departmentId: 2, roleIds: [2], permissions: ['message.view', 'review.level2'] },
  ],
};

describe('permissionsForScope', () => {
  it('uses the exact branch and department, not aggregate permissions or admin status', () => {
    expect(permissionsForScope(user, 1, 1)).toEqual(['message.view', 'review.level1']);
    expect(permissionsForScope(user, 1, 2)).toEqual([]);
    expect(permissionsForScope(user, 2, 1)).toEqual([]);
  });
  it('normalizes numeric IDs from the API and denies access before user data is loaded', () => {
    expect(permissionsForScope(user, '2', '2')).toEqual(['message.view', 'review.level2']);
    expect(permissionsForScope(undefined, 1, 1)).toEqual([]);
  });
});
