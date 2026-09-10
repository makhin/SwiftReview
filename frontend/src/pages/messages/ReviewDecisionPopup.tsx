import { useQuery } from '@tanstack/react-query';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import TextArea from 'devextreme-react/text-area';
import notify from 'devextreme/ui/notify';
import { useEffect, useEffectEvent, useRef, useState } from 'react';

import { ApiError } from '../../shared/api/errors';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import { approveReview, cancelReview, getMessage, rejectReview, startReview } from './messagesApi';
import type { MessageRow } from './messagesApi';
import { getReviewStep, type ReviewDecision } from './reviewDecision';
import './message-action-popup.css';
import './raw-message-popup.css';

type ReviewDecisionPopupProps = {
  canApprove: boolean;
  canReject: boolean;
  message: MessageRow;
  onClose: () => void;
  onChanged: () => void;
};

type ReviewAction = ReviewDecision | 'cancel';

export default function ReviewDecisionPopup({
  canApprove,
  canReject,
  message,
  onClose,
  onChanged,
}: ReviewDecisionPopupProps) {
  const [comment, setComment] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pendingDecision, setPendingDecision] = useState<ReviewAction | null>(null);
  const isSubmitting = pendingDecision !== null;
  const messageQuery = useQuery({
    queryKey: ['messages', message.id],
    queryFn: ({ signal }) => getMessage(message.id, signal),
  });
  const step = getReviewStep(message.state);
  const reviewEnabled = canApprove || canReject;
  const level = step?.level;
  const [ready, setReady] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const startPromise = useRef<ReturnType<typeof startReview> | null>(null);
  const reviewId = useRef<Awaited<ReturnType<typeof startReview>> | null>(null);
  const changed = useEffectEvent(onChanged);
  const isStarting = reviewEnabled && !!level && !ready && !startError && !error;
  const isBusy = isStarting || isSubmitting;

  useEffect(() => {
    if (!reviewEnabled || !level) return;
    let active = true;
    // Reuse the in-flight request during StrictMode's effect replay.
    startPromise.current ??= startReview(message.id, level);
    void startPromise.current.then((id) => {
      if (active) {
        if (reviewId.current !== null && String(reviewId.current) !== String(id)) {
          setStartError(null);
          setError('This review attempt has changed. Close this window and reopen the message.');
          setReady(false);
          return;
        }
        reviewId.current = id;
        setReady(true); changed();
      }
    }, () => {
      if (active) setStartError('Unable to start or resume this review. Check your connection and access, then retry.');
    });
    return () => { active = false; };
  }, [message.id, level, reviewEnabled, attempt]);

  function retryStart() {
    startPromise.current = null;
    setStartError(null);
    setError(null);
    setAttempt((value) => value + 1);
  }

  async function submit(decision: ReviewAction) {
    const allowed = decision === 'cancel' ? reviewEnabled : decision === 'approve' ? canApprove : canReject;
    if (!step || !ready || reviewId.current === null || isSubmitting || !allowed ||
        (decision !== 'cancel' && !messageQuery.isSuccess)) {
      return;
    }

    setError(null);
    setPendingDecision(decision);
    let completed = false;

    try {
      const normalizedComment = comment.trim() || null;
      if (decision === 'cancel') {
        await cancelReview(message.id, step.level, reviewId.current);
      } else if (decision === 'approve') {
        await approveReview(message.id, step.level, normalizedComment, reviewId.current);
      } else {
        await rejectReview(message.id, step.level, normalizedComment, reviewId.current);
      }

      completed = true;
      onChanged();
      onClose();
      notify(
        decision === 'cancel' ? `Review for message ${message.externalId} cancelled.`
          : `Message ${message.externalId} ${decision === 'approve' ? 'approved' : 'rejected'}.`,
        'success',
        4000,
      );
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 409
        ? caught.message
        : `Unable to ${decision} ${decision === 'cancel' ? 'the review' : 'the message'}. Check your access and try again.`);
      if (caught instanceof ApiError && caught.status === 409) {
        setReady(false);
        onChanged();
        return;
      }
      // The server may have committed the decision even if its response was lost.
      try {
        const current = await getMessage(message.id);
        const currentStep = getReviewStep(current.state);
        if (!currentStep || currentStep.needsStart || currentStep.level !== step.level) {
          setReady(false);
          setError('This review is no longer active. Close this window and check the updated message and audit trail.');
        }
      } catch {
        setReady(false);
        if (decision === 'cancel') {
          setError('Unable to verify cancellation. Close this window and refresh the grid before reopening the review.');
        } else {
          setStartError('Unable to verify the review state. Retry to resume before making another decision.');
        }
      }
      onChanged();
    } finally {
      if (!completed) {
        setPendingDecision(null);
      }
    }
  }

  return (
    <Popup
      className="review-decision-popup raw-message-popup"
      visible
      title={reviewEnabled ? 'Review message' : 'View message'}
      showTitle
      showCloseButton={!isBusy}
      hideOnOutsideClick={!isBusy}
      dragEnabled={false}
      width="90vw"
      maxWidth={900}
      height="80vh"
      maxHeight={900}
      onHiding={() => {
        if (!isBusy) {
          onClose();
        }
      }}
      elementAttr={{ 'aria-label': 'Review message' }}
    >
      <div className="review-decision-popup__content">
        <p className="raw-message-popup__message-id">{message.externalId}</p>

        {messageQuery.isPending && <PageLoading message="Loading raw message…" />}

        {messageQuery.isError && (
          <PageError
            title="Unable to load raw message"
            message="Check your connection and try again."
            actionLabel="Retry"
            onAction={() => void messageQuery.refetch()}
          />
        )}

        {isStarting && <PageLoading message="Starting review…" />}
        {startError && <PageError title="Review unavailable" message={startError} actionLabel="Retry review" onAction={retryStart} />}
        {ready && reviewEnabled && <p role="status">Review in progress. Closing this window leaves the review and assignment active. Cancel review stops this review and allows reassignment.</p>}

        {messageQuery.isSuccess && (!reviewEnabled || ready) && messageQuery.data.body && (
          <pre className="raw-message-popup__body" aria-label="Raw message content">
            {messageQuery.data.body}
          </pre>
        )}

        {messageQuery.isSuccess && (!reviewEnabled || ready) && !messageQuery.data.body && (
          <p className="raw-message-popup__empty">No raw message content available.</p>
        )}

        <div>
          <label className="app-label" htmlFor="review-comment">Comment (optional)</label>
          <TextArea
            value={comment}
            onValueChanged={(event) => setComment(event.value)}
            placeholder=""
            maxLength={2000}
            minHeight={112}
            disabled={isSubmitting}
            inputAttr={{ id: 'review-comment', 'aria-label': 'Comment (optional)' }}
          />
        </div>

        {error && (
          <div className="app-callout app-callout--danger" role="alert">
            {error}
          </div>
        )}

        <div className="review-decision-popup__actions">
          <Button
            text={pendingDecision === 'approve' ? 'Approve…' : 'Approve'}
            type="success"
            stylingMode="contained"
            disabled={!step || !ready || !canApprove || !messageQuery.isSuccess || isSubmitting}
            onClick={() => void submit('approve')}
          />
          <Button
            text={pendingDecision === 'reject' ? 'Reject…' : 'Reject'}
            type="danger"
            stylingMode="contained"
            disabled={!step || !ready || !canReject || !messageQuery.isSuccess || isSubmitting}
            onClick={() => void submit('reject')}
          />
          <Button
            text={pendingDecision === 'cancel' ? 'Cancelling…' : 'Cancel review'}
            stylingMode="outlined"
            disabled={!step || !ready || !reviewEnabled || isSubmitting}
            onClick={() => void submit('cancel')}
          />
          <Button
            text="Close"
            stylingMode="outlined"
            disabled={isBusy}
            onClick={onClose}
          />
        </div>
      </div>
    </Popup>
  );
}
