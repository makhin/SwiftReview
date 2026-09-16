import { queryOptions } from '@tanstack/react-query';

import { currentUserKey } from './queryKeys';

import { getCurrentUser } from './currentUserApi';

export function currentUserQueryOptions() {
  return queryOptions({
    queryKey: currentUserKey,
    queryFn: ({ signal }) => getCurrentUser(signal),
    staleTime: 0,
    refetchInterval: 30_000,
  });
}
