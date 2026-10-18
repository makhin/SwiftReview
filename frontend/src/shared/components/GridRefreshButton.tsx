import { useState } from 'react';
import { Button } from '@smbc/ui';
import { useNotify } from '../notifications/notifications';

export default function GridRefreshButton({
  refresh,
}: {
  refresh: () => PromiseLike<unknown> | undefined;
}) {
  const notify = useNotify();
  const [pending, setPending] = useState(false);

  async function reload() {
    if (pending) return;
    setPending(true);
    try {
      await refresh();
    } catch {
      notify(
        'Unable to refresh the grid. Check your connection and try again.',
        'error',
        4000,
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <Button
      icon="refresh"
      disabled={pending}
      submit={false}
      onClick={() => void reload()}
      variant="secondary"
      children={pending ? 'Refreshing…' : 'Refresh'}
    />
  );
}
