import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';

const { approveReview, rejectReview, startReview } = vi.hoisted(() => ({
  approveReview: vi.fn(),
  rejectReview: vi.fn(),
  startReview: vi.fn(),
}));

vi.mock('./messagesApi', () => ({ approveReview, rejectReview, startReview }));
vi.mock('devextreme-react/popup', () => ({
  default: ({ children, title }: PropsWithChildren<{ title: string }>) => (
    <section role="dialog" aria-label={title}>{children}</section>
  ),
}));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, onClick, disabled }: {
    text: string;
    onClick: () => void;
    disabled?: boolean;
  }) => (
    <button type="button" disabled={disabled} onClick={onClick}>{text}</button>
  ),
}));
vi.mock('devextreme-react/text-area', () => ({
  default: ({ value, onValueChanged, maxLength, disabled, inputAttr }: {
    value: string;
    onValueChanged: (event: { value: string }) => void;
    maxLength: number;
    disabled?: boolean;
    inputAttr: { 'aria-label': string };
  }) => (
    <textarea
      aria-label={inputAttr['aria-label']}
      value={value}
      maxLength={maxLength}
      disabled={disabled}
      onChange={(event) => onValueChanged({ value: event.target.value })}
    />
  ),
}));

import ReviewDecisionPopup from './ReviewDecisionPopup';

const baseMessage = {
  id: 42,
  externalId: 'MSG-0042',
  messageType: 'MT103',
  branchId: 10,
  departmentId: 20,
  receivedAt: '2026-09-05T08:00:00Z',
  currentAssigneeId: 1,
  activeReviewId: null,
  activeReviewLevel: null,
  activeReviewerId: null,
  account: null,
  currency: null,
  amount: null,
};

describe('ReviewDecisionPopup', () => {
  beforeEach(() => {
    approveReview.mockReset().mockResolvedValue(undefined);
    rejectReview.mockReset().mockResolvedValue(undefined);
    startReview.mockReset().mockResolvedValue(undefined);
  });

  it('cancels without changing the review', () => {
    const onClose = vi.fn();
    render(
      <ReviewDecisionPopup
        decision="approve"
        message={{ ...baseMessage, state: 'Assigned' }}
        onClose={onClose}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalledOnce();
    expect(startReview).not.toHaveBeenCalled();
    expect(approveReview).not.toHaveBeenCalled();
  });

  it('starts a waiting review before approving it', async () => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <ReviewDecisionPopup
        decision="approve"
        message={{ ...baseMessage, state: 'WaitingForSecondReview' }}
        onClose={onClose}
        onChanged={onChanged}
      />,
    );

    fireEvent.change(screen.getByLabelText('Comment (optional)'), {
      target: { value: '  confirmed  ' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(approveReview).toHaveBeenCalledWith(42, 2, 'confirmed'));
    expect(startReview).toHaveBeenCalledWith(42, 2);
    expect(startReview.mock.invocationCallOrder[0]).toBeLessThan(
      approveReview.mock.invocationCallOrder[0],
    );
    expect(onClose).toHaveBeenCalledOnce();
    expect(onChanged).toHaveBeenCalledOnce();
  });

  it('rejects an active review without starting it and allows no comment', async () => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <ReviewDecisionPopup
        decision="reject"
        message={{ ...baseMessage, state: 'ThirdReviewInProgress' }}
        onClose={onClose}
        onChanged={onChanged}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }));

    await waitFor(() => expect(rejectReview).toHaveBeenCalledWith(42, 3, null));
    expect(startReview).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledOnce();
    expect(onChanged).toHaveBeenCalledOnce();
  });

  it('starts and approves the third review level', async () => {
    render(
      <ReviewDecisionPopup
        decision="approve"
        message={{ ...baseMessage, state: 'WaitingForThirdReview' }}
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(approveReview).toHaveBeenCalledWith(42, 3, null));
    expect(startReview).toHaveBeenCalledWith(42, 3);
  });

  it('shows an automatic-assignment conflict and keeps the dialog open', async () => {
    approveReview.mockRejectedValue(new ApiError(
      'No eligible reviewer is available for review level 2.',
      409,
    ));
    const onClose = vi.fn();
    render(
      <ReviewDecisionPopup
        decision="approve"
        message={{ ...baseMessage, state: 'FirstReviewInProgress' }}
        onClose={onClose}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No eligible reviewer is available for review level 2.',
    );
    expect(onClose).not.toHaveBeenCalled();
  });

  it('keeps the dialog open and refreshes after a started review fails to approve', async () => {
    approveReview
      .mockRejectedValueOnce(new Error('conflict'))
      .mockResolvedValueOnce(undefined);
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <ReviewDecisionPopup
        decision="approve"
        message={{ ...baseMessage, state: 'Assigned' }}
        onClose={onClose}
        onChanged={onChanged}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to approve the message',
    );
    expect(onClose).not.toHaveBeenCalled();
    expect(onChanged).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(approveReview).toHaveBeenCalledTimes(2));
    expect(startReview).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalledOnce();
    expect(onChanged).toHaveBeenCalledTimes(2);
  });
});
