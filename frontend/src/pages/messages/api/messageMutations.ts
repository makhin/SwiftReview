import { useQueryClient } from '@tanstack/react-query';
import { messageKeys } from '../../../shared/api/queryKeys';
import { refreshAfterMutation, type RefreshData } from '../../../shared/api/refreshAfterMutation';
import { useExclusiveMutation } from '../../../shared/api/useExclusiveMutation';
import { ApiError, ApiRequestError } from '../../../shared/api/errors';
import { assignMessage, changeMessageWorkflow, undoReview, startReview, approveReview, rejectReview, cancelReview } from './messagesApi';

export function useRefreshMessage(id: number | string, refresh?: RefreshData) {
  const client = useQueryClient();
  return (saved: boolean) => refreshAfterMutation(client, [
    // Include detail, audit and candidates, including equivalent numeric/string IDs.
    { predicate: ({ queryKey }) => queryKey[0] === messageKeys.all[0] && String(queryKey[1]) === String(id) },
    { queryKey: messageKeys.counts },
  ], refresh, saved);
}

type Options = { onChanged: RefreshData; onSuccess: () => void; onError: (error: Error) => void };
function useMessageMutation<T>(id: number | string, mutationFn: (variables: T) => Promise<unknown>, options: Options, refreshOnError = false) {
  const refresh = useRefreshMessage(id, options.onChanged);
  return useExclusiveMutation({ mutationFn,
    onSuccess: async () => { options.onSuccess(); await refresh(true); },
    onError: async (error) => {
      options.onError(error);
      if (refreshOnError || (error instanceof ApiError && error.status === 409) || (error instanceof ApiRequestError && error.outcomeUnknown)) await refresh(false);
    },
  });
}
export function useAssignMessage(id: number | string, options: Options) {
  return useMessageMutation(id, ({ assignedTo, reassign }: { assignedTo: number | string; reassign: boolean }) =>
    assignMessage(id, assignedTo, reassign), options);
}
export function useChangeMessageWorkflow(id: number | string, options: Options) {
  return useMessageMutation(id, (workflowId: number | string) => changeMessageWorkflow(id, workflowId), options, true);
}
export function useUndoReview(id: number | string, options: Options) {
  return useMessageMutation(id, ({ reviewId, comment }: { reviewId: number | string; comment: string | null }) =>
    undoReview(id, reviewId, comment), options, true);
}

// Recovery and refresh timing belong to useReviewSession's state machine.
export function useReviewOperations(id: number | string) {
  const start = useExclusiveMutation({ mutationFn: (level: number) => startReview(id, level) });
  const decision = useExclusiveMutation({ mutationFn: (input: {
    action: 'approve' | 'reject' | 'cancel'; level: number; reviewId: number | string; comment: string | null;
  }) => input.action === 'cancel' ? cancelReview(id, input.level, input.reviewId)
    : input.action === 'approve' ? approveReview(id, input.level, input.comment, input.reviewId)
      : rejectReview(id, input.level, input.comment, input.reviewId) });
  return { start: start.mutateAsync, decide: decision.mutateAsync };
}
