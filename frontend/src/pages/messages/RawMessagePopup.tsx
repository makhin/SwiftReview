import { useQuery } from '@tanstack/react-query';
import Popup from 'devextreme-react/popup';

import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import { getMessage } from './messagesApi';
import type { MessageRow } from './messagesApi';

type RawMessagePopupProps = {
  message: MessageRow;
  onClose: () => void;
};

export default function RawMessagePopup({ message, onClose }: RawMessagePopupProps) {
  const messageQuery = useQuery({
    queryKey: ['messages', message.id],
    queryFn: ({ signal }) => getMessage(message.id, signal),
  });

  return (
    <Popup
      className="raw-message-popup"
      visible
      title="Raw message"
      showTitle
      showCloseButton
      hideOnOutsideClick
      dragEnabled={false}
      width="90vw"
      maxWidth={900}
      height="80vh"
      maxHeight={720}
      onHiding={onClose}
      elementAttr={{ 'aria-label': 'Raw message' }}
    >
      <div className="raw-message-popup__content">
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
      </div>
    </Popup>
  );
}
