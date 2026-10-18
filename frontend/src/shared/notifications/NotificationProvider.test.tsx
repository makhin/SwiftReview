import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

vi.unmock('./notifications');
vi.mock('@smbc/ui', () => ({
  Toast: ({ message, onClose }: { message: string; onClose: () => void }) => (
    <div role="status">
      {message}
      <button onClick={onClose}>Dismiss</button>
    </div>
  ),
}));
import NotificationProvider from './NotificationProvider';
import { useNotify } from './notifications';

function DialogContent({ close }: { close: () => void }) {
  const notify = useNotify();
  return (
    <button
      onClick={() => {
        notify('Changes saved.', 'success', 4000);
        close();
        notify('Refresh failed.', 'warning', 6000);
      }}
    >
      Save
    </button>
  );
}
function Consumer() {
  const [open, setOpen] = useState(true);
  return open ? (
    <DialogContent close={() => setOpen(false)} />
  ) : (
    <p>Dialog closed</p>
  );
}
describe('NotificationProvider', () => {
  it('keeps queued notifications after their caller unmounts and shows them in order', () => {
    render(
      <NotificationProvider>
        <Consumer />
      </NotificationProvider>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(screen.getByText('Dialog closed')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Changes saved.');
    expect(screen.queryByText('Refresh failed.')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(screen.getByRole('status')).toHaveTextContent('Refresh failed.');
    fireEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
