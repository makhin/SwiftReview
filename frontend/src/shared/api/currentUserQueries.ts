import { queryOptions } from '@tanstack/react-query';

import { getCurrentUser } from './currentUserApi';

const FIVE_MINUTES = 5 * 60 * 1000;

export function currentUserQueryOptions() {
  return queryOptions({
    queryKey: ['current-user'],
    queryFn: ({ signal }) => getCurrentUser(signal),
    staleTime: FIVE_MINUTES,
  });
}
