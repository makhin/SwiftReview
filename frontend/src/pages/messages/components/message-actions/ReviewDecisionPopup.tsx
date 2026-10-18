import ActionDialog, {
  DialogActions,
} from '../../../../shared/components/ActionDialog';
import { Field, Callout, Button, TextArea } from '@smbc/ui';

import { useQuery } from '@tanstack/react-query';

import { useState } from 'react';

import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import PageError from '../../../../shared/components/feedback/PageError';
import PageLoading from '../../../../shared/components/feedback/PageLoading';
import { messageQueryOptions } from '../../api/messageQueries';
import type { MessageRow } from '../../api/messagesApi';
import { useReviewSession } from '../../model/useReviewSession';

type ReviewDecisionPopupProps = {
  canApprove: boolean;
  canReject: boolean;
  message: MessageRow;
  onClose: () => void;
  onChanged: RefreshData;
};

export default function ReviewDecisionPopup({
  canApprove,
  canReject,
  message,
  onClose,
  onChanged,
}: ReviewDecisionPopupProps) {
  const [comment, setComment] = useState('');
  const messageQuery = useQuery(messageQueryOptions(message.id));
  const reviewEnabled = canApprove || canReject;
  const { state, submit, retry } = useReviewSession({
    message,
    canApprove,
    canReject,
    hasMessage: messageQuery.isSuccess,
    onChanged,
    onClose,
  });
  const ready = state.status === 'active';
  const isStarting = state.status === 'starting';
  const isSubmitting =
    state.status === 'submitting' || state.status === 'verifying';
  const isBusy = isStarting || isSubmitting || state.status === 'completed';
  const pendingDecision = isSubmitting ? state.decision : null;
  const startError =
    state.status === 'blocked' && state.recovery === 'resume'
      ? state.error
      : null;
  const error = 'error' in state && !startError ? state.error : null;
  const showMessage = !reviewEnabled || ready || isSubmitting;

  return (
    <ActionDialog
      title={reviewEnabled ? 'Review message' : 'View message'}
      busy={isBusy}
      onClose={onClose}
      devExtremeProps={{
        className: 'review-decision-popup raw-message-popup',
        maxWidth: 900,
        height: '80vh',
        maxHeight: 900,
        elementAttr: { 'aria-label': 'Review message' },
      }}
    >
      <p className="raw-message-popup__message-id">{message.externalId}</p>
      <p>Direction: {message.direction ?? 'Unknown'}</p>

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
      {startError && (
        <PageError
          title="Review unavailable"
          message={startError}
          actionLabel="Retry review"
          onAction={retry}
        />
      )}
      {ready && reviewEnabled && (
        <p role="status">
          Review in progress. Closing this window leaves the review and
          assignment active. Cancel review stops this review and allows
          reassignment.
        </p>
      )}

      {messageQuery.isSuccess && showMessage && messageQuery.data.body && (
        <pre
          className="raw-message-popup__body"
          aria-label="Raw message content"
        >
          {messageQuery.data.body}
        </pre>
      )}

      {messageQuery.isSuccess && showMessage && !messageQuery.data.body && (
        <p className="raw-message-popup__empty">
          No raw message content available.
        </p>
      )}

      <Field label="Comment (optional)" id="review-comment">
        <TextArea
          value={comment}
          onChange={(event) => setComment(event)}
          placeholder=""
          disabled={isSubmitting}
          devExtremeProps={{
            maxLength: 2000,
            minHeight: 112,
            inputAttr: {
              id: 'review-comment',
              'aria-label': 'Comment (optional)',
            },
          }}
        />
      </Field>

      {error && (
        <Callout tone="danger" role="alert">
          {error}
        </Callout>
      )}

      <DialogActions>
        <Button
          disabled={
            !ready || !canApprove || !messageQuery.isSuccess || isSubmitting
          }
          onClick={() => submit('approve', comment)}
          variant="primary"
          children={pendingDecision === 'approve' ? 'Approve…' : 'Approve'}
        />
        <Button
          disabled={
            !ready || !canReject || !messageQuery.isSuccess || isSubmitting
          }
          onClick={() => submit('reject', comment)}
          variant="danger"
          children={pendingDecision === 'reject' ? 'Reject…' : 'Reject'}
        />
        <Button
          disabled={!ready || !reviewEnabled || isSubmitting}
          onClick={() => submit('cancel', comment)}
          variant="secondary"
          children={
            pendingDecision === 'cancel' ? 'Cancelling…' : 'Cancel review'
          }
        />
        <Button
          disabled={isBusy}
          onClick={onClose}
          variant="secondary"
          children="Close"
        />
      </DialogActions>
    </ActionDialog>
  );
}
