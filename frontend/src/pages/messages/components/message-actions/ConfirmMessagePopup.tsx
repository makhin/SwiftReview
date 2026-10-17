import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import TextArea from 'devextreme-react/text-area';
import notify from 'devextreme/ui/notify';
import { messageQueryOptions } from '../../api/messageQueries';
import { useConfirmReview } from '../../api/messageMutations';
import type { MessageRow } from '../../api/messagesApi';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';

export default function ConfirmMessagePopup({ message, onClose, onChanged }: {
  message: MessageRow; onClose: () => void; onChanged: RefreshData;
}) {
  const details = useQuery(messageQueryOptions(message.id));
  const [comment, setComment] = useState('');
  // A failed or uncertain write always requires reopening with fresh data.
  const [attempted, setAttempted] = useState(false);
  const mutation = useConfirmReview(message.id, {
    onChanged,
    onSuccess: () => { onClose(); notify('Message confirmed.', 'success', 4000); },
    onError: () => {},
  });
  const current = details.data;
  const busy = mutation.isPending || mutation.isSuccess;
  const allowed = details.isSuccess && current?.version && current.canConfirm && current.confirmationLevel != null && !attempted;
  return <Popup visible title="Confirm message" showCloseButton={!busy} hideOnOutsideClick={!busy}
    width="90vw" maxWidth={900} height="80vh" onHiding={() => { if (!busy && !mutation.isLocked()) onClose(); }}>
    <div className="review-decision-popup__content">
      <p>{message.externalId}</p>
      {details.isPending && <p role="status">Loading message…</p>}
      {details.isError && <div role="alert">Unable to load message. <Button text="Retry" onClick={() => void details.refetch()} /></div>}
      {current && <>
        <p>Confirmation level: {current.confirmationLevel ?? 'Unavailable'}</p>
        {current.body ? <pre className="raw-message-popup__body" aria-label="Raw message content">{current.body}</pre>
          : <p>No raw message content available.</p>}
        {!current.canConfirm && <p role="alert">Confirmation is no longer available. Close this window and refresh the messages.</p>}
      </>}
      <label className="app-label" htmlFor="confirmation-comment">Comment (optional)</label>
      <TextArea value={comment} maxLength={2000} disabled={busy || attempted}
        inputAttr={{ id: 'confirmation-comment' }} onValueChanged={(event) => setComment(event.value)} />
      {mutation.error && <p role="alert">{mutation.error.message} Close this window and reload before confirming again.</p>}
      <div className="review-decision-popup__actions">
        <Button text={busy ? 'Confirming…' : 'Confirm'} type="success" disabled={!allowed || busy}
          onClick={() => {
            if (!allowed || !current?.version || current.confirmationLevel == null) return;
            setAttempted(true);
            mutation.mutate({ level: current.confirmationLevel, expectedVersion: current.version, comment: comment.trim() || null });
          }} />
        <Button text="Close" disabled={busy} onClick={onClose} />
      </div>
    </div>
  </Popup>;
}
