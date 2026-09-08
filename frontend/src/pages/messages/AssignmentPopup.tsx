import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import SelectBox from 'devextreme-react/select-box';
import { useEffect, useState } from 'react';

import type { AssignmentCandidateDto } from '../../shared/api/generated/contracts.generated';
import { ApiError } from '../../shared/api/errors';
import { assignMessage, getAssignmentCandidates, type MessageRow } from './messagesApi';

type AssignmentPopupProps = {
  message: MessageRow;
  onClose: () => void;
  onChanged: () => void;
};

export default function AssignmentPopup({ message, onClose, onChanged }: AssignmentPopupProps) {
  const [candidates, setCandidates] = useState<AssignmentCandidateDto[] | null>(null);
  const [selectedId, setSelectedId] = useState<number | string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const reassign = message.currentAssigneeId != null;
  const action = reassign ? 'Reassign' : 'Assign';

  useEffect(() => {
    const controller = new AbortController();
    void getAssignmentCandidates(message.id, controller.signal)
      .then(setCandidates)
      .catch((caught) => {
        if (!controller.signal.aborted) {
          setError(caught instanceof ApiError ? caught.message : 'Unable to load reviewers.');
        }
      });
    return () => controller.abort();
  }, [message.id]);

  async function submit() {
    if (selectedId == null || isSubmitting) {
      return;
    }
    setError(null);
    setIsSubmitting(true);
    try {
      await assignMessage(message.id, selectedId, reassign);
      onChanged();
      onClose();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : `Unable to ${action.toLowerCase()} the message.`);
      setIsSubmitting(false);
    }
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
      width="min(90vw, 520px)"
      height="auto"
      onHiding={() => {
        if (!isSubmitting) onClose();
      }}
      elementAttr={{ 'aria-label': `${action} message` }}
    >
      <div className="review-decision-popup__content">
        <p>Select the reviewer for message <strong>{message.externalId}</strong>.</p>
        {candidates === null && !error && <p>Loading reviewers…</p>}
        {candidates && candidates.length === 0 && (
          <div className="app-callout app-callout--warning">No eligible reviewers are available.</div>
        )}
        {candidates && candidates.length > 0 && (
          <SelectBox
            items={candidates}
            value={selectedId}
            valueExpr="id"
            displayExpr="displayName"
            label="Reviewer"
            labelMode="floating"
            searchEnabled
            disabled={isSubmitting}
            onValueChanged={(event) => setSelectedId(event.value as number | string | null)}
            inputAttr={{ 'aria-label': 'Reviewer' }}
          />
        )}
        {error && <div className="app-callout app-callout--danger" role="alert">{error}</div>}
        <div className="review-decision-popup__actions">
          <Button text="Cancel" stylingMode="outlined" disabled={isSubmitting} onClick={onClose} />
          <Button
            text={isSubmitting ? `${action}…` : action}
            type="default"
            stylingMode="contained"
            disabled={selectedId == null || isSubmitting}
            onClick={() => void submit()}
          />
        </div>
      </div>
    </Popup>
  );
}
