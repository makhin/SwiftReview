import type { MessageRow } from '../../api/messagesApi';
import type {
  UserSummaryDto,
  MessageStateReferenceDto,
} from '../../../../shared/api/generated/contracts.generated';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import ConfirmMessagePopup from '../message-actions/ConfirmMessagePopup';
import MessageViewPopup from '../message-actions/MessageViewPopup';
import ReviewDecisionPopup from '../message-actions/ReviewDecisionPopup';
import AssignmentPopup from '../message-actions/AssignmentPopup';

export type MessageDialog = {
  kind: 'confirm' | 'view' | 'review' | 'assignment';
  message: MessageRow;
};
export default function MessageDialogs({
  dialog,
  close,
  refresh,
  canReview,
  canConfirm,
  allowWorkflow,
  allowUndo,
  users,
  messageStates,
}: {
  dialog: MessageDialog | null;
  close: () => void;
  refresh: RefreshData;
  canReview: (message: MessageRow) => boolean;
  canConfirm: (message: MessageRow) => boolean;
  allowWorkflow: boolean;
  allowUndo: boolean;
  users?: UserSummaryDto[];
  messageStates?: MessageStateReferenceDto[];
}) {
  if (!dialog) return null;
  const { kind, message } = dialog;
  const props = { message, onClose: close, onChanged: refresh };
  switch (kind) {
    case 'confirm':
      return <ConfirmMessagePopup key={message.id} {...props} />;
    case 'view':
      return (
        <MessageViewPopup
          key={message.id}
          {...props}
          canReview={canReview}
          canConfirm={canConfirm}
          allowWorkflow={allowWorkflow}
          allowUndo={allowUndo}
          users={users}
          messageStates={messageStates}
        />
      );
    case 'review':
      return (
        <ReviewDecisionPopup
          key={message.id}
          {...props}
          canApprove={canReview(message)}
          canReject={canReview(message) && message.rejectAllowed === true}
        />
      );
    case 'assignment':
      return <AssignmentPopup key={message.id} {...props} />;
  }
}
