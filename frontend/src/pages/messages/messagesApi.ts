import type { LoadOptions, LoadResultObject } from 'devextreme/common/data';

import { apiFetch } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type {
  ApproveReviewRequest,
  CancelReviewRequest,
  AssignmentCandidateDto,
  MessageDetailsDto,
  MessageStateCountDto,
  MessageListItemDto,
  RejectReviewRequest,
  StartReviewRequest,
  StartReviewResponse,
  UndoReviewRequest,
  ChangeMessageWorkflowRequest,
} from '../../shared/api/generated/contracts.generated';

export type MessageRow = MessageListItemDto & {
  requiredReviewLevels?: number[];
  undoReviewId?: number | string | null;
  workflowDefinitionId?: number | string;
  canChangeWorkflow?: boolean;
  canReview?: boolean;
};

export async function changeMessageWorkflow(messageId: MessageRow['id'], workflowDefinitionId: number | string) {
  const request: ChangeMessageWorkflowRequest = { workflowDefinitionId };
  const response = await apiFetch(`/api/messages/${messageId}/workflow`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string } | null;
    throw new ApiError(problem?.detail ?? 'Unable to change workflow. Refresh the grid and check your access and review history.', response.status);
  }
}
export type MessageAssignmentScope = 'mine' | 'departments' | 'assignable';

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
  action: 'start' | 'approve' | 'reject' | 'cancel',
  request: StartReviewRequest | ApproveReviewRequest | RejectReviewRequest | CancelReviewRequest,
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
    return response;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    throw new Error(`Unable to ${action} review.`, { cause: error });
  }
}

export async function startReview(messageId: MessageRow['id'], level: number) {
  const response = await postReviewAction(messageId, 'start', { level });
  const result = await response.json() as StartReviewResponse;
  if (result.reviewId == null || !/^[1-9][0-9]*$/.test(String(result.reviewId))) throw new Error('Invalid review ID returned by the server.');
  return result.reviewId;
}

export function cancelReview(messageId: MessageRow['id'], level: number, reviewId: CancelReviewRequest['reviewId']) {
  return postReviewAction(messageId, 'cancel', { level, reviewId });
}

export async function undoReview(messageId: MessageRow['id'], reviewId: UndoReviewRequest['reviewId'], comment: string | null = null) {
  const request: UndoReviewRequest = { reviewId, comment };
  const response = await apiFetch(`/api/messages/${messageId}/undo`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new ApiError('Unable to undo the approval. Refresh the grid and check the message state.', response.status);
}

export function approveReview(
  messageId: MessageRow['id'],
  level: number,
  comment: string | null,
  reviewId: ApproveReviewRequest['reviewId'],
) {
  return postReviewAction(messageId, 'approve', { level, comment, reviewId });
}

export function rejectReview(
  messageId: MessageRow['id'],
  level: number,
  comment: string | null,
  reviewId: ApproveReviewRequest['reviewId'],
) {
  return postReviewAction(messageId, 'reject', { level, comment, reviewId });
}

export async function getAssignmentCandidates(
  messageId: MessageRow['id'],
  signal?: AbortSignal,
): Promise<AssignmentCandidateDto[]> {
  try {
    const response = await apiFetch(`/api/messages/${messageId}/assignment-candidates`, {
      signal,
    });
    if (!response.ok) {
      throw new ApiError(
        `Unable to load assignment candidates (${response.status}).`,
        response.status,
      );
    }
    return (await response.json()) as AssignmentCandidateDto[];
  } catch (error) {
    if (error instanceof ApiError || signal?.aborted) {
      throw error;
    }
    throw new Error('Unable to load assignment candidates.', { cause: error });
  }
}

export async function assignMessage(
  messageId: MessageRow['id'],
  assignedTo: number | string,
  reassign: boolean,
) {
  const action = reassign ? 'reassign' : 'assign';
  try {
    const response = await apiFetch(`/api/messages/${messageId}/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ assignedTo }),
    });
    if (!response.ok) {
      let detail: string | undefined;
      try {
        const problem = (await response.json()) as { detail?: unknown };
        if (typeof problem.detail === 'string' && problem.detail.trim()) {
          detail = problem.detail;
        }
      } catch {
        // Fall back to the stable assignment error.
      }
      throw new ApiError(detail ?? `Unable to ${action} message (${response.status}).`, response.status);
    }
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }
    throw new Error(`Unable to ${action} message.`, { cause: error });
  }
}

export async function getMessageStateCounts(signal?: AbortSignal): Promise<MessageStateCountDto[]> {
  const response = await apiFetch('/api/messages/state-counts', { signal });
  if (!response.ok) throw new ApiError('Unable to load message counts.', response.status);
  return response.json() as Promise<MessageStateCountDto[]>;
}
