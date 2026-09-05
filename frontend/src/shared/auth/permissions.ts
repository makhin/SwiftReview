export const MESSAGE_ACCESS_ALL_DEPARTMENTS = 'message.access.all-departments';

export function canViewAllMessages(permissions: string[]) {
  return permissions.includes(MESSAGE_ACCESS_ALL_DEPARTMENTS);
}
