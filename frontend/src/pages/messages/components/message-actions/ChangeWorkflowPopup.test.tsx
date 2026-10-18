import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PropsWithChildren } from 'react';
import { createTestQueryClient } from '../../../../test/createTestQueryClient';
import { ApiError, ApiRequestError } from '../../../../shared/api/errors';
import type { MessageRow } from '../../api/messagesApi';
const { changeMessageWorkflow, getWorkflowCandidates, notify } = vi.hoisted(() => ({
  notify: vi.fn(), changeMessageWorkflow: vi.fn(), getWorkflowCandidates: vi.fn(),
}));
vi.mock('../../api/messagesApi', () => ({ changeMessageWorkflow, getWorkflowCandidates }));
vi.mock('../../../../shared/notifications/notifications', () => ({ useNotify: () => notify }));



import ChangeWorkflowPopup from './ChangeWorkflowPopup';
const message: MessageRow = { id: 42, externalId: 'MSG-42', sender: "SENDER", receiver: "RECEIVER", branchName: "Paris", departmentIds: [20], departmentNames: "CS", currentAssigneeName: null, activeReviewerName: null, workflowDefinitionName: "Workflow", messageType: 'MT199', direction: 'Incoming', branchId: 1,
  receivedAt: '2026-09-10T10:00:00Z', state: 'Assigned', currentAssigneeId: 1,
  activeReviewId: null, activeReviewLevel: null, activeReviewerId: null, workflowDefinitionId: 1, canChangeWorkflow: true,
  undoReviewId: null, canReview: true, requiredReviewLevels: [1] };
function setup(selectedMessage: MessageRow = message) {
  const client = createTestQueryClient();
  getWorkflowCandidates.mockResolvedValue([{ id: 2, code: 'ROUTED', name: 'Workflow 2', isActive: true, confirmationMode: 'ReviewSession', steps: [] }]);
  client.setQueryData(['messages', selectedMessage.id, 'workflow-candidates'], [{ id: 2, code: 'ROUTED', name: 'Workflow 2', isActive: true, confirmationMode: 'ReviewSession', steps: [] }]);
  const onClose = vi.fn(); const onChanged = vi.fn();
  render(<QueryClientProvider client={client}><ChangeWorkflowPopup message={selectedMessage} onClose={onClose} onChanged={onChanged} /></QueryClientProvider>);
  return { onClose, onChanged };
}
describe('ChangeWorkflowPopup', () => {
  afterEach(() => vi.resetAllMocks());
  it('shows only the workflow returned by server routing', () => {
    setup();
    expect(screen.getAllByRole('option')).toHaveLength(1);
    expect(screen.getByRole('option', { name: /ROUTED.*Workflow 2/ })).toBeInTheDocument();
  });
  it('saves an active alternative and prevents duplicate submission', async () => {
    let resolve!: () => void;
    changeMessageWorkflow.mockReturnValue(new Promise<void>((done) => { resolve = done; }));
    const { onClose, onChanged } = setup();
    expect(screen.getByRole('button', { name: 'Save workflow' })).toBeEnabled();
    expect(screen.queryByRole('option', { name: /Workflow 3/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Workflow 4/ })).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Workflow'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save workflow' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled());
    expect(screen.getByLabelText('Workflow')).toBeDisabled();
    expect(changeMessageWorkflow).toHaveBeenCalledExactlyOnceWith(42, 2);
    resolve();
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    expect(onChanged).toHaveBeenCalledOnce();
    expect(notify).toHaveBeenCalledExactlyOnceWith('Workflow changed.', 'success', 4000);
  });
  it('shows the server refusal and refreshes stale grid data', async () => {
    changeMessageWorkflow.mockRejectedValue(new ApiError('All review attempts must be cancelled or undone.', 409));
    const { onClose, onChanged } = setup();
    fireEvent.change(screen.getByLabelText('Workflow'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save workflow' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('All review attempts must be cancelled or undone.');
    expect(onClose).not.toHaveBeenCalled();
    expect(notify).toHaveBeenCalledExactlyOnceWith('All review attempts must be cancelled or undone.', 'error', 4000);
    expect(onChanged).toHaveBeenCalledOnce();
  });
  it('does not offer a workflow when the server returns no candidates', async () => {
    getWorkflowCandidates.mockResolvedValue([]);
    const client = createTestQueryClient();
    render(<QueryClientProvider client={client}><ChangeWorkflowPopup message={message} onClose={vi.fn()} onChanged={vi.fn()} /></QueryClientProvider>);
    expect(await screen.findByText('No workflow can be applied in the current state.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save workflow' })).toBeDisabled();
  });

  it('cancels without changing the workflow', () => {
    const { onClose } = setup();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(changeMessageWorkflow).not.toHaveBeenCalled();
  });

  it('recovers workflow loading after a transport failure', async () => {
    getWorkflowCandidates.mockRejectedValueOnce(new ApiRequestError('Connection lost.', 'network'))
      .mockResolvedValueOnce([{ id: 2, code: 'ALT', name: 'Alternative', confirmationMode: 'ReviewSession', isActive: true,
        steps: [{ order: 2, reviewLevel: 2, required: true }, { order: 1, reviewLevel: 1, required: true }] }]);
    const client = createTestQueryClient();
    render(<QueryClientProvider client={client}><ChangeWorkflowPopup
      message={message} onClose={vi.fn()} onChanged={vi.fn()} /></QueryClientProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Retry' }));
    expect(await screen.findByRole('option', { name: 'ALT — Alternative — ReviewSession' })).toBeInTheDocument();
    expect(getWorkflowCandidates).toHaveBeenCalledTimes(2);
    expect(changeMessageWorkflow).not.toHaveBeenCalled();
  });

  it('shows an unknown mutation outcome and refreshes the grid', async () => {
    const error = new ApiRequestError('Unable to change workflow. The result is unknown.', 'network', { outcomeUnknown: true });
    changeMessageWorkflow.mockRejectedValue(error);
    const { onClose, onChanged } = setup();
    fireEvent.change(screen.getByLabelText('Workflow'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save workflow' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(error.message);
    expect(onChanged).toHaveBeenCalledOnce();
    expect(onClose).not.toHaveBeenCalled();
    expect(changeMessageWorkflow).toHaveBeenCalledOnce();
  });
});

vi.mock('@smbc/ui', async () => ({ ...await import('../../../../test/smbcMock'),
...(() => ({ Dialog: ({ children }: PropsWithChildren) => <section role="dialog">{children}</section> }))(),
...(() => ({ Button: ({ children, disabled, onClick }: { children: string; disabled: boolean; onClick: () => void }) => <button disabled={disabled} onClick={onClick}>{children}</button> }))(),
...(() => ({ Select: ({ options, value, disabled, onChange }: {
  options: { id: number; label: string }[]; value: number; disabled: boolean; onChange: (event: number) => void;
}) => <select aria-label="Workflow" value={value} disabled={disabled} onChange={(e) => onChange(Number(e.target.value))}>
  {options.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}
</select> }))()
}));
