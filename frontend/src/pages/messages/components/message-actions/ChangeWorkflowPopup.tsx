import { Callout, Button, Dialog, Select } from '@smbc/ui';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';

import { useNotify } from '../../../../shared/notifications/notifications';
import { getWorkflowCandidates } from '../../api/messagesApi';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';
import PageError from '../../../../shared/components/feedback/PageError';
import { type MessageRow } from '../../api/messagesApi';
import { useChangeMessageWorkflow } from '../../api/messageMutations';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';

export default function ChangeWorkflowPopup({
  message,
  onClose,
  onChanged,
}: {
  message: MessageRow;
  onClose: () => void;
  onChanged: RefreshData;
}) {
  const notify = useNotify();
  const workflows = useQuery({
    queryKey: ['messages', message.id, 'workflow-candidates'],
    queryFn: ({ signal }) => getWorkflowCandidates(message.id, signal),
  });
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const candidates = workflows.data?.map((workflow) => ({
    ...workflow,
    label: `${workflow.code} — ${workflow.name} — ${workflow.confirmationMode}`,
  }));
  const effectiveId = selectedId ?? candidates?.[0]?.id;
  const changed = effectiveId != null;
  const selected = candidates?.find(
    (workflow) => String(workflow.id) === String(effectiveId),
  );
  const mutation = useChangeMessageWorkflow(message.id, {
    onChanged,
    onSuccess: () => {
      onClose();
      notify('Workflow changed.', 'success', 4000);
    },
    onError: (caught) => notify(errorText(caught), 'error', 4000),
  });
  const pending = mutation.isPending || mutation.isSuccess;
  const error = mutation.error ? errorText(mutation.error) : null;
  function submit() {
    if (!changed || !selected || pending) return;
    mutation.mutate(selected.id);
  }
  return (
    <Dialog
      open
      title="Change workflow"
      showCloseButton={!pending}
      closeOnOutsideClick={!pending}
      width="90vw"
      onClose={() => {
        if (!pending && !mutation.isLocked()) onClose();
      }}
      devExtremeProps={{
        showTitle: true,
        dragEnabled: false,
        maxWidth: 600,
        height: 'auto',
        elementAttr: { 'aria-label': 'Change workflow' },
      }}
    >
      <div className="review-decision-popup__content flex flex-col gap-4 p-4">
        <p>
          Change workflow for <strong>{message.externalId}</strong>. Available
          when there are no reviews or all previous review attempts are
          cancelled or undone.
        </p>
        <p>
          The allowed workflow is determined by the routing matrix for this
          message.
        </p>
        <p>
          The current assignment, branch and departments will stay the same.
        </p>
        {workflows.isPending && <p role="status">Loading workflows…</p>}
        {workflows.isError && (
          <PageError
            title="Unable to load workflows"
            message="Check your connection and retry."
            actionLabel="Retry"
            onAction={() => void workflows.refetch()}
          />
        )}
        {workflows.isSuccess && (
          <>
            <p>
              Current workflow:{' '}
              {message.workflowDefinitionName ??
                message.workflowDefinitionId ??
                'Unknown'}
            </p>
            <label
              className="block mb-1 text-sm font-semibold text-fg"
              htmlFor="message-workflow"
            >
              Workflow
            </label>
            <Select
              options={candidates ?? []}
              value={effectiveId}
              optionValue="id"
              optionLabel="label"
              searchable
              disabled={pending}
              onChange={(event) => setSelectedId(event)}
              devExtremeProps={{
                inputAttr: { id: 'message-workflow', 'aria-label': 'Workflow' },
              }}
            />
            {!candidates?.length && (
              <p>No workflow can be applied in the current state.</p>
            )}
          </>
        )}
        {error && (
          <Callout tone="danger" className="" role="alert">
            {error}
          </Callout>
        )}
        <div className="flex flex-wrap justify-end gap-2">
          <Button
            disabled={!changed || !selected || pending}
            onClick={() => submit()}
            variant="primary"
            children={pending ? 'Saving…' : 'Save workflow'}
          />
          <Button
            disabled={pending}
            onClick={() => {
              if (!mutation.isLocked()) onClose();
            }}
            variant="secondary"
            children="Cancel"
          />
        </div>
      </div>
    </Dialog>
  );
}

function errorText(caught: unknown) {
  return caught instanceof ApiError || caught instanceof ApiRequestError
    ? caught.message
    : 'Unable to confirm the workflow change. Refresh the grid and check the audit trail.';
}
