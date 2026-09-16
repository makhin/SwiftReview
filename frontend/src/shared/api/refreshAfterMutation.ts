import type { InvalidateQueryFilters, QueryClient } from '@tanstack/react-query';
import notify from 'devextreme/ui/notify';

export type RefreshData = () => void | PromiseLike<unknown>;

// Refresh failures must never turn a committed mutation into a failed mutation.
export async function refreshAfterMutation(
  client: QueryClient, filters: InvalidateQueryFilters[], refresh: RefreshData | undefined, saved: boolean,
) {
  const results = await Promise.allSettled([
    ...filters.map((filter) => Promise.resolve().then(() => client.invalidateQueries(filter, { throwOnError: true }))),
    Promise.resolve().then(() => refresh?.()),
  ]);
  if (results.some((result) => result.status === 'rejected')) {
    notify(saved
      ? 'Changes saved, but the displayed data could not be refreshed. Refresh the list before continuing.'
      : 'Unable to refresh the displayed data. Refresh the list before continuing.', 'warning', 6000);
  }
}
