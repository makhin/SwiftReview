import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PropsWithChildren } from 'react';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import { referenceDataKeys } from '../../shared/api/referenceDataQueries';
import { ApiError } from '../../shared/api/errors';
import type { MessageRow } from './messagesApi';
const { changeMessageWorkflow } = vi.hoisted(() => ({ changeMessageWorkflow: vi.fn() }));
vi.mock('./messagesApi', () => ({ changeMessageWorkflow }));
vi.mock('devextreme/ui/notify', () => ({ default: vi.fn() }));
vi.mock('devextreme-react/popup', () => ({ default: ({ children }: PropsWithChildren) => <section role="dialog">{children}</section> }));
vi.mock('devextreme-react/button', () => ({ default: ({ text, disabled, onClick }: { text: string; disabled: boolean; onClick: () => void }) => <button disabled={disabled} onClick={onClick}>{text}</button> }));
vi.mock('devextreme-react/select-box', () => ({ default: ({ items, value, disabled, onValueChanged }: {
  items: { id: number; label: string }[]; value: number; disabled: boolean; onValueChanged: (event: { value: number }) => void;
}) => <select aria-label="Workflow" value={value} disabled={disabled} onChange={(e) => onValueChanged({ value: Number(e.target.value) })}>
  {items.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}
</select> }));
import ChangeWorkflowPopup from './ChangeWorkflowPopup';
const message: MessageRow = { id: 42, externalId: 'MSG-42', messageType: 'MT199', branchId: 1, departmentId: 1,
  receivedAt: '2026-09-10T10:00:00Z', state: 'Assigned', currentAssigneeId: 1,
  activeReviewId: null, activeReviewLevel: null, activeReviewerId: null, workflowDefinitionId: 1, canChangeWorkflow: true };
function setup() {
  const client = createTestQueryClient();
  client.setQueryData(referenceDataKeys.workflows, [1, 2, 3].map((id) => ({ id, name: `Workflow ${id}`, messageType: 'MT199', isActive: id !== 3, steps: [{ order: 1, reviewLevel: 1, required: true }] })));
  const onClose = vi.fn(); const onChanged = vi.fn();
  render(<QueryClientProvider client={client}><ChangeWorkflowPopup message={message} onClose={onClose} onChanged={onChanged} /></QueryClientProvider>);
  return { onClose, onChanged };
}
describe('ChangeWorkflowPopup', () => {
  afterEach(() => vi.resetAllMocks());
  it('saves an active alternative and prevents duplicate submission', async () => {
    let resolve!: () => void;
    changeMessageWorkflow.mockReturnValue(new Promise<void>((done) => { resolve = done; }));
    const { onClose, onChanged } = setup();
    expect(screen.getByRole('button', { name: 'Save workflow' })).toBeDisabled();
    expect(screen.queryByRole('option', { name: /Workflow 3/ })).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Workflow'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save workflow' }));
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(screen.getByLabelText('Workflow')).toBeDisabled();
    expect(changeMessageWorkflow).toHaveBeenCalledExactlyOnceWith(42, 2);
    resolve();
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    expect(onChanged).toHaveBeenCalledOnce();
  });
  it('shows the server refusal and refreshes stale grid data', async () => {
    changeMessageWorkflow.mockRejectedValue(new ApiError('All review attempts must be cancelled or undone.', 409));
    const { onClose, onChanged } = setup();
    fireEvent.change(screen.getByLabelText('Workflow'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save workflow' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('All review attempts must be cancelled or undone.');
    expect(onClose).not.toHaveBeenCalled();
    expect(onChanged).toHaveBeenCalledOnce();
  });
  it('cancels without changing the workflow', () => {
    const { onClose } = setup();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(changeMessageWorkflow).not.toHaveBeenCalled();
  });
});
