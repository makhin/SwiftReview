import { QueryClientProvider } from '@tanstack/react-query';
import { createTestQueryClient } from '../../../test/createTestQueryClient';
import { act, renderHook, waitFor } from '@testing-library/react';
import { StrictMode, type PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../shared/api/errors';
import type { MessageRow } from '../api/messagesApi';
import { reviewSessionReducer, useReviewSession, type ReviewSessionState } from './useReviewSession';

const { startReview, approveReview, rejectReview, cancelReview, getMessage, notify } = vi.hoisted(() => ({
  startReview: vi.fn(), approveReview: vi.fn(), rejectReview: vi.fn(), cancelReview: vi.fn(),
  getMessage: vi.fn(), notify: vi.fn(),
}));
vi.mock('../api/messagesApi', () => ({ startReview, approveReview, rejectReview, cancelReview, getMessage }));
vi.mock('devextreme/ui/notify', () => ({ default: notify }));

const message: MessageRow = {
  id: 42, externalId: 'MSG-42', messageType: 'MT103', state: 'Assigned', branchId: 1, departmentId: 1,
  receivedAt: '2026-09-16T10:00:00Z', currentAssigneeId: 1, activeReviewId: null, activeReviewLevel: null,
  activeReviewerId: null, undoReviewId: null, workflowDefinitionId: 1, canReview: true,
  canChangeWorkflow: false, requiredReviewLevels: [1, 2],
};
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

function setup(strict = false) {
  const options = { message, canApprove: true, canReject: true, hasMessage: true, onChanged: vi.fn(), onClose: vi.fn() };
  const client = createTestQueryClient();
  const hook = renderHook(() => useReviewSession(options), {
    wrapper: ({ children }: PropsWithChildren) => <QueryClientProvider client={client}>
      {strict ? <StrictMode>{children}</StrictMode> : children}
    </QueryClientProvider>,
  });
  return { ...hook, ...options };
}

beforeEach(() => {
  vi.resetAllMocks();
  startReview.mockResolvedValue(73);
  approveReview.mockResolvedValue(undefined);
  rejectReview.mockResolvedValue(undefined);
  cancelReview.mockResolvedValue(undefined);
  getMessage.mockResolvedValue({ state: 'FirstReviewInProgress' });
});

describe('reviewSessionReducer', () => {
  it.each<ReviewSessionState>([
    { status: 'inactive' }, { status: 'starting', expectedReviewId: null },
    { status: 'submitting', reviewId: 73, decision: 'approve', comment: null },
    { status: 'verifying', reviewId: 73, decision: 'approve', error: 'Offline' },
    { status: 'blocked', error: 'Conflict', recovery: 'close', expectedReviewId: 73 },
    { status: 'completed' },
  ])('ignores decisions in $status', (state) => {
    expect(reviewSessionReducer(state, { type: 'submit', decision: 'reject', comment: null })).toBe(state);
  });

  it('retains the attempt ID across repeated recovery failures', () => {
    let state: ReviewSessionState = { status: 'verifying', reviewId: 73, decision: 'approve', error: 'Offline' };
    state = reviewSessionReducer(state, { type: 'verificationFailed' });
    state = reviewSessionReducer(state, { type: 'retry' });
    state = reviewSessionReducer(state, { type: 'startFailed', error: 'Still offline' });
    state = reviewSessionReducer(state, { type: 'retry' });
    expect(state).toEqual({ status: 'starting', expectedReviewId: 73 });
    expect(reviewSessionReducer(state, { type: 'started', reviewId: 74 })).toMatchObject({ status: 'blocked', recovery: 'close' });
  });
});

describe('useReviewSession', () => {
  it('shares the start request during StrictMode replay', async () => {
    const start = deferred<number>();
    startReview.mockReturnValue(start.promise);
    const { result, onChanged } = setup(true);
    expect(result.current.state.status).toBe('starting');
    await act(async () => start.resolve(73));
    expect(result.current.state).toEqual({ status: 'active', reviewId: 73, error: null });
    expect(startReview).toHaveBeenCalledOnce();
    expect(onChanged).toHaveBeenCalledOnce();
  });

  it('serializes decisions even when handlers run before React renders', async () => {
    const { result } = setup();
    await waitFor(() => expect(result.current.state.status).toBe('active'));
    approveReview.mockReturnValue(new Promise(() => {}));
    act(() => {
      result.current.submit('approve', '  confirmed  ');
      result.current.submit('reject', '');
      result.current.submit('cancel', '');
    });
    expect(result.current.state.status).toBe('submitting');
    await waitFor(() => expect(approveReview).toHaveBeenCalledExactlyOnceWith(42, 1, 'confirmed', 73));
    expect(rejectReview).not.toHaveBeenCalled();
    expect(cancelReview).not.toHaveBeenCalled();
  });

  it.each(['approve', 'reject', 'cancel'] as const)('blocks %s after 409 without retrying or verifying', async (decision) => {
    const operation = { approve: approveReview, reject: rejectReview, cancel: cancelReview }[decision];
    operation.mockRejectedValue(new ApiError('Stale attempt', 409));
    const { result, onChanged } = setup();
    await waitFor(() => expect(result.current.state.status).toBe('active'));
    act(() => result.current.submit(decision, ''));
    await waitFor(() => expect(result.current.state).toMatchObject({ status: 'blocked', recovery: 'close' }));
    act(() => { result.current.retry(); result.current.submit(decision, ''); });
    expect(operation).toHaveBeenCalledOnce();
    expect(startReview).toHaveBeenCalledOnce();
    expect(getMessage).not.toHaveBeenCalled();
    expect(onChanged).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['still active', 'FirstReviewInProgress', 'active'],
    ['completed decision', 'Completed', 'blocked'],
    ['next review level', 'SecondReviewInProgress', 'blocked'],
  ] as const)('verifies a lost response: %s', async (_, state, expected) => {
    const verification = deferred<{ state: string }>();
    getMessage.mockReturnValue(verification.promise);
    approveReview.mockRejectedValue(new Error('Response lost'));
    const { result } = setup();
    await waitFor(() => expect(result.current.state.status).toBe('active'));
    act(() => result.current.submit('approve', ''));
    await waitFor(() => expect(result.current.state.status).toBe('verifying'));
    act(() => { result.current.submit('reject', ''); result.current.retry(); });
    expect(result.current.state.status).toBe('verifying');
    expect(rejectReview).not.toHaveBeenCalled();
    await act(async () => verification.resolve({ state }));
    expect(result.current.state.status).toBe(expected);
    expect(approveReview).toHaveBeenCalledOnce();
  });

  it.each([73, 74])('recovers connectivity by resuming and checking attempt %s', async (resumedId) => {
    approveReview.mockRejectedValue(new Error('Response lost'));
    getMessage.mockRejectedValue(new Error('Offline'));
    const { result } = setup();
    await waitFor(() => expect(result.current.state.status).toBe('active'));
    act(() => result.current.submit('approve', ''));
    await waitFor(() => expect(result.current.state).toMatchObject({ status: 'blocked', recovery: 'resume' }));
    startReview.mockResolvedValue(resumedId);
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.state.status).toBe(resumedId === 73 ? 'active' : 'blocked'));
    expect(startReview).toHaveBeenCalledTimes(2);
    expect(approveReview).toHaveBeenCalledOnce();
    expect(rejectReview).not.toHaveBeenCalled();
  });

  it('does not resume when cancellation cannot be verified', async () => {
    cancelReview.mockRejectedValue(new Error('Response lost'));
    getMessage.mockRejectedValue(new Error('Offline'));
    const { result } = setup();
    await waitFor(() => expect(result.current.state.status).toBe('active'));
    act(() => result.current.submit('cancel', ''));
    await waitFor(() => expect(result.current.state).toMatchObject({ status: 'blocked', recovery: 'close' }));
    act(() => result.current.retry());
    expect(startReview).toHaveBeenCalledOnce();
  });

  it('ignores late results after unmount', async () => {
    const start = deferred<number>();
    startReview.mockReturnValue(start.promise);
    const { unmount, onChanged } = setup();
    unmount();
    await act(async () => start.resolve(73));
    expect(onChanged).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();
  });
});
