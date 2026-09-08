import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { assignMessage, getAssignmentCandidates } = vi.hoisted(() => ({
  assignMessage: vi.fn(),
  getAssignmentCandidates: vi.fn(),
}));

vi.mock('./messagesApi', () => ({ assignMessage, getAssignmentCandidates }));
vi.mock('devextreme-react/popup', () => ({
  default: ({ children, title }: PropsWithChildren<{ title: string }>) =>
    <section role="dialog" aria-label={title}>{children}</section>,
}));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, onClick, disabled }: { text: string; onClick: () => void; disabled?: boolean }) =>
    <button type="button" disabled={disabled} onClick={onClick}>{text}</button>,
}));
vi.mock('devextreme-react/select-box', () => ({
  default: ({ items, onValueChanged, inputAttr }: {
    items: Array<{ id: number; displayName: string }>;
    onValueChanged: (event: { value: number }) => void;
    inputAttr: { 'aria-label': string };
  }) => (
    <select aria-label={inputAttr['aria-label']} onChange={(event) =>
      onValueChanged({ value: Number(event.target.value) })}>
      <option value="">Select</option>
      {items.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}
    </select>
  ),
}));

import AssignmentPopup from './AssignmentPopup';

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
  account: null,
  currency: null,
  amount: null,
};

describe('AssignmentPopup', () => {
  beforeEach(() => {
    getAssignmentCandidates.mockReset().mockResolvedValue([
      { id: 2, userName: 'sam.lee', displayName: 'Sam Lee' },
    ]);
    assignMessage.mockReset().mockResolvedValue(undefined);
  });

  it('assigns a selected eligible reviewer', async () => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(<AssignmentPopup message={message} onClose={onClose} onChanged={onChanged} />);

    fireEvent.change(await screen.findByLabelText('Reviewer'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Assign' }));

    await waitFor(() => expect(assignMessage).toHaveBeenCalledWith(42, 2, false));
    expect(onChanged).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('shows an empty eligible-reviewer state', async () => {
    getAssignmentCandidates.mockResolvedValue([]);
    render(<AssignmentPopup message={message} onClose={vi.fn()} onChanged={vi.fn()} />);

    expect(await screen.findByText('No eligible reviewers are available.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled();
  });
});
