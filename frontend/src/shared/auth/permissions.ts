import type { CurrentUserResponse } from '../api/generated/contracts.generated';

export const MESSAGE_VIEW = 'message.view';
export const MESSAGE_ASSIGN = 'message.assign';
export const AUDIT_VIEW = 'audit.view';

export function canViewAllMessages(permissions: string[]) {
  return permissions.includes(MESSAGE_VIEW);
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
