import { queryOptions } from '@tanstack/react-query';

import { getCurrentUser } from './currentUserApi';

export function currentUserQueryOptions() {
  return queryOptions({
    queryKey: ['current-user'],
    queryFn: ({ signal }) => getCurrentUser(signal),
    staleTime: 0,
    refetchInterval: 30_000,
  });
}
