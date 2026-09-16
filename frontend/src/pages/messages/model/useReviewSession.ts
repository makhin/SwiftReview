import { useEffect, useEffectEvent, useReducer, useRef } from 'react';
import notify from 'devextreme/ui/notify';
import { ApiError, ApiRequestError } from '../../../shared/api/errors';
import { approveReview, cancelReview, getMessage, rejectReview, startReview, type MessageRow } from '../api/messagesApi';
import { getReviewStep, type ReviewDecision } from './reviewDecision';

type ReviewId = Awaited<ReturnType<typeof startReview>>;
export type ReviewAction = ReviewDecision | 'cancel';
export type ReviewSessionState =
  | { status: 'inactive' }
  | { status: 'starting'; expectedReviewId: ReviewId | null }
  | { status: 'active'; reviewId: ReviewId; error: string | null }
  | { status: 'submitting'; reviewId: ReviewId; decision: ReviewAction; comment: string | null }
  | { status: 'verifying'; reviewId: ReviewId; decision: ReviewAction; error: string }
  | { status: 'blocked'; error: string; recovery: 'resume' | 'close'; expectedReviewId: ReviewId | null }
  | { status: 'completed' };

type Event =
  | { type: 'started'; reviewId: ReviewId }
  | { type: 'submit'; decision: ReviewAction; comment: string | null }
  | { type: 'failed'; error: string; conflict: boolean }
  | { type: 'verified'; sameReview: boolean }
  | { type: 'verificationFailed' }
  | { type: 'startFailed'; error: string }
  | { type: 'retry' }
  | { type: 'completed' };

const changedAttempt = 'This review attempt has changed. Close this window and reopen the message.';
export function reviewSessionReducer(state: ReviewSessionState, event: Event): ReviewSessionState {
  switch (event.type) {
    case 'started':
      if (state.status !== 'starting') return state;
      return state.expectedReviewId !== null && String(state.expectedReviewId) !== String(event.reviewId)
        ? { status: 'blocked', error: changedAttempt, recovery: 'close', expectedReviewId: state.expectedReviewId }
        : { status: 'active', reviewId: event.reviewId, error: null };
    case 'startFailed':
      return state.status === 'starting'
        ? { status: 'blocked', error: event.error, recovery: 'resume', expectedReviewId: state.expectedReviewId } : state;
    case 'retry':
      return state.status === 'blocked' && state.recovery === 'resume'
        ? { status: 'starting', expectedReviewId: state.expectedReviewId } : state;
    case 'submit':
      return state.status === 'active'
        ? { status: 'submitting', reviewId: state.reviewId, decision: event.decision, comment: event.comment } : state;
    case 'failed':
      if (state.status !== 'submitting') return state;
      return event.conflict
        ? { status: 'blocked', error: event.error, recovery: 'close', expectedReviewId: state.reviewId }
        : { status: 'verifying', reviewId: state.reviewId, decision: state.decision, error: event.error };
    case 'verified':
      if (state.status !== 'verifying') return state;
      return event.sameReview
        ? { status: 'active', reviewId: state.reviewId, error: state.error }
        : { status: 'blocked', error: 'This review is no longer active. Close this window and check the updated message and audit trail.', recovery: 'close', expectedReviewId: state.reviewId };
    case 'verificationFailed':
      if (state.status !== 'verifying') return state;
      return { status: 'blocked', expectedReviewId: state.reviewId,
        recovery: state.decision === 'cancel' ? 'close' : 'resume',
        error: state.decision === 'cancel'
          ? 'Unable to verify cancellation. Close this window and refresh the grid before reopening the review.'
          : 'Unable to verify the review state. Retry to resume before making another decision.' };
    case 'completed':
      return state.status === 'submitting' ? { status: 'completed' } : state;
  }
}

type Options = {
  message: MessageRow;
  canApprove: boolean;
  canReject: boolean;
  hasMessage: boolean;
  onChanged: () => void;
  onClose: () => void;
};

