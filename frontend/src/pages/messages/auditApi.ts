import { apiClient } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type { PagedResultOfAuditEventDto } from '../../shared/api/generated/contracts.generated';

export async function getMessageAudit(
  messageId: number | string,
  skip: number,
  take: number,
  signal?: AbortSignal,
): Promise<PagedResultOfAuditEventDto> {
  try {
    const query = new URLSearchParams({ skip: String(skip), take: String(take) });
    const { data, response } = await apiClient.GET<PagedResultOfAuditEventDto>(
      `/api/messages/${messageId}/audit?${query}`,
      { signal },
    );

    if (!data) {
      throw new ApiError(
        `Unable to load the audit trail (${response.status}).`,
        response.status,
      );
    }

    return data;
  } catch (error) {
    if (error instanceof ApiError || signal?.aborted) {
      throw error;
    }

    throw new Error('Unable to load the audit trail.', { cause: error });
  }
}
