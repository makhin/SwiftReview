import { useQuery } from '@tanstack/react-query';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import TextArea from 'devextreme-react/text-area';
import { useState } from 'react';

import PageError from '../../../../shared/components/feedback/PageError';
import PageLoading from '../../../../shared/components/feedback/PageLoading';
import { getMessage } from '../../api/messagesApi';
import type { MessageRow } from '../../api/messagesApi';
import { useReviewSession } from '../../model/useReviewSession';
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
  const messageQuery = useQuery({
    queryKey: ['messages', message.id],
    queryFn: ({ signal }) => getMessage(message.id, signal),
  });
  const reviewEnabled = canApprove || canReject;
  const { state, submit, retry } = useReviewSession({
    message, canApprove, canReject, hasMessage: messageQuery.isSuccess, onChanged, onClose,
  });
  const ready = state.status === 'active';
  const isStarting = state.status === 'starting';
  const isSubmitting = state.status === 'submitting' || state.status === 'verifying';
  const isBusy = isStarting || isSubmitting || state.status === 'completed';
  const pendingDecision = isSubmitting ? state.decision : null;
  const startError = state.status === 'blocked' && state.recovery === 'resume' ? state.error : null;
  const error = 'error' in state && !startError ? state.error : null;
  const showMessage = !reviewEnabled || ready || isSubmitting;

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
        {startError && <PageError title="Review unavailable" message={startError} actionLabel="Retry review" onAction={retry} />}
        {ready && reviewEnabled && <p role="status">Review in progress. Closing this window leaves the review and assignment active. Cancel review stops this review and allows reassignment.</p>}

        {messageQuery.isSuccess && showMessage && messageQuery.data.body && (
          <pre className="raw-message-popup__body" aria-label="Raw message content">
            {messageQuery.data.body}
          </pre>
        )}

        {messageQuery.isSuccess && showMessage && !messageQuery.data.body && (
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
            disabled={!ready || !canApprove || !messageQuery.isSuccess || isSubmitting}
            onClick={() => submit('approve', comment)}
          />
          <Button
            text={pendingDecision === 'reject' ? 'Reject…' : 'Reject'}
            type="danger"
            stylingMode="contained"
            disabled={!ready || !canReject || !messageQuery.isSuccess || isSubmitting}
            onClick={() => submit('reject', comment)}
          />
          <Button
            text={pendingDecision === 'cancel' ? 'Cancelling…' : 'Cancel review'}
            stylingMode="outlined"
            disabled={!ready || !reviewEnabled || isSubmitting}
            onClick={() => submit('cancel', comment)}
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
