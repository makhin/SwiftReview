import { queryOptions, useQuery } from '@tanstack/react-query';
import { messageKeys } from '../../../shared/api/queryKeys';
import type { CurrentUserResponse } from '../../../shared/api/generated/contracts.generated';
import { getAssignmentCandidates, getMessage, getMessageStateCounts } from './messagesApi';

export function messageQueryOptions(id: number | string) {
  return queryOptions({ queryKey: messageKeys.detail(id), queryFn: ({ signal }) => getMessage(id, signal) });
}
export function useAssignmentCandidates(id: number | string) {
  return useQuery({ queryKey: messageKeys.candidates(id), queryFn: ({ signal }) => getAssignmentCandidates(id, signal) });
}
export function messageCountsQueryOptions(user: CurrentUserResponse | undefined) {
  return queryOptions({
    queryKey: [...messageKeys.counts, user?.userId, user?.scopes, user?.isGlobalAdministrator],
    queryFn: ({ signal }) => getMessageStateCounts(signal), enabled: user != null, refetchInterval: 30_000,
  });
}
