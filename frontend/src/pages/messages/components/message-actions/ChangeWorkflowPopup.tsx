import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import SelectBox from 'devextreme-react/select-box';
import notify from 'devextreme/ui/notify';
import { getWorkflowCandidates } from '../../api/messagesApi';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';
import PageError from '../../../../shared/components/feedback/PageError';
import { type MessageRow } from '../../api/messagesApi';
import { useChangeMessageWorkflow } from '../../api/messageMutations';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import './message-action-popup.css';

export default function ChangeWorkflowPopup({ message, onClose, onChanged }: {
  message: MessageRow; onClose: () => void; onChanged: RefreshData;
}) {
  const workflows = useQuery({ queryKey: ['messages', message.id, 'workflow-candidates'], queryFn: ({ signal }) => getWorkflowCandidates(message.id, signal) });
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const candidates = workflows.data?.map((workflow) => ({
    ...workflow,
    label: `${workflow.code} — ${workflow.name} — ${workflow.confirmationMode}`,
  }));
  const effectiveId = selectedId ?? candidates?.[0]?.id;
  const changed = effectiveId != null;
  const selected = candidates?.find((workflow) => String(workflow.id) === String(effectiveId));
  const mutation = useChangeMessageWorkflow(message.id, {
    onChanged,
    onSuccess: () => { onClose(); notify('Workflow changed.', 'success', 4000); },
    onError: (caught) => notify(errorText(caught), 'error', 4000),
  });
  const pending = mutation.isPending || mutation.isSuccess;
  const error = mutation.error ? errorText(mutation.error) : null;
  function submit() {
    if (!changed || !selected || pending) return;
    mutation.mutate(selected.id);
  }
  return <Popup visible title="Change workflow" showTitle showCloseButton={!pending} hideOnOutsideClick={!pending}
    dragEnabled={false} width="90vw" maxWidth={600} height="auto" onHiding={() => { if (!pending && !mutation.isLocked()) onClose(); }}
    elementAttr={{ 'aria-label': 'Change workflow' }}>
    <div className="review-decision-popup__content">
      <p>Change workflow for <strong>{message.externalId}</strong>. Available when there are no reviews or all previous review attempts are cancelled or undone.</p>
      <p>The allowed workflow is determined by the routing matrix for this message.</p>
      <p>The current assignment, branch and departments will stay the same.</p>
      {workflows.isPending && <p role="status">Loading workflows…</p>}
      {workflows.isError && <PageError title="Unable to load workflows" message="Check your connection and retry."
        actionLabel="Retry" onAction={() => void workflows.refetch()} />}
      {workflows.isSuccess && <>
        <p>Current workflow: {message.workflowDefinitionName ?? message.workflowDefinitionId ?? 'Unknown'}</p>
        <label className="app-label" htmlFor="message-workflow">Workflow</label>
        <SelectBox items={candidates} value={effectiveId} valueExpr="id" displayExpr="label" searchEnabled
          disabled={pending} onValueChanged={(event) => setSelectedId(event.value)}
          inputAttr={{ id: 'message-workflow', 'aria-label': 'Workflow' }} />
        {!candidates?.length && <p>No workflow can be applied in the current state.</p>}
      </>}
      {error && <div className="app-callout app-callout--danger" role="alert">{error}</div>}
      <div className="review-decision-popup__actions">
        <Button text={pending ? 'Saving…' : 'Save workflow'} type="default" disabled={!changed || !selected || pending} onClick={() => submit()} />
        <Button text="Cancel" disabled={pending} onClick={() => { if (!mutation.isLocked()) onClose(); }} />
      </div>
    </div>
  </Popup>;
}

function errorText(caught: unknown) {
  return caught instanceof ApiError || caught instanceof ApiRequestError ? caught.message
    : 'Unable to confirm the workflow change. Refresh the grid and check the audit trail.';
}
