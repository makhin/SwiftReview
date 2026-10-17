import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PropsWithChildren } from 'react';
import { createTestQueryClient } from '../../../../test/createTestQueryClient';
import { messageKeys } from '../../../../shared/api/queryKeys';
import { ApiError } from '../../../../shared/api/errors';
import type { MessageRow } from '../../api/messagesApi';
const { confirmReview, getMessage, startReview } = vi.hoisted(() => ({ confirmReview: vi.fn(), getMessage: vi.fn(), startReview: vi.fn() }));
vi.mock('../../api/messagesApi', async (original) => ({ ...await original<typeof import('../../api/messagesApi')>(), confirmReview, getMessage, startReview }));
vi.mock('devextreme/ui/notify', () => ({ default: vi.fn() }));
vi.mock('devextreme-react/popup', () => ({ default: ({ children }: PropsWithChildren) => <section role="dialog">{children}</section> }));
vi.mock('devextreme-react/button', () => ({ default: ({ text, disabled, onClick }: { text: string; disabled: boolean; onClick: () => void }) => <button disabled={disabled} onClick={onClick}>{text}</button> }));
vi.mock('devextreme-react/text-area', () => ({ default: ({ value, disabled, onValueChanged }: { value: string; disabled: boolean; onValueChanged: (event: { value: string }) => void }) => <textarea aria-label="Comment" value={value} disabled={disabled} onChange={(event) => onValueChanged({ value: event.target.value })} /> }));
import ConfirmMessagePopup from './ConfirmMessagePopup';

const message: MessageRow = { id: 42, externalId: 'MSG-42', sender: '', receiver: '', messageType: 'MT199', direction: 'Incoming',
  branchId: 1, branchName: 'Paris', departmentIds: [1], departmentNames: 'CS', state: 'Assigned', receivedAt: '2026-10-17T00:00:00Z',
  currentAssigneeId: 1, currentAssigneeName: 'Actor', activeReviewId: null, activeReviewLevel: null, activeReviewerId: null,
  activeReviewerName: null, undoReviewId: null, workflowDefinitionId: 1, workflowDefinitionName: 'Immediate', canChangeWorkflow: false,
  requiredReviewLevels: [1], version: 'AAAAAAAAAAE=', confirmationMode: 'Immediate', canConfirm: true, confirmationLevel: 1 };
function setup(canConfirm = true) {
  const client = createTestQueryClient();
  const details = { ...message, body: 'Raw message', canConfirm };
  getMessage.mockResolvedValue(details);
  client.setQueryData(messageKeys.detail(42), details);
  const onClose = vi.fn(); const onChanged = vi.fn();
  render(<QueryClientProvider client={client}><ConfirmMessagePopup message={message} onClose={onClose} onChanged={onChanged} /></QueryClientProvider>);
  return { onClose, onChanged };
}
describe('immediate confirmation', () => {
  afterEach(() => vi.resetAllMocks());
  it('sends the displayed version and trimmed comment once without starting a session', async () => {
    let complete!: () => void;
    confirmReview.mockReturnValue(new Promise<void>((resolve) => { complete = resolve; }));
    const { onClose, onChanged } = setup();
    fireEvent.change(screen.getByLabelText('Comment'), { target: { value: ' checked ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    await waitFor(() => expect(confirmReview).toHaveBeenCalledExactlyOnceWith(42, { level: 1, expectedVersion: message.version, comment: 'checked' }));
    expect(screen.getByRole('button', { name: 'Confirming…' })).toBeDisabled();
    expect(startReview).not.toHaveBeenCalled();
    complete();
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    expect(onChanged).toHaveBeenCalledOnce();
  });
  it('refreshes after a version conflict and requires a fresh explicit attempt', async () => {
    confirmReview.mockRejectedValue(new ApiError('Version changed', 409));
    const { onChanged, onClose } = setup();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Version changed');
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(onClose).not.toHaveBeenCalled();
    expect(confirmReview).toHaveBeenCalledOnce();
  });
  it('disables confirmation when refreshed details no longer permit it', () => {
    setup(false);
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
    expect(confirmReview).not.toHaveBeenCalled();
  });
});
