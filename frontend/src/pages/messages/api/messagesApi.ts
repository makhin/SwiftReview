import type { LoadOptions, LoadResultObject } from 'devextreme/common/data';

import { apiRequest } from '../../../shared/api/client';
import { ApiRequestError } from '../../../shared/api/errors';
import type {
  ApproveReviewRequest,
  CancelReviewRequest,
  AssignmentCandidateDto,
  MessageDetailsDto,
  MessageStateCountDto,
  MessageGridRowDto,
  RejectReviewRequest,
  StartReviewRequest,
  StartReviewResponse,
  UndoReviewRequest,
  ChangeMessageWorkflowRequest,
} from '../../../shared/api/generated/contracts.generated';

export type MessageRow = MessageGridRowDto;

export function changeMessageWorkflow(messageId: MessageRow['id'], workflowDefinitionId: number | string) {
  const body: ChangeMessageWorkflowRequest = { workflowDefinitionId };
  return apiRequest(`/api/messages/${messageId}/workflow`, {
    method: 'PUT', body, responseType: 'none', errorMessage: 'Unable to change workflow',
  });
}

export type MessageAssignmentScope = 'mine' | 'departments' | 'assignable';

export function getMessage(messageId: MessageRow['id'], signal?: AbortSignal): Promise<MessageDetailsDto> {
  return apiRequest(`/api/messages/${messageId}`, { signal, errorMessage: 'Unable to load message' });
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

export function getMessageGrid(
  loadOptions: LoadOptions<MessageRow>,
  assignmentScope?: MessageAssignmentScope,
): Promise<LoadResultObject<MessageRow>> {
  return apiRequest(`/api/messages/grid?${buildQuery(loadOptions, assignmentScope)}`, {
    errorMessage: 'Unable to load messages',
  });
}

function postReviewAction(
  messageId: MessageRow['id'],
  action: 'approve' | 'reject' | 'cancel' | 'undo',
  body: ApproveReviewRequest | RejectReviewRequest | CancelReviewRequest | UndoReviewRequest,
) {
  return apiRequest(`/api/messages/${messageId}/reviews/${action}`, {
    method: 'POST', body, responseType: 'none', errorMessage: `Unable to ${action} review`,
  });
}

export async function startReview(messageId: MessageRow['id'], level: number) {
  const body: StartReviewRequest = { level };
  const result = await apiRequest<StartReviewResponse>(`/api/messages/${messageId}/reviews/start`, {
    method: 'POST', body, errorMessage: 'Unable to start review',
  });
  if (result?.reviewId == null || !/^[1-9][0-9]*$/.test(String(result.reviewId))) {
    throw new ApiRequestError(
      'Invalid review ID returned by the server. The result is unknown. Refresh the data before trying again.',
      'invalid-response', { outcomeUnknown: true },
    );
  }
  return result.reviewId;
}

export function cancelReview(messageId: MessageRow['id'], level: number, reviewId: CancelReviewRequest['reviewId']) {
  return postReviewAction(messageId, 'cancel', { level, reviewId });
}

export function undoReview(messageId: MessageRow['id'], reviewId: UndoReviewRequest['reviewId'], comment: string | null = null) {
  return postReviewAction(messageId, 'undo', { reviewId, comment });
}

export function approveReview(
  messageId: MessageRow['id'], level: number, comment: string | null, reviewId: ApproveReviewRequest['reviewId'],
) {
  return postReviewAction(messageId, 'approve', { level, comment, reviewId });
}

export function rejectReview(
  messageId: MessageRow['id'], level: number, comment: string | null, reviewId: RejectReviewRequest['reviewId'],
) {
  return postReviewAction(messageId, 'reject', { level, comment, reviewId });
}

export function getAssignmentCandidates(
  messageId: MessageRow['id'], signal?: AbortSignal,
): Promise<AssignmentCandidateDto[]> {
  return apiRequest(`/api/messages/${messageId}/assignment-candidates`, {
    signal, errorMessage: 'Unable to load assignment candidates',
  });
}

export function assignMessage(messageId: MessageRow['id'], assignedTo: number | string, reassign: boolean) {
  const action = reassign ? 'reassign' : 'assign';
  return apiRequest(`/api/messages/${messageId}/${action}`, {
    method: 'POST', body: { assignedTo }, responseType: 'none', errorMessage: `Unable to ${action} message`,
  });
}

export function getMessageStateCounts(signal?: AbortSignal): Promise<MessageStateCountDto[]> {
  return apiRequest('/api/messages/state-counts', { signal, errorMessage: 'Unable to load message counts' });
}
