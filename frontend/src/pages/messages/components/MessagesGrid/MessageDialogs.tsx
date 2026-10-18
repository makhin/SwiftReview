import { Drawer } from '@smbc/ui';
import { createPortal } from 'react-dom';
import type { MessageRow } from '../../api/messagesApi';
import type {
  UserSummaryDto,
  MessageStateReferenceDto,
} from '../../../../shared/api/generated/contracts.generated';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import ConfirmMessagePopup from '../message-actions/ConfirmMessagePopup';
import MessagePdfPopup from '../message-actions/MessagePdfPopup';
import ReviewDecisionPopup from '../message-actions/ReviewDecisionPopup';
import ChangeWorkflowPopup from '../message-actions/ChangeWorkflowPopup';
import AssignmentPopup from '../message-actions/AssignmentPopup';
import AuditTrailDrawer from '../AuditTrailDrawer/AuditTrailDrawer';

export type MessageDialog = {
  kind:
    | 'confirm'
    | 'pdf'
    | 'view'
    | 'review'
    | 'workflow'
    | 'assignment'
    | 'audit';
  message: MessageRow;
};
export default function MessageDialogs({
  dialog,
  close,
  refresh,
  canReview,
  users,
  messageStates,
  prefersReducedMotion,
}: {
  dialog: MessageDialog | null;
  close: () => void;
  refresh: RefreshData;
  canReview: (message: MessageRow) => boolean;
  users?: UserSummaryDto[];
  messageStates?: MessageStateReferenceDto[];
  prefersReducedMotion: boolean;
}) {
  if (!dialog) return null;
  const { kind, message } = dialog;
  const props = { message, onClose: close, onChanged: refresh };
  switch (kind) {
    case 'confirm':
      return <ConfirmMessagePopup key={message.id} {...props} />;
    case 'pdf':
      return <MessagePdfPopup key={message.id} {...props} />;
    case 'view':
    case 'review':
      return (
        <ReviewDecisionPopup
          key={`${kind}-${message.id}`}
          {...props}
          canApprove={kind === 'review' && canReview(message)}
          canReject={
            kind === 'review' &&
            canReview(message) &&
            message.rejectAllowed === true
          }
        />
      );
    case 'workflow':
      return <ChangeWorkflowPopup key={message.id} {...props} />;
    case 'assignment':
      return <AssignmentPopup key={message.id} {...props} />;
    case 'audit':
      return createPortal(
        <Drawer
          open
          onOpenChange={(opened) => {
            if (!opened) close();
          }}
          panel={() => (
            <AuditTrailDrawer
              message={message}
              users={users}
              messageStates={messageStates}
              onClose={close}
            />
          )}
          devExtremeProps={{
            className: 'audit-drawer',
            openedStateMode: 'overlap',
            revealMode: 'expand',
            position: 'right',
            minSize: 0,
            maxSize: 440,
            animationEnabled: !prefersReducedMotion,
            shading: true,
            closeOnOutsideClick: true,
          }}
        >
          <div className="w-full h-full" aria-hidden="true" />
        </Drawer>,
        document.body,
      );
  }
}
