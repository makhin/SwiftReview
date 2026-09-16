import { useId, useState } from 'react';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import TextArea from 'devextreme-react/text-area';
import notify from 'devextreme/ui/notify';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';
import { type MessageRow } from '../../api/messagesApi';
import { useUndoReview } from '../../api/messageMutations';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import './message-action-popup.css';

export default function UndoReviewButton({ message, onChanged }: { message: MessageRow; onChanged: RefreshData }) {
  const [open, setOpen] = useState(false);
  const [comment, setComment] = useState('');
  const commentId = useId();
  const available = message.undoReviewId != null &&
    ['WaitingForSecondReview', 'WaitingForThirdReview', 'Completed'].includes(message.state);

  const mutation = useUndoReview(message.id, {
    onChanged,
    onSuccess: () => {
      setOpen(false);
      notify('Approval undone. Assign the message to a reviewer to continue.', 'success', 4000);
    },
    onError: (error) => notify((error instanceof ApiError || error instanceof ApiRequestError) ? error.message
      : 'Unable to confirm the undo result. Refresh the grid and check the audit trail.', 'error', 4000),
  });
  const pending = mutation.isPending || (mutation.isSuccess && String(mutation.variables?.reviewId) === String(message.undoReviewId));
  function submit() {
    if (!available || pending || message.undoReviewId == null) return;
    mutation.mutate({ reviewId: message.undoReviewId, comment: comment.trim() || null });
  }

  return <>
    <Button text="Undo" icon="undo" hint="Undo the latest approval"
      stylingMode="outlined" disabled={!available || pending} onClick={() => { setComment(''); setOpen(true); }} />
    {open && <Popup visible title="Undo approval" showTitle showCloseButton={!pending}
      hideOnOutsideClick={!pending} dragEnabled={false} width="90vw" maxWidth={520} height="auto"
      onHiding={() => { if (!pending && !mutation.isLocked()) setOpen(false); }} elementAttr={{ 'aria-label': 'Undo approval' }}>
      <div className="review-decision-popup__content">
        <p>Undo the latest approval for <strong>{message.externalId}</strong>? This ends any current assignment.
          The reopened level will require a new assignment.</p>
        <div>
          <label className="app-label" htmlFor={commentId}>Comment (optional)</label>
          <TextArea value={comment} onValueChanged={(event) => setComment(event.value)}
            maxLength={2000} minHeight={112} disabled={pending}
            inputAttr={{ id: commentId, 'aria-label': 'Comment (optional)' }} />
        </div>
        <div className="review-decision-popup__actions">
          <Button text={pending ? 'Undoing…' : 'Confirm undo'} type="default" stylingMode="contained"
            disabled={!available || pending} onClick={() => submit()} />
          <Button text="Cancel" stylingMode="outlined" disabled={pending} onClick={() => { if (!mutation.isLocked()) setOpen(false); }} />
        </div>
      </div>
    </Popup>}
  </>;
}
