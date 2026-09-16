import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import SelectBox from 'devextreme-react/select-box';
import notify from 'devextreme/ui/notify';
import { useState } from 'react';

import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import { useAssignmentCandidates } from '../../api/messageQueries';
import { useAssignMessage } from '../../api/messageMutations';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';
import { type MessageRow } from '../../api/messagesApi';
import './message-action-popup.css';

type AssignmentPopupProps = {
  message: MessageRow;
  onClose: () => void;
  onChanged: RefreshData;
};

export default function AssignmentPopup({ message, onClose, onChanged }: AssignmentPopupProps) {
  const candidatesQuery = useAssignmentCandidates(message.id);
  const candidates = candidatesQuery.data;
  const [selectedId, setSelectedId] = useState<number | string | null>(null);
  const reassign = message.currentAssigneeId != null;
  const action = reassign ? 'Reassign' : 'Assign';

  const mutation = useAssignMessage(message.id, {
    onChanged,
    onSuccess: () => {
      notify(`Message ${message.externalId} ${reassign ? 'reassigned' : 'assigned'}.`, 'success', 4000);
      onClose();
    },
    onError: (caught) => notify(errorText(caught, `Unable to ${action.toLowerCase()} the message.`), 'error', 4000),
  });
  const isSubmitting = mutation.isPending || mutation.isSuccess;
  const error = mutation.error ? errorText(mutation.error, `Unable to ${action.toLowerCase()} the message.`)
    : candidatesQuery.error ? errorText(candidatesQuery.error, 'Unable to load reviewers.') : null;
  function submit() {
    if (selectedId == null || isSubmitting || !candidatesQuery.isSuccess) return;
    mutation.mutate({ assignedTo: selectedId, reassign });
  }

  return (
    <Popup
      className="assignment-popup"
      visible
      title={`${action} message`}
      showTitle
      showCloseButton={!isSubmitting}
      hideOnOutsideClick={!isSubmitting}
      dragEnabled={false}
      width="90vw"
      maxWidth={520}
      height="auto"
      onHiding={() => {
        if (!isSubmitting && !mutation.isLocked()) onClose();
      }}
      elementAttr={{ 'aria-label': `${action} message` }}
    >
      <div className="review-decision-popup__content">
        <p>Select the reviewer for message <strong>{message.externalId}</strong>.</p>
        {candidatesQuery.isPending && !error && <p role="status">Loading reviewers…</p>}
        {candidates && candidates.length === 0 && (
          <div className="app-callout app-callout--warning">No eligible reviewers are available.</div>
        )}
        {candidates && candidates.length > 0 && (
          <div>
            <label className="app-label" htmlFor="assignment-reviewer">Reviewer</label>
            <SelectBox
              items={candidates}
              value={selectedId}
              valueExpr="id"
              displayExpr="displayName"
              placeholder=""
              searchEnabled
              disabled={isSubmitting}
              onValueChanged={(event) => setSelectedId(event.value as number | string | null)}
              inputAttr={{ id: 'assignment-reviewer', 'aria-label': 'Reviewer' }}
            />
          </div>
        )}
        {error && <div className="app-callout app-callout--danger" role="alert">{error}</div>}
        <div className="review-decision-popup__actions">
          <Button text="Cancel" stylingMode="outlined" disabled={isSubmitting} onClick={() => { if (!mutation.isLocked()) onClose(); }} />
          <Button
            text={isSubmitting ? `${action}…` : action}
            type="default"
            stylingMode="contained"
            disabled={selectedId == null || isSubmitting || !candidatesQuery.isSuccess}
            onClick={() => submit()}
          />
        </div>
      </div>
    </Popup>
  );
}

function errorText(caught: unknown, fallback: string) {
  return caught instanceof ApiError || caught instanceof ApiRequestError ? caught.message : fallback;
}
