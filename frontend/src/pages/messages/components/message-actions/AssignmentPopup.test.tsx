import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { assignMessage, getAssignmentCandidates, notify } = vi.hoisted(() => ({
  notify: vi.fn(),
  assignMessage: vi.fn(),
  getAssignmentCandidates: vi.fn(),
}));

vi.mock('devextreme/ui/notify', () => ({ default: notify }));
vi.mock('../../api/messagesApi', () => ({ assignMessage, getAssignmentCandidates }));
vi.mock('devextreme-react/popup', () => ({
  default: ({ children, title, width, maxWidth }: PropsWithChildren<{
    title: string;
    width: string;
    maxWidth: number;
  }>) =>
    <section role="dialog" aria-label={title} data-width={width} data-max-width={maxWidth}>{children}</section>,
}));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, onClick, disabled }: { text: string; onClick: () => void; disabled?: boolean }) =>
    <button type="button" disabled={disabled} onClick={onClick}>{text}</button>,
}));
vi.mock('devextreme-react/select-box', () => ({
  default: ({ items, onValueChanged, inputAttr, placeholder }: {
    items: Array<{ id: number; displayName: string }>;
    onValueChanged: (event: { value: number }) => void;
    inputAttr: { id: string; 'aria-label': string };
    placeholder: string;
  }) => (
    <select id={inputAttr.id} aria-label={inputAttr['aria-label']} data-placeholder={placeholder} onChange={(event) =>
      onValueChanged({ value: Number(event.target.value) })}>
      <option value="">Select</option>
      {items.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}
    </select>
  ),
}));

import AssignmentPopup from './AssignmentPopup';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';

const message = {
  id: 42,
  externalId: 'MSG-0042',
  messageType: 'MT103',
  branchId: 10,
  departmentId: 20,
  state: 'New' as const,
  receivedAt: '2026-09-05T08:00:00Z',
  currentAssigneeId: null,
  activeReviewId: null,
  activeReviewLevel: null,
  activeReviewerId: null,
};

describe('AssignmentPopup', () => {
  beforeEach(() => {
    notify.mockReset();
    getAssignmentCandidates.mockReset().mockResolvedValue([
      { id: 2, userName: 'sam.lee', displayName: 'Sam Lee' },
    ]);
    assignMessage.mockReset().mockResolvedValue(undefined);
  });

  it.each([false, true])('confirms successful assignment (reassign: %s)', async (reassign) => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(<AssignmentPopup message={{ ...message, currentAssigneeId: reassign ? 1 : null }} onClose={onClose} onChanged={onChanged} />);

    const reviewer = await screen.findByLabelText('Reviewer');
    expect(reviewer).toHaveAttribute('data-placeholder', '');
    expect(screen.getByRole('dialog')).toHaveAttribute('data-width', '90vw');
    expect(screen.getByRole('dialog')).toHaveAttribute('data-max-width', '520');
    fireEvent.change(reviewer, { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: reassign ? 'Reassign' : 'Assign' }));

    await waitFor(() => expect(assignMessage).toHaveBeenCalledWith(42, 2, reassign));
    expect(notify).toHaveBeenCalledExactlyOnceWith(`Message MSG-0042 ${reassign ? 'reassigned' : 'assigned'}.`, 'success', 4000);
    expect(onChanged).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalledOnce();
  });

  it.each([false, true])('does not confirm a failed assignment (reassign: %s)', async (reassign) => {
    assignMessage.mockRejectedValue(new Error('Request failed'));
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(<AssignmentPopup message={{ ...message, currentAssigneeId: reassign ? 1 : null }} onClose={onClose} onChanged={onChanged} />);
    fireEvent.change(await screen.findByLabelText('Reviewer'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: reassign ? 'Reassign' : 'Assign' }));
    await screen.findByRole('alert');
    expect(notify).toHaveBeenCalledExactlyOnceWith(`Unable to ${reassign ? 'reassign' : 'assign'} the message.`, 'error', 4000);
    expect(onChanged).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('shows an empty eligible-reviewer state', async () => {
    getAssignmentCandidates.mockResolvedValue([]);
    render(<AssignmentPopup message={message} onClose={vi.fn()} onChanged={vi.fn()} />);

    expect(await screen.findByText('No eligible reviewers are available.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled();
  });

  it.each([
    new ApiError('Reviewer access denied.', 403),
    new ApiRequestError('Unable to load assignment candidates.', 'network'),
  ])('shows a typed reviewer-loading error: %s', async (error) => {
    getAssignmentCandidates.mockRejectedValue(error);
    render(<AssignmentPopup message={message} onClose={vi.fn()} onChanged={vi.fn()} />);
    expect(await screen.findByRole('alert')).toHaveTextContent(error.message);
    expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled();
    expect(assignMessage).not.toHaveBeenCalled();
  });

  it('shows an unknown outcome without claiming success after a lost response', async () => {
    const error = new ApiRequestError(
      'Unable to assign message. The result is unknown. Refresh the data before trying again.',
      'network', { outcomeUnknown: true },
    );
    assignMessage.mockRejectedValue(error);
    const onClose = vi.fn();
    render(<AssignmentPopup message={message} onClose={onClose} onChanged={vi.fn()} />);
    fireEvent.change(await screen.findByLabelText('Reviewer'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Assign' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(error.message);
    expect(notify).toHaveBeenCalledExactlyOnceWith(error.message, 'error', 4000);
    expect(onClose).not.toHaveBeenCalled();
  });
});
