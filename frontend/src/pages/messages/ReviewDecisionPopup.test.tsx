import { QueryClientProvider } from '@tanstack/react-query';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import { fireEvent, render as renderComponent, screen, waitFor } from '@testing-library/react';
import type { PropsWithChildren, ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';

const { approveReview, getMessage, notify, rejectReview, startReview } = vi.hoisted(() => ({
  approveReview: vi.fn(),
  getMessage: vi.fn(),
  notify: vi.fn(),
  rejectReview: vi.fn(),
  startReview: vi.fn(),
}));

vi.mock('devextreme/ui/notify', () => ({ default: notify }));
vi.mock('./messagesApi', () => ({ approveReview, getMessage, rejectReview, startReview }));
vi.mock('devextreme-react/popup', () => ({
  default: ({ children, title, width, maxWidth }: PropsWithChildren<{
    title: string;
    width: string;
    maxWidth: number;
  }>) => (
    <section role="dialog" aria-label={title} data-width={width} data-max-width={maxWidth}>{children}</section>
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
  default: ({ value, onValueChanged, maxLength, disabled, inputAttr, placeholder }: {
    value: string;
    onValueChanged: (event: { value: string }) => void;
    maxLength: number;
    disabled?: boolean;
    inputAttr: { id: string; 'aria-label': string };
    placeholder: string;
  }) => (
    <textarea
      aria-label={inputAttr['aria-label']}
      id={inputAttr.id}
      placeholder={placeholder}
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

function render(ui: ReactElement, preload = true) {
  const client = createTestQueryClient();
  if (preload) {
    client.setQueryData(['messages', 42], { body: '{1:F01RAW}\n  {4:PAYLOAD}' });
  }
  return renderComponent(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('ReviewDecisionPopup', () => {
  beforeEach(() => {
    notify.mockReset();
    getMessage.mockReset().mockResolvedValue({ body: '{1:F01RAW}\n  {4:PAYLOAD}' });
    approveReview.mockReset().mockResolvedValue(undefined);
    rejectReview.mockReset().mockResolvedValue(undefined);
    startReview.mockReset().mockResolvedValue(undefined);
  });

  it('shows raw text and all three actions with an optional comment', () => {
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
        message={{ ...baseMessage, state: 'Assigned' }}
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />,
    );

    expect(screen.getByLabelText('Raw message content').textContent).toBe('{1:F01RAW}\n  {4:PAYLOAD}');
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeEnabled();
    expect(screen.getByLabelText('Comment (optional)')).toHaveAttribute('placeholder', '');
    expect(screen.getByRole('dialog')).toHaveAttribute('data-width', '90vw');
    expect(screen.getByRole('dialog')).toHaveAttribute('data-max-width', '900');
  });

  it('cancels without changing the review', () => {
    const onClose = vi.fn();
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
        message={{ ...baseMessage, state: 'Assigned' }}
        onClose={onClose}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalledOnce();
    expect(startReview).not.toHaveBeenCalled();
    expect(approveReview).not.toHaveBeenCalled();
    expect(rejectReview).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();
  });

  it('starts a waiting review before approving it', async () => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
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
    expect(notify).toHaveBeenCalledWith('Message MSG-0042 approved.', 'success', 4000);
    expect(onClose.mock.invocationCallOrder[0]).toBeLessThan(notify.mock.invocationCallOrder[0]);
  });

  it('rejects an active review without starting it and allows no comment', async () => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
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
    expect(notify).toHaveBeenCalledWith('Message MSG-0042 rejected.', 'success', 4000);
    expect(onClose.mock.invocationCallOrder[0]).toBeLessThan(notify.mock.invocationCallOrder[0]);
  });

  it('starts and approves the third review level', async () => {
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
        message={{ ...baseMessage, state: 'WaitingForThirdReview' }}
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(approveReview).toHaveBeenCalledWith(42, 3, null));
    expect(startReview).toHaveBeenCalledWith(42, 3);
  });

  it('shows a review conflict and keeps the dialog open', async () => {
    approveReview.mockRejectedValue(new ApiError(
      'The review state changed. Refresh and try again.',
      409,
    ));
    const onClose = vi.fn();
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
        message={{ ...baseMessage, state: 'FirstReviewInProgress' }}
        onClose={onClose}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The review state changed. Refresh and try again.',
    );
    expect(onClose).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();
  });

  it('keeps the dialog open and refreshes after a started review fails to approve', async () => {
    approveReview
      .mockRejectedValueOnce(new Error('conflict'))
      .mockResolvedValueOnce(undefined);
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <ReviewDecisionPopup
        canApprove
        canReject
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
    expect(notify).not.toHaveBeenCalled();
    expect(onChanged).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(approveReview).toHaveBeenCalledTimes(2));
    expect(startReview).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalledOnce();
    expect(onChanged).toHaveBeenCalledTimes(2);
  });

  it('disables decisions until the raw message loads and supports retry', async () => {
    getMessage.mockRejectedValueOnce(new Error('network failure'))
      .mockResolvedValueOnce({ body: 'RAW MESSAGE' });
    render(
      <ReviewDecisionPopup canApprove canReject message={{ ...baseMessage, state: 'Assigned' }}
        onClose={vi.fn()} onChanged={vi.fn()} />,
      false,
    );
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load raw message');
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByLabelText('Raw message content')).toHaveTextContent('RAW MESSAGE');
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(getMessage).toHaveBeenCalledWith(42, expect.anything());
  });

  it('shows an empty state when the message has no raw content', async () => {
    getMessage.mockResolvedValue({ body: null });
    render(
      <ReviewDecisionPopup canApprove={false} canReject={false}
        message={{ ...baseMessage, state: 'New' }} onClose={vi.fn()} onChanged={vi.fn()} />,
      false,
    );
    expect(await screen.findByText('No raw message content available.')).toBeInTheDocument();
  });

  it.each([[false, false], [true, false], [false, true]])(
    'respects independent decision permissions (approve: %s, reject: %s)',
    (canApprove, canReject) => {
      render(
        <ReviewDecisionPopup canApprove={canApprove} canReject={canReject}
          message={{ ...baseMessage, state: 'FirstReviewInProgress' }}
          onClose={vi.fn()} onChanged={vi.fn()} />,
      );
      expect(screen.getByRole('button', { name: 'Approve' })).toHaveProperty('disabled', !canApprove);
      expect(screen.getByRole('button', { name: 'Reject' })).toHaveProperty('disabled', !canReject);
    },
  );

  it('disables all actions while submitting a decision', async () => {
    approveReview.mockImplementation(() => new Promise(() => undefined));
    render(
      <ReviewDecisionPopup canApprove canReject
        message={{ ...baseMessage, state: 'FirstReviewInProgress' }}
        onClose={vi.fn()} onChanged={vi.fn()} />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(screen.getByRole('button', { name: 'Approve…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(rejectReview).not.toHaveBeenCalled();
  });

});
