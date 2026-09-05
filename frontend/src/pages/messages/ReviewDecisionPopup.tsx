import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import TextArea from 'devextreme-react/text-area';
import { useState } from 'react';

import { ApiError } from '../../shared/api/errors';
import { approveReview, rejectReview, startReview } from './messagesApi';
import type { MessageRow } from './messagesApi';
import { getReviewStep, type ReviewDecision } from './reviewDecision';

type ReviewDecisionPopupProps = {
  decision: ReviewDecision;
  message: MessageRow;
  onClose: () => void;
  onChanged: () => void;
};

export default function ReviewDecisionPopup({
  decision,
  message,
  onClose,
  onChanged,
}: ReviewDecisionPopupProps) {
  const [comment, setComment] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const actionLabel = decision === 'approve' ? 'Approve' : 'Reject';
  const step = getReviewStep(message.state);
  const [hasStartedReview, setHasStartedReview] = useState(!step?.needsStart);

  async function submit() {
    if (!step || isSubmitting) {
      return;
    }

    setError(null);
    setIsSubmitting(true);
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
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 409
        ? caught.message
        : `Unable to ${decision} the message. Check your access and try again.`);
      if (startedDuringSubmit) {
        onChanged();
      }
    } finally {
      if (!completed) {
        setIsSubmitting(false);
      }
    }
  }

  return (
    <Popup
      className="review-decision-popup"
      visible
      title={`${actionLabel} message`}
      showTitle
      showCloseButton={!isSubmitting}
      hideOnOutsideClick={!isSubmitting}
      dragEnabled={false}
      width="min(90vw, 520px)"
      height="auto"
      onHiding={() => {
        if (!isSubmitting) {
          onClose();
        }
      }}
      elementAttr={{ 'aria-label': `${actionLabel} message` }}
    >
      <div className="review-decision-popup__content">
        <p>
          Confirm that you want to {decision} message <strong>{message.externalId}</strong>.
        </p>

        <TextArea
          value={comment}
          onValueChanged={(event) => setComment(event.value)}
          label="Comment (optional)"
          labelMode="floating"
          maxLength={2000}
          minHeight={112}
          disabled={isSubmitting}
          inputAttr={{ 'aria-label': 'Comment (optional)' }}
        />

        {error && (
          <div className="app-callout app-callout--danger" role="alert">
            {error}
          </div>
        )}

        <div className="review-decision-popup__actions">
          <Button
            text="Cancel"
            stylingMode="outlined"
            disabled={isSubmitting}
            onClick={onClose}
          />
          <Button
            text={isSubmitting ? `${actionLabel}…` : actionLabel}
            type={decision === 'approve' ? 'success' : 'danger'}
            stylingMode="contained"
            disabled={!step || isSubmitting}
            onClick={() => void submit()}
          />
        </div>
      </div>
    </Popup>
  );
}
