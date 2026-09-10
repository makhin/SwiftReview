import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { notify } = vi.hoisted(() => ({ notify: vi.fn() }));
vi.mock('devextreme/ui/notify', () => ({ default: notify }));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, disabled, onClick }: { text: string; disabled: boolean; onClick: () => void }) =>
    <button type="button" disabled={disabled} onClick={onClick}>{text}</button>,
}));
import GridRefreshButton from './GridRefreshButton';

describe('GridRefreshButton', () => {
  it('prevents duplicate refreshes while loading and allows refresh again afterwards', async () => {
    let resolve!: () => void;
    const refresh = vi.fn(() => new Promise<void>((done) => { resolve = done; }));
    render(<GridRefreshButton refresh={refresh} />);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    const pending = screen.getByRole('button', { name: 'Refreshing…' });
    expect(pending).toBeDisabled();
    fireEvent.click(pending);
    expect(refresh).toHaveBeenCalledOnce();
    resolve();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
  });

  it('reports a refresh failure and lets the user retry', async () => {
    const refresh = vi.fn().mockRejectedValueOnce(new Error('Offline')).mockResolvedValue(undefined);
    render(<GridRefreshButton refresh={refresh} />);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('Unable to refresh'), 'error', 4000));
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
    expect(refresh).toHaveBeenCalledTimes(2);
  });
});
