import { Button } from '@smbc/ui';
import type { MessageRow } from '../../api/messagesApi';
import type { MessageDialog } from './MessageDialogs';

export default function MessageRowActions({
  message,
  open,
  review,
  confirm,
}: {
  message: MessageRow;
  open: (kind: MessageDialog['kind'], message: MessageRow) => void;
  review: boolean;
  confirm: boolean;
}) {
  const primary = confirm
    ? 'confirm'
    : review
      ? 'review'
      : message.canAssign
        ? 'assignment'
        : null;
  const assignmentLabel =
    message.currentAssigneeId == null ? 'Assign' : 'Reassign';
  const primaryLabel =
    primary === 'confirm'
      ? 'Confirm'
      : primary === 'review'
        ? 'Review'
        : assignmentLabel;
  return (
    <div className="message-actions flex items-center gap-2 whitespace-nowrap">
      <Button
        icon="doc"
        variant="secondary"
        onClick={() => open('view', message)}
        devExtremeProps={{ elementAttr: { id: `message-view-${message.id}` } }}
      >
        View
      </Button>
      {primary && (
        <Button
          variant="primary"
          onClick={() => open(primary, message)}
          devExtremeProps={{
            elementAttr: { id: `message-${primary}-${message.id}` },
          }}
        >
          {primaryLabel}
        </Button>
      )}
      {message.canAssign && primary !== 'assignment' && (
        <Button
          variant="secondary"
          onClick={() => open('assignment', message)}
          devExtremeProps={{
            elementAttr: { id: `message-assignment-${message.id}` },
          }}
        >
          {assignmentLabel}
        </Button>
      )}
    </div>
  );
}
