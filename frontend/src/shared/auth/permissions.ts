export const MESSAGE_ACCESS_ALL_DEPARTMENTS = 'message.access.all-departments';
export const MESSAGE_ASSIGN = 'message.assign';
export const AUDIT_VIEW = 'audit.view';

export function canViewAllMessages(permissions: string[]) {
  return permissions.includes(MESSAGE_ACCESS_ALL_DEPARTMENTS);
}

export function canViewAudit(permissions: string[]) {
  return permissions.includes(AUDIT_VIEW);
}

export function canAssignMessages(permissions: string[]) {
  return permissions.includes(MESSAGE_ASSIGN);
}
