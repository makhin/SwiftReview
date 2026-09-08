import { useQuery } from '@tanstack/react-query';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import TextArea from 'devextreme-react/text-area';
import notify from 'devextreme/ui/notify';
import { useState } from 'react';

import { ApiError } from '../../shared/api/errors';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import { approveReview, getMessage, rejectReview, startReview } from './messagesApi';
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

export default function ReviewDecisionPopup({
  canApprove,
  canReject,
  message,
  onClose,
  onChanged,
}: ReviewDecisionPopupProps) {
  const [comment, setComment] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pendingDecision, setPendingDecision] = useState<ReviewDecision | null>(null);
  const isSubmitting = pendingDecision !== null;
  const messageQuery = useQuery({
    queryKey: ['messages', message.id],
    queryFn: ({ signal }) => getMessage(message.id, signal),
  });
  const step = getReviewStep(message.state);
  const [hasStartedReview, setHasStartedReview] = useState(!step?.needsStart);

  async function submit(decision: ReviewDecision) {
    if (!step || isSubmitting || !messageQuery.isSuccess ||
        !(decision === 'approve' ? canApprove : canReject)) {
      return;
    }

    setError(null);
    setPendingDecision(decision);
    let startedDuringSubmit = false;
    let completed = false;

    try {
      if (!hasStartedReview) {
        await startReview(message.id, step.level);
        setHasStartedReview(true);
        startedDuringSubmit = true;
      }

      const normalizedComment = comment.trim() || null;
      if (decision === 'approve') {
        await approveReview(message.id, step.level, normalizedComment);
      } else {
        await rejectReview(message.id, step.level, normalizedComment);
      }

      completed = true;
      onChanged();
      onClose();
      notify(
        `Message ${message.externalId} ${decision === 'approve' ? 'approved' : 'rejected'}.`,
        'success',
        4000,
      );
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 409
        ? caught.message
        : `Unable to ${decision} the message. Check your access and try again.`);
      if (startedDuringSubmit) {
        onChanged();
      }
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
      title="Review message"
      showTitle
      showCloseButton={!isSubmitting}
      hideOnOutsideClick={!isSubmitting}
      dragEnabled={false}
      width="90vw"
      maxWidth={900}
      height="80vh"
      maxHeight={900}
      onHiding={() => {
        if (!isSubmitting) {
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

        {messageQuery.isSuccess && messageQuery.data.body && (
          <pre className="raw-message-popup__body" aria-label="Raw message content">
            {messageQuery.data.body}
          </pre>
        )}

        {messageQuery.isSuccess && !messageQuery.data.body && (
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
            disabled={!step || !canApprove || !messageQuery.isSuccess || isSubmitting}
            onClick={() => void submit('approve')}
          />
          <Button
            text={pendingDecision === 'reject' ? 'Reject…' : 'Reject'}
            type="danger"
            stylingMode="contained"
            disabled={!step || !canReject || !messageQuery.isSuccess || isSubmitting}
            onClick={() => void submit('reject')}
          />
          <Button
            text="Cancel"
            stylingMode="outlined"
            disabled={isSubmitting}
            onClick={onClose}
          />
        </div>
      </div>
    </Popup>
  );
}
