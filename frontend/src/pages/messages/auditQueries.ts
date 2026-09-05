import { infiniteQueryOptions } from '@tanstack/react-query';

import { getMessageAudit } from './auditApi';

export const AUDIT_PAGE_SIZE = 50;

export function messageAuditQueryOptions(messageId: number | string) {
  return infiniteQueryOptions({
    queryKey: ['messages', messageId, 'audit'],
    queryFn: ({ pageParam, signal }) =>
      getMessageAudit(messageId, pageParam, AUDIT_PAGE_SIZE, signal),
    initialPageParam: 0,
    getNextPageParam: (lastPage, pages) => {
      const loadedCount = pages.reduce((count, page) => count + page.items.length, 0);
      return loadedCount < Number(lastPage.totalCount) ? loadedCount : undefined;
    },
  });
}
