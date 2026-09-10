import type { CurrentUserResponse } from '../api/generated/contracts.generated';

export const MESSAGE_ASSIGN = 'message.assign';
export const AUDIT_VIEW = 'audit.view';

export function canManageWorkflows(permissions: string[], isGlobalAdministrator = false) {
  return isGlobalAdministrator || permissions.includes('workflow.manage');
}

export function canOpenMessagesPage(permissions: string[], isGlobalAdministrator = false) {
  return canAssignMessages(permissions, isGlobalAdministrator) || canManageWorkflows(permissions, isGlobalAdministrator);
}

export function permissionsForScope(user: CurrentUserResponse | undefined, branchId: number | string, departmentId: number | string) {
  if (user?.isGlobalAdministrator) return user.permissions;
  return user?.scopes?.find((scope) => String(scope.branchId) === String(branchId) && String(scope.departmentId) === String(departmentId))?.permissions ?? [];
}

export function canViewAudit(permissions: string[], isGlobalAdministrator = false) {
  return isGlobalAdministrator || permissions.includes(AUDIT_VIEW);
}

export function canAssignMessages(permissions: string[], isGlobalAdministrator = false) {
  return isGlobalAdministrator || permissions.includes(MESSAGE_ASSIGN);
}

export function canOpenReviewQueue(permissions: string[], isGlobalAdministrator = false) {
  return isGlobalAdministrator || permissions.includes('message.view');
}
