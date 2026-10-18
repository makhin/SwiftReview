import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Dialog, TextArea } from '@smbc/ui';

import { useNotify } from '../../../../shared/notifications/notifications';
import { messageQueryOptions } from '../../api/messageQueries';
import { useConfirmReview } from '../../api/messageMutations';
import type { MessageRow } from '../../api/messagesApi';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';

export default function ConfirmMessagePopup({
  message,
  onClose,
  onChanged,
}: {
  message: MessageRow;
  onClose: () => void;
  onChanged: RefreshData;
}) {
  const notify = useNotify();
  const details = useQuery(messageQueryOptions(message.id));
  const [comment, setComment] = useState('');
  // A failed or uncertain write always requires reopening with fresh data.
  const [attempted, setAttempted] = useState(false);
  const mutation = useConfirmReview(message.id, {
    onChanged,
    onSuccess: () => {
      onClose();
      notify('Message confirmed.', 'success', 4000);
    },
    onError: () => {},
  });
  const current = details.data;
  const busy = mutation.isPending || mutation.isSuccess;
  const allowed =
    details.isSuccess &&
    current?.version &&
    current.canConfirm &&
    current.confirmationLevel != null &&
    !attempted;
  return (
    <Dialog
      open
      title="Confirm message"
      showCloseButton={!busy}
      closeOnOutsideClick={!busy}
      width="90vw"
      onClose={() => {
        if (!busy && !mutation.isLocked()) onClose();
      }}
      devExtremeProps={{ maxWidth: 900, height: '80vh' }}
    >
      <div className="review-decision-popup__content flex flex-col gap-4 p-4">
        <p>{message.externalId}</p>
        {details.isPending && <p role="status">Loading message…</p>}
        {details.isError && (
          <div role="alert">
            Unable to load message.{' '}
            <Button
              onClick={() => void details.refetch()}
              variant="secondary"
              children="Retry"
            />
          </div>
        )}
        {current && (
          <>
            <p>
              Confirmation level: {current.confirmationLevel ?? 'Unavailable'}
            </p>
            {current.body ? (
              <pre
                className="raw-message-popup__body"
                aria-label="Raw message content"
              >
                {current.body}
              </pre>
            ) : (
              <p>No raw message content available.</p>
            )}
            {!current.canConfirm && (
              <p role="alert">
                Confirmation is no longer available. Close this window and
                refresh the messages.
              </p>
            )}
          </>
        )}
        <label
          className="block mb-1 text-sm font-semibold text-fg"
          htmlFor="confirmation-comment"
        >
          Comment (optional)
        </label>
        <TextArea
          value={comment}
          disabled={busy || attempted}
          onChange={(event) => setComment(event)}
          devExtremeProps={{
            maxLength: 2000,
            inputAttr: { id: 'confirmation-comment' },
          }}
        />
        {mutation.error && (
          <p role="alert">
            {mutation.error.message} Close this window and reload before
            confirming again.
          </p>
        )}
        <div className="flex flex-wrap justify-end gap-2">
          <Button
            disabled={!allowed || busy}
            onClick={() => {
              if (
                !allowed ||
                !current?.version ||
                current.confirmationLevel == null
              )
                return;
              setAttempted(true);
              mutation.mutate({
                level: current.confirmationLevel,
                expectedVersion: current.version,
                comment: comment.trim() || null,
              });
            }}
            variant="primary"
            children={busy ? 'Confirming…' : 'Confirm'}
          />
          <Button
            disabled={busy}
            onClick={onClose}
            variant="secondary"
            children="Close"
          />
        </div>
      </div>
    </Dialog>
  );
}
