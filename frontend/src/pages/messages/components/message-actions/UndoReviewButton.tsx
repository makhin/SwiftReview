import { Field, Button, Dialog, TextArea } from '@smbc/ui';
import { useId, useState } from 'react';

import { useNotify } from '../../../../shared/notifications/notifications';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';
import { type MessageRow } from '../../api/messagesApi';
import { useUndoReview } from '../../api/messageMutations';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';

export default function UndoReviewButton({
  message,
  onChanged,
}: {
  message: MessageRow;
  onChanged: RefreshData;
}) {
  const notify = useNotify();
  const [open, setOpen] = useState(false);
  const [comment, setComment] = useState('');
  const commentId = useId();
  const available = message.undoReviewId != null;

  const mutation = useUndoReview(message.id, {
    onChanged,
    onSuccess: () => {
      setOpen(false);
      notify('Approval undone.', 'success', 4000);
    },
    onError: (error) =>
      notify(
        error instanceof ApiError || error instanceof ApiRequestError
          ? error.message
          : 'Unable to confirm the undo result. Refresh the grid and check the audit trail.',
        'error',
        4000,
      ),
  });
  const pending =
    mutation.isPending ||
    (mutation.isSuccess &&
      String(mutation.variables?.reviewId) === String(message.undoReviewId));
  function submit() {
    if (!available || pending || message.undoReviewId == null) return;
    mutation.mutate({
      reviewId: message.undoReviewId,
      comment: comment.trim() || null,
    });
  }

  return (
    <>
      <Button
        icon="undo"
        disabled={!available || pending}
        onClick={() => {
          setComment('');
          setOpen(true);
        }}
        variant="secondary"
        children="Undo"
        devExtremeProps={{ hint: 'Undo the latest approval' }}
      />
      {open && (
        <Dialog
          open
          title="Undo approval"
          showCloseButton={!pending}
          closeOnOutsideClick={!pending}
          width="90vw"
          onClose={() => {
            if (!pending && !mutation.isLocked()) setOpen(false);
          }}
          devExtremeProps={{
            showTitle: true,
            dragEnabled: false,
            maxWidth: 520,
            height: 'auto',
            elementAttr: { 'aria-label': 'Undo approval' },
          }}
        >
          <div className="review-decision-popup__content flex flex-col gap-4 p-4">
            <p>
              Undo the latest approval for <strong>{message.externalId}</strong>
              ? Assignment retention follows the workflow policy.
            </p>
            {message.activeReviewId != null && (
              <p>
                The active next review will also be cancelled. Its history will
                be preserved.
              </p>
            )}
            <Field label="Comment (optional)" id={commentId}>
              <TextArea
                value={comment}
                onChange={(event) => setComment(event)}
                disabled={pending}
                devExtremeProps={{
                  maxLength: 2000,
                  minHeight: 112,
                  inputAttr: {
                    id: commentId,
                    'aria-label': 'Comment (optional)',
                  },
                }}
              />
            </Field>
            <div className="flex flex-wrap justify-end gap-2">
              <Button
                disabled={!available || pending}
                onClick={() => submit()}
                variant="primary"
                children={pending ? 'Undoing…' : 'Confirm undo'}
              />
              <Button
                disabled={pending}
                onClick={() => {
                  if (!mutation.isLocked()) setOpen(false);
                }}
                variant="secondary"
                children="Cancel"
              />
            </div>
          </div>
        </Dialog>
      )}
    </>
  );
}
