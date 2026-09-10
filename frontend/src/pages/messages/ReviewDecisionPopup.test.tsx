import { QueryClientProvider } from '@tanstack/react-query';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import { fireEvent, render as renderComponent, screen, waitFor } from '@testing-library/react';
import { StrictMode, type PropsWithChildren, type ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';

const { approveReview, cancelReview, getMessage, notify, rejectReview, startReview } = vi.hoisted(() => ({
  approveReview: vi.fn(),
  cancelReview: vi.fn(),
  getMessage: vi.fn(),
  notify: vi.fn(),
  rejectReview: vi.fn(),
  startReview: vi.fn(),
}));

vi.mock('devextreme/ui/notify', () => ({ default: notify }));
vi.mock('./messagesApi', () => ({ approveReview, cancelReview, getMessage, rejectReview, startReview }));
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
    getMessage.mockReset().mockResolvedValue({ body: '{1:F01RAW}\n  {4:PAYLOAD}', state: 'FirstReviewInProgress' });
    approveReview.mockReset().mockResolvedValue(undefined);
    cancelReview.mockReset().mockResolvedValue(undefined);
    rejectReview.mockReset().mockResolvedValue(undefined);
    startReview.mockReset().mockResolvedValue(undefined);
  });

  function open(overrides: Partial<React.ComponentProps<typeof ReviewDecisionPopup>> = {}, strict = false) {
    const props = { canApprove: true, canReject: true, message: { ...baseMessage, state: 'Assigned' as const },
      onClose: vi.fn(), onChanged: vi.fn(), ...overrides };
    const popup = <ReviewDecisionPopup {...props} />;
    render(strict ? <StrictMode>{popup}</StrictMode> : popup);
    return props;
  }
  async function ready() {
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());
  }

  it('starts on opening, before exposing the message or enabling decisions', async () => {
    let resolve!: () => void;
    startReview.mockReturnValue(new Promise<void>((done) => { resolve = done; }));
    const props = open();
    expect(startReview).toHaveBeenCalledWith(42, 1);
    expect(screen.queryByLabelText('Raw message content')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel review' })).toBeDisabled();
    resolve();
    await ready();
    expect(screen.getByRole('button', { name: 'Close' })).toBeEnabled();
    expect(screen.getByLabelText('Raw message content').textContent).toBe('{1:F01RAW}\n  {4:PAYLOAD}');
    expect(screen.getByLabelText('Comment (optional)')).toHaveAttribute('placeholder', '');
    expect(screen.getByRole('dialog')).toHaveAttribute('data-width', '90vw');
    expect(screen.getByRole('dialog')).toHaveAttribute('data-max-width', '900');
    expect(props.onChanged).toHaveBeenCalledOnce();
    expect(approveReview).not.toHaveBeenCalled();
    expect(rejectReview).not.toHaveBeenCalled();
  });

  it('does not start a review for read-only viewing', async () => {
    open({ canApprove: false, canReject: false });
    expect(await screen.findByLabelText('Raw message content')).toBeInTheDocument();
    expect(startReview).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel review' })).toBeDisabled();
  });

  it('does not duplicate start during StrictMode effect replay', async () => {
    open({}, true);
    await ready();
    expect(startReview).toHaveBeenCalledOnce();
  });

  it('retries an uncertain start before enabling decisions', async () => {
    startReview.mockRejectedValueOnce(new Error('Response lost')).mockResolvedValueOnce(undefined);
    open();
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to start or resume');
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Retry review' }));
    await ready();
    expect(startReview).toHaveBeenCalledTimes(2);
    expect(approveReview).not.toHaveBeenCalled();
  });

  it('closing the window leaves the started review active', async () => {
    const props = open();
    await ready();
    expect(screen.getByText(/Closing this window keeps the message assigned/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(props.onClose).toHaveBeenCalledOnce();
    expect(startReview).toHaveBeenCalledOnce();
    expect(approveReview).not.toHaveBeenCalled();
    expect(rejectReview).not.toHaveBeenCalled();
    expect(cancelReview).not.toHaveBeenCalled();
  });

  it('approves without starting again and trims the comment', async () => {
    const props = open({ message: { ...baseMessage, state: 'WaitingForSecondReview' } });
    await ready();
    fireEvent.change(screen.getByLabelText('Comment (optional)'), { target: { value: '  confirmed  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(approveReview).toHaveBeenCalledWith(42, 2, 'confirmed'));
    expect(startReview).toHaveBeenCalledExactlyOnceWith(42, 2);
    expect(props.onChanged).toHaveBeenCalledTimes(2);
    expect(props.onClose).toHaveBeenCalledOnce();
    expect(notify).toHaveBeenCalledWith('Message MSG-0042 approved.', 'success', 4000);
  });

  it('resumes and rejects an active third-level review', async () => {
    const props = open({ message: { ...baseMessage, state: 'ThirdReviewInProgress' } });
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Reject' }));
    await waitFor(() => expect(rejectReview).toHaveBeenCalledWith(42, 3, null));
    expect(startReview).toHaveBeenCalledExactlyOnceWith(42, 3);
    expect(props.onClose).toHaveBeenCalledOnce();
  });

  it('keeps a failed decision retryable only after verifying the review is still active', async () => {
    approveReview.mockRejectedValueOnce(new ApiError('Please try again.', 409));
    const props = open();
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Please try again.');
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(approveReview).toHaveBeenCalledTimes(2));
    expect(startReview).toHaveBeenCalledOnce();
    expect(props.onClose).toHaveBeenCalledOnce();
  });

  it.each(['Completed', 'WaitingForSecondReview', 'Rejected'] as const)(
    'does not repeat a decision after a lost response when the server state is %s', async (state) => {
      const props = open();
      await ready();
      getMessage.mockResolvedValue({ body: 'RAW', state });
      approveReview.mockRejectedValue(new Error('Response lost'));
      fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
      expect(await screen.findByText(/This review is no longer active/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
      expect(props.onChanged).toHaveBeenCalledTimes(2);
      expect(notify).not.toHaveBeenCalled();
    },
  );

  it('requires recovery when the current state cannot be retrieved', async () => {
    open();
    await ready();
    approveReview.mockRejectedValue(new Error('Response lost'));
    getMessage.mockRejectedValue(new Error('Offline'));
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText(/Unable to verify the review state/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Retry review' }));
    await ready();
    expect(startReview).toHaveBeenCalledTimes(2);
  });

  it('keeps decisions disabled if loading the message fails and supports retry', async () => {
    getMessage.mockRejectedValueOnce(new Error('Network')).mockResolvedValueOnce({ body: 'RAW', state: 'FirstReviewInProgress' });
    render(<ReviewDecisionPopup canApprove canReject message={{ ...baseMessage, state: 'Assigned' }}
      onClose={vi.fn()} onChanged={vi.fn()} />, false);
    expect(await screen.findByText('Unable to load raw message')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await ready();
    expect(startReview).toHaveBeenCalledOnce();
  });

  it.each([
    ['Assigned', 1], ['WaitingForSecondReview', 2], ['ThirdReviewInProgress', 3],
  ] as const)('cancels the current review from %s and refreshes the grid', async (state, level) => {
    const props = open({ message: { ...baseMessage, state } });
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    await waitFor(() => expect(props.onClose).toHaveBeenCalledOnce());
    expect(cancelReview).toHaveBeenCalledExactlyOnceWith(42, level);
    expect(props.onChanged).toHaveBeenCalledTimes(2);
    expect(approveReview).not.toHaveBeenCalled();
    expect(rejectReview).not.toHaveBeenCalled();
    expect(notify).toHaveBeenCalledWith('Review for message MSG-0042 cancelled.', 'success', 4000);
  });

  it('allows cancellation even if the raw message failed to load', async () => {
    getMessage.mockRejectedValue(new Error('Offline'));
    render(<ReviewDecisionPopup canApprove canReject message={{ ...baseMessage, state: 'Assigned' }}
      onClose={vi.fn()} onChanged={vi.fn()} />, false);
    await screen.findByText('Unable to load raw message');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Cancel review' })).toBeEnabled());
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    await waitFor(() => expect(cancelReview).toHaveBeenCalledOnce());
  });

  it('prevents duplicate cancellation and decisions while cancellation is pending', async () => {
    cancelReview.mockReturnValue(new Promise(() => undefined));
    open();
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    for (const name of ['Cancelling…', 'Approve', 'Reject', 'Close'])
      expect(screen.getByRole('button', { name })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Cancelling…' }));
    expect(cancelReview).toHaveBeenCalledOnce();
  });

  it('does not restart a cancelled review when the cancellation response is lost', async () => {
    const props = open();
    await ready();
    cancelReview.mockRejectedValue(new Error('Response lost'));
    getMessage.mockResolvedValue({ body: 'RAW', state: 'Assigned' });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    await screen.findByText(/This review is no longer active/);
    expect(screen.getByRole('button', { name: 'Cancel review' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    expect(startReview).toHaveBeenCalledOnce();
    expect(props.onChanged).toHaveBeenCalledTimes(2);
    expect(props.onClose).not.toHaveBeenCalled();
  });

  it('requires refreshing the grid when cancellation cannot be verified', async () => {
    open();
    await ready();
    cancelReview.mockRejectedValue(new Error('Response lost'));
    getMessage.mockRejectedValue(new Error('Offline'));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    await screen.findByText(/Unable to verify cancellation/);
    expect(screen.queryByRole('button', { name: 'Retry review' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel review' })).toBeDisabled();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Close' })).toBeEnabled());
    expect(startReview).toHaveBeenCalledOnce();
  });

  it('allows retrying cancellation after verifying the review is still active', async () => {
    cancelReview.mockRejectedValueOnce(new Error('Offline'));
    const props = open();
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    await screen.findByText(/Unable to cancel the review/);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Cancel review' })).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: 'Cancel review' }));
    await waitFor(() => expect(props.onClose).toHaveBeenCalledOnce());
    expect(cancelReview).toHaveBeenCalledTimes(2);
    expect(startReview).toHaveBeenCalledOnce();
  });

  it('starts and approves the third review level', async () => {
    open({ message: { ...baseMessage, state: 'WaitingForThirdReview' } });
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(approveReview).toHaveBeenCalledWith(42, 3, null));
    expect(startReview).toHaveBeenCalledExactlyOnceWith(42, 3);
  });

  it('shows an empty state when the message has no raw content', async () => {
    getMessage.mockResolvedValue({ body: null });
    render(<ReviewDecisionPopup canApprove={false} canReject={false}
      message={{ ...baseMessage, state: 'New' }} onClose={vi.fn()} onChanged={vi.fn()} />, false);
    expect(await screen.findByText('No raw message content available.')).toBeInTheDocument();
  });

  it.each([[false, false], [true, false], [false, true]])(
    'respects decision permissions (approve: %s, reject: %s)', async (canApprove, canReject) => {
      open({ canApprove, canReject });
      if (canApprove || canReject) await screen.findByText(/Review in progress/);
      expect(screen.getByRole('button', { name: 'Approve' })).toHaveProperty('disabled', !canApprove);
      expect(screen.getByRole('button', { name: 'Reject' })).toHaveProperty('disabled', !canReject);
    },
  );

  it('disables decisions and closing while a decision is in flight', async () => {
    approveReview.mockReturnValue(new Promise(() => undefined));
    open();
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(screen.getByRole('button', { name: 'Approve…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled();
  });
});
