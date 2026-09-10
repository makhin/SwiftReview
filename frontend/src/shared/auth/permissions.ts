import type { CurrentUserResponse } from '../api/generated/contracts.generated';

export const MESSAGE_ASSIGN = 'message.assign';
export const AUDIT_VIEW = 'audit.view';

export function canReviewMessages(permissions: string[]) {
  return ['review.level1', 'review.level2', 'review.level3'].some((permission) => permissions.includes(permission));
}

export function permissionsForScope(user: CurrentUserResponse | undefined, branchId: number | string, departmentId: number | string) {
  return user?.scopes?.find((scope) => String(scope.branchId) === String(branchId) && String(scope.departmentId) === String(departmentId))?.permissions ?? [];
}

export function canViewAudit(permissions: string[]) {
  return permissions.includes(AUDIT_VIEW);
}

export function canAssignMessages(permissions: string[]) {
  return permissions.includes(MESSAGE_ASSIGN);
}
