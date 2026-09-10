import { useState } from 'react';
import Button from 'devextreme-react/button';
import notify from 'devextreme/ui/notify';

export default function GridRefreshButton({ refresh }: { refresh: () => PromiseLike<unknown> | undefined }) {
  const [pending, setPending] = useState(false);

  async function reload() {
    if (pending) return;
    setPending(true);
    try {
      await refresh();
    } catch {
      notify('Unable to refresh the grid. Check your connection and try again.', 'error', 4000);
    } finally {
      setPending(false);
    }
  }

  return <Button text={pending ? 'Refreshing…' : 'Refresh'} icon="refresh" disabled={pending}
    useSubmitBehavior={false} onClick={() => void reload()} />;
}
