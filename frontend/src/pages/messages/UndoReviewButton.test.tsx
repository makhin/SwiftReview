import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PropsWithChildren } from 'react';
import type { MessageRow } from './messagesApi';

const { undoReview, notify } = vi.hoisted(() => ({ undoReview: vi.fn(), notify: vi.fn() }));
vi.mock('./messagesApi', () => ({ undoReview }));
vi.mock('devextreme/ui/notify', () => ({ default: notify }));
vi.mock('devextreme-react/popup', () => ({
  default: ({ children }: PropsWithChildren) => <section role="dialog" aria-label="Undo approval">{children}</section>,
}));
vi.mock('devextreme-react/text-area', () => ({
  default: ({ value, disabled, maxLength, inputAttr, onValueChanged }: {
    value: string; disabled: boolean; maxLength: number; inputAttr: { id: string; 'aria-label': string };
    onValueChanged: (event: { value: string }) => void;
  }) => <textarea value={value} disabled={disabled} maxLength={maxLength} id={inputAttr.id}
    aria-label={inputAttr['aria-label']} onChange={(event) => onValueChanged({ value: event.target.value })} />,
}));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, disabled, onClick }: { text: string; disabled: boolean; onClick: () => void }) =>
    <button disabled={disabled} onClick={onClick}>{text}</button>,
}));
import UndoReviewButton from './UndoReviewButton';

const message: MessageRow = { id: 42, externalId: 'MSG-42', messageType: 'MT199', branchId: 1, departmentId: 1,
  receivedAt: '2026-09-10T10:00:00Z', state: 'Completed', currentAssigneeId: null,
  activeReviewId: null, activeReviewLevel: null, activeReviewerId: null, undoReviewId: 73 };

describe('UndoReviewButton', () => {
  afterEach(() => { vi.restoreAllMocks(); vi.resetAllMocks(); });

  it('confirms, sends the exact review ID and refreshes after completion', async () => {
    let resolve!: () => void;
    undoReview.mockReturnValue(new Promise<void>((done) => { resolve = done; }));
    const onChanged = vi.fn();
    render(<UndoReviewButton message={message} onChanged={onChanged} />);
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(screen.getByText(/ends any current assignment/)).toBeInTheDocument();
    expect(undoReview).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Comment (optional)'), { target: { value: '  Please check again  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm undo' }));
    const button = screen.getByRole('button', { name: 'Undoing…' });
    expect(button).toBeDisabled();
    fireEvent.click(button);
    expect(undoReview).toHaveBeenCalledExactlyOnceWith(42, 73, 'Please check again');
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(screen.getByLabelText('Comment (optional)')).toBeDisabled();
    expect(onChanged).not.toHaveBeenCalled();
    resolve();
    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Approval undone'), 'success', 4000);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('does nothing when confirmation is cancelled', () => {
    render(<UndoReviewButton message={message} onChanged={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(undoReview).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it.each(['', '   '])('allows undo without a comment (%j)', async (comment) => {
    undoReview.mockResolvedValue(undefined);
    render(<UndoReviewButton message={message} onChanged={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(screen.getByLabelText('Comment (optional)')).toHaveAttribute('maxlength', '2000');
    fireEvent.change(screen.getByLabelText('Comment (optional)'), { target: { value: comment } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm undo' }));
    await waitFor(() => expect(undoReview).toHaveBeenCalledExactlyOnceWith(42, 73, null));
  });

  it.each(['New', 'Assigned', 'FirstReviewInProgress', 'SecondReviewInProgress', 'ThirdReviewInProgress', 'Rejected'] as const)(
    'disables undo in %s even if the row contains a stale review ID', (state) => {
      render(<UndoReviewButton message={{ ...message, state }} onChanged={vi.fn()} />);
      expect(screen.getByRole('button', { name: 'Undo' })).toBeDisabled();
    },
  );

  it('disables undo when no approval is available', () => {
    render(<UndoReviewButton message={{ ...message, undoReviewId: null }} onChanged={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Undo' })).toBeDisabled();
  });

  it('refreshes after an uncertain response without retrying or reporting success', async () => {
    undoReview.mockRejectedValue(new Error('Response lost'));
    const onChanged = vi.fn();
    render(<UndoReviewButton message={message} onChanged={onChanged} />);
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    fireEvent.change(screen.getByLabelText('Comment (optional)'), { target: { value: 'Check this approval' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm undo' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(notify).toHaveBeenCalledExactlyOnceWith(expect.stringContaining('check the audit trail'), 'error', 4000);
    expect(undoReview).toHaveBeenCalledOnce();
    expect(screen.getByLabelText('Comment (optional)')).toHaveValue('Check this approval');
  });
});
