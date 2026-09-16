import { apiRequest } from '../../../shared/api/client';
import type { PagedResultOfAuditEventDto } from '../../../shared/api/generated/contracts.generated';

export function getMessageAudit(
  messageId: number | string,
  skip: number,
  take: number,
  signal?: AbortSignal,
): Promise<PagedResultOfAuditEventDto> {
  const query = new URLSearchParams({ skip: String(skip), take: String(take) });
  return apiRequest(`/api/messages/${messageId}/audit?${query}`, {
    signal, errorMessage: 'Unable to load the audit trail',
  });
}
