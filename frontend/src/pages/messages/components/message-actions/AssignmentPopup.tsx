import ActionDialog, {
  DialogActions,
} from '../../../../shared/components/ActionDialog';
import { Field, Callout, Button, Select } from '@smbc/ui';

import { useNotify } from '../../../../shared/notifications/notifications';
import { useState } from 'react';

import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import { useAssignmentCandidates } from '../../api/messageQueries';
import { useAssignMessage } from '../../api/messageMutations';
import { apiErrorText } from '../../../../shared/api/errors';
import { type MessageRow } from '../../api/messagesApi';

type AssignmentPopupProps = {
  message: MessageRow;
  onClose: () => void;
  onChanged: RefreshData;
};

export default function AssignmentPopup({
  message,
  onClose,
  onChanged,
}: AssignmentPopupProps) {
  const notify = useNotify();
  const candidatesQuery = useAssignmentCandidates(message.id);
  const candidates = candidatesQuery.data;
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const reassign = message.currentAssigneeId != null;
  const action = reassign ? 'Reassign' : 'Assign';

  const mutation = useAssignMessage(message.id, {
    onChanged,
    onSuccess: () => {
      notify(
        `Message ${message.externalId} ${reassign ? 'reassigned' : 'assigned'}.`,
        'success',
        4000,
      );
      onClose();
    },
    onError: (caught) =>
      notify(
        apiErrorText(caught, `Unable to ${action.toLowerCase()} the message.`),
        'error',
        4000,
      ),
  });
  const isSubmitting = mutation.isPending || mutation.isSuccess;
  const error = mutation.error
    ? apiErrorText(
        mutation.error,
        `Unable to ${action.toLowerCase()} the message.`,
      )
    : candidatesQuery.error
      ? apiErrorText(candidatesQuery.error, 'Unable to load reviewers.')
      : null;
  function submit() {
    if (selectedId == null || isSubmitting || !candidatesQuery.isSuccess)
      return;
    mutation.mutate({ assignedTo: selectedId, reassign });
  }

  return (
    <ActionDialog
      title={`${action} message`}
      busy={isSubmitting}
      isLocked={mutation.isLocked}
      onClose={onClose}
      devExtremeProps={{
        className: 'assignment-popup',
        elementAttr: { 'aria-label': `${action} message` },
      }}
    >
      <p>
        Select the reviewer for message <strong>{message.externalId}</strong>.
      </p>
      {candidatesQuery.isPending && !error && (
        <p role="status">Loading reviewers…</p>
      )}
      {candidates && candidates.length === 0 && (
        <Callout tone="warning">No eligible reviewers are available.</Callout>
      )}
      {candidates && candidates.length > 0 && (
        <Field label="Reviewer" id="assignment-reviewer">
          <Select
            options={candidates ?? []}
            value={selectedId}
            optionValue="id"
            optionLabel="displayName"
            placeholder=""
            searchable
            disabled={isSubmitting}
            onChange={(event) => setSelectedId(event as number | null)}
            devExtremeProps={{
              inputAttr: {
                id: 'assignment-reviewer',
                'aria-label': 'Reviewer',
              },
            }}
          />
        </Field>
      )}
      {error && (
        <Callout tone="danger" role="alert">
          {error}
        </Callout>
      )}
      <DialogActions>
        <Button
          disabled={
            selectedId == null || isSubmitting || !candidatesQuery.isSuccess
          }
          onClick={() => submit()}
          variant="primary"
          children={isSubmitting ? `${action}…` : action}
        />
        <Button
          disabled={isSubmitting}
          onClick={() => {
            if (!mutation.isLocked()) onClose();
          }}
          variant="secondary"
          children="Cancel"
        />
      </DialogActions>
    </ActionDialog>
  );
}