function errorText(caught: unknown, fallback: string) {
  return caught instanceof ApiError || caught instanceof ApiRequestError ? caught.message : fallback;
}

// The popup is keyed by message ID: a mounted hook owns exactly one review session.
export function useReviewSession({ message, canApprove, canReject, hasMessage, onChanged, onClose }: Options) {
  const step = getReviewStep(message.state);
  const level = step?.level;
  const needsStart = step?.needsStart;
  const enabled = canApprove || canReject;
  const [state, dispatch] = useReducer(reviewSessionReducer, enabled && level
    ? { status: 'starting', expectedReviewId: null } : { status: 'inactive' });
  // Cache the operation for this state, including StrictMode's effect replay.
  // Session data (review ID, errors and recovery) lives exclusively in the reducer.
  const request = useRef<{ state: ReviewSessionState; promise: Promise<Event> } | null>(null);
  const changed = useEffectEvent(onChanged);
  const closed = useEffectEvent(onClose);

  useEffect(() => {
    if (!level || (state.status !== 'starting' && state.status !== 'submitting' && state.status !== 'verifying')) return;
    let active = true;
    async function run(): Promise<Event> {
      if (state.status === 'starting') {
        try { return { type: 'started', reviewId: await startReview(message.id, level!) }; }
        catch (caught) { return { type: 'startFailed', error: errorText(caught,
          'Unable to start or resume this review. Check your connection and access, then retry.') }; }
      }
      if (state.status === 'submitting') {
        try {
          if (state.decision === 'cancel') await cancelReview(message.id, level!, state.reviewId);
          else if (state.decision === 'approve') await approveReview(message.id, level!, state.comment, state.reviewId);
          else await rejectReview(message.id, level!, state.comment, state.reviewId);
          return { type: 'completed' };
        } catch (caught) {
          return { type: 'failed', conflict: caught instanceof ApiError && caught.status === 409,
            error: errorText(caught, `Unable to ${state.decision} ${state.decision === 'cancel' ? 'the review' : 'the message'}. Check your access and try again.`) };
        }
      }
      if (state.status === 'verifying') {
        try {
          const current = await getMessage(message.id);
          const currentStep = getReviewStep(current.state);
          return { type: 'verified', sameReview: !!currentStep && !currentStep.needsStart &&
            currentStep.level === level };
        } catch { return { type: 'verificationFailed' }; }
      }
      throw new Error('No review operation for this state');
    }
    if (request.current?.state !== state) request.current = { state, promise: run() };
    void request.current.promise.then((event) => {
      if (!active) return;
      dispatch(event);
      if (event.type === 'started' && state.status === 'starting') {
        if (state.expectedReviewId !== null && String(state.expectedReviewId) !== String(event.reviewId)) {
          notify(changedAttempt, 'error', 4000);
        } else {
          if (state.expectedReviewId === null && needsStart) notify(`Review for message ${message.externalId} started.`, 'success', 4000);
          changed();
        }
      } else if (event.type === 'startFailed' || event.type === 'failed') {
        notify(event.error, 'error', 4000);
        if (event.type === 'failed' && event.conflict) changed();
      } else if (event.type === 'verified' || event.type === 'verificationFailed') {
        changed();
      } else if (event.type === 'completed' && state.status === 'submitting') {
        changed();
        closed();
        notify(state.decision === 'cancel' ? `Review for message ${message.externalId} cancelled.`
          : `Message ${message.externalId} ${state.decision === 'approve' ? 'approved' : 'rejected'}.`, 'success', 4000);
      }
    });
    return () => { active = false; };
  }, [state, message.id, message.externalId, level, needsStart]);

  function submit(decision: ReviewAction, comment: string) {
    const allowed = decision === 'cancel' ? enabled : decision === 'approve' ? canApprove : canReject;
    if (!level || !allowed || (decision !== 'cancel' && !hasMessage)) return;
    dispatch({ type: 'submit', decision, comment: comment.trim() || null });
  }

  return { state, submit, retry: () => dispatch({ type: 'retry' }) };
}
