import type { LoadOptions, LoadResultObject } from 'devextreme/common/data';

import { apiFetch } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type {
  ApproveReviewRequest,
  MessageDetailsDto,
  MessageListItemDto,
  RejectReviewRequest,
  StartReviewRequest,
} from '../../shared/api/generated/contracts.generated';

export type MessageRow = MessageListItemDto;
export type MessageAssignmentScope = 'mine' | 'departments';

export async function getMessage(
  messageId: MessageRow['id'],
  signal?: AbortSignal,
): Promise<MessageDetailsDto> {
  try {
    const response = await apiFetch(`/api/messages/${messageId}`, { signal });

    if (!response.ok) {
      throw new ApiError(`Unable to load message (${response.status}).`, response.status);
    }

    return (await response.json()) as MessageDetailsDto;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    throw new Error('Unable to load message.', { cause: error });
  }
}

const loadOptionNames = [
  'skip',
  'take',
  'sort',
  'filter',
  'group',
  'totalSummary',
  'groupSummary',
  'select',
  'requireTotalCount',
  'requireGroupCount',
] as const;

function buildQuery(
  loadOptions: LoadOptions<MessageRow>,
  assignmentScope?: MessageAssignmentScope,
) {
  const query = new URLSearchParams({
    skip: String(loadOptions.skip ?? 0),
    take: String(loadOptions.take ?? 20),
  });

  if (assignmentScope) {
    query.set('assignmentScope', assignmentScope);
  }

  for (const name of loadOptionNames) {
    const value = loadOptions[name];

    if (value === undefined || name === 'skip' || name === 'take') {
      continue;
    }

    query.set(name, typeof value === 'object' ? JSON.stringify(value) : String(value));
  }

  return query;
}

export async function getMessageGrid(
  loadOptions: LoadOptions<MessageRow>,
  assignmentScope?: MessageAssignmentScope,
): Promise<LoadResultObject<MessageRow>> {
  try {
    const response = await apiFetch(
      `/api/messages/grid?${buildQuery(loadOptions, assignmentScope)}`,
    );

    if (!response.ok) {
      throw new ApiError(`Unable to load messages (${response.status}).`, response.status);
    }

    return (await response.json()) as LoadResultObject<MessageRow>;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    throw new Error('Unable to load messages.', { cause: error });
  }
}

async function postReviewAction(
  messageId: MessageRow['id'],
  action: 'start' | 'approve' | 'reject',
  request: StartReviewRequest | ApproveReviewRequest | RejectReviewRequest,
) {
  try {
    const response = await apiFetch(`/api/messages/${messageId}/reviews/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      let detail: string | undefined;
      if (response.status === 409) {
        try {
          const problem = (await response.json()) as { detail?: unknown };
          if (typeof problem.detail === 'string' && problem.detail.trim()) {
            detail = problem.detail;
          }
        } catch {
          // Fall back to the stable action error when the response is not Problem Details.
        }
      }
      throw new ApiError(
        detail ?? `Unable to ${action} review (${response.status}).`,
        response.status,
      );
    }
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    throw new Error(`Unable to ${action} review.`, { cause: error });
  }
}

export function startReview(messageId: MessageRow['id'], level: number) {
  return postReviewAction(messageId, 'start', { level });
}

export function approveReview(
  messageId: MessageRow['id'],
  level: number,
  comment: string | null,
) {
  return postReviewAction(messageId, 'approve', { level, comment });
}

export function rejectReview(
  messageId: MessageRow['id'],
  level: number,
  comment: string | null,
) {
  return postReviewAction(messageId, 'reject', { level, comment });
}
