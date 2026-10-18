import { Button } from '@smbc/ui';
import ActionMenu from '../../../../shared/components/ActionMenu';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import type { MessageRow } from '../../api/messagesApi';
import type { MessageDialog } from './MessageDialogs';
import UndoReviewButton from '../message-actions/UndoReviewButton';

export default function MessageRowActions({
  message,
  open,
  review,
  confirm,
  undo,
  workflow,
  onChanged,
}: {
  message: MessageRow;
  open: (kind: MessageDialog['kind'], message: MessageRow) => void;
  review: boolean;
  confirm: boolean;
  undo: boolean;
  workflow: boolean;
  onChanged: RefreshData;
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
      <ActionMenu id={`message-more-${message.id}`}>
        {message.canAssign && primary !== 'assignment' && (
          <Button
            variant="secondary"
            onClick={() => open('assignment', message)}
          >
            {assignmentLabel}
          </Button>
        )}
        {review && primary !== 'review' && (
          <Button variant="secondary" onClick={() => open('review', message)}>
            Review
          </Button>
        )}
        <Button variant="secondary" onClick={() => open('pdf', message)}>
          PDF
        </Button>
        {message.canViewAudit && (
          <Button variant="secondary" onClick={() => open('audit', message)}>
            View audit trail
          </Button>
        )}
        {workflow && (
          <Button variant="secondary" onClick={() => open('workflow', message)}>
            Change workflow
          </Button>
        )}
        {undo && (
          <UndoReviewButton
            key={String(message.undoReviewId ?? 'none')}
            message={message}
            onChanged={onChanged}
          />
        )}
      </ActionMenu>
    </div>
  );
}
