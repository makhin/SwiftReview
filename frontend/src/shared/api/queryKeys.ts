// Shared keys let access mutations invalidate dependent data without importing page slices.
export const currentUserKey = ['current-user'] as const;
export const messageKeys = {
  all: ['messages'] as const,
  detail: (id: number | string) => ['messages', id] as const,
  audit: (id: number | string) => ['messages', id, 'audit'] as const,
  candidates: (id: number | string) => ['messages', id, 'assignment-candidates'] as const,
  counts: ['message-state-counts'] as const,
};
export const administrationKeys = {
  catalog: ['admin', 'catalog'] as const,
  users: ['admin', 'user'] as const,
  user: (id: number | undefined) => ['admin', 'user', id] as const,
};
