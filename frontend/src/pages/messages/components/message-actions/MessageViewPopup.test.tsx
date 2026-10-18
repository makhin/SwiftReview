import { QueryClientProvider } from '@tanstack/react-query';
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestQueryClient } from '../../../../test/createTestQueryClient';
import type { MessageRow } from '../../api/messagesApi';

const { getMessage, getMessageGrid, pdf, audit, startReview } = vi.hoisted(
  () => ({
    getMessage: vi.fn(),
    getMessageGrid: vi.fn(),
    pdf: vi.fn(),
    audit: vi.fn(),
    startReview: vi.fn(),
  }),
);
vi.mock('../../api/messagesApi', () => ({ getMessage, getMessageGrid }));
vi.mock('./MessagePdfContent', () => ({
  default: () => {
    pdf();
    return <p>PDF preview</p>;
  },
}));
vi.mock('../AuditTrailDrawer/AuditTrailContent', () => ({
  default: ({ onUndo }: { onUndo?: () => void }) => {
    audit();
    return (
      <div>
        Audit events{onUndo && <button onClick={onUndo}>Undo approval</button>}
      </div>
    );
  },
}));
vi.mock('./AssignmentPopup', () => ({
  default: ({
    onClose,
    onChanged,
  }: {
    onClose: () => void;
    onChanged: () => void;
  }) => (
    <section role="dialog" aria-label="Assign message">
      <button
        onClick={() => {
          onClose();
          onChanged();
        }}
      >
        Save assignment
      </button>
      <button onClick={onClose}>Cancel</button>
    </section>
  ),
}));
vi.mock('./ChangeWorkflowPopup', () => ({
  default: ({
    onClose,
    onChanged,
  }: {
    onClose: () => void;
    onChanged: () => void;
  }) => (
    <section role="dialog" aria-label="Change workflow">
      <button
        onClick={() => {
          onClose();
          onChanged();
        }}
      >
        Save workflow
      </button>
    </section>
  ),
}));
vi.mock('./UndoReviewPopup', () => ({
  default: ({
    onClose,
    onChanged,
  }: {
    onClose: () => void;
    onChanged: () => void;
  }) => (
    <section role="dialog" aria-label="Undo approval">
      <button
        onClick={() => {
          onClose();
          onChanged();
        }}
      >
        Confirm undo
      </button>
    </section>
  ),
}));
vi.mock('./ConfirmMessagePopup', () => ({
  default: () => <section role="dialog" aria-label="Confirm message" />,
}));
vi.mock('./ReviewDecisionPopup', () => ({
  default: () => {
    startReview();
    return <section role="dialog" aria-label="Review message" />;
  },
}));
vi.mock('@smbc/ui', async () => ({
  ...(await import('../../../../test/smbcMock')),
  Button: ({
    children,
    disabled,
    onClick,
  }: PropsWithChildren<{ disabled?: boolean; onClick: () => void }>) => (
    <button disabled={disabled} onClick={onClick}>
      {children}
    </button>
  ),
}));

import MessageViewPopup from './MessageViewPopup';

const message: MessageRow = {
  id: 42,
  externalId: 'MSG-42',
  sender: 'BANK-A',
  receiver: 'BANK-B',
  direction: 'Incoming',
  messageType: 'MT103',
  branchId: 1,
  branchName: 'Paris',
  departmentIds: [20],
  departmentNames: 'Operations',
  state: 'New',
  receivedAt: '2026-10-18T10:00:00Z',
  currentAssigneeId: null,
  currentAssigneeName: null,
  activeReviewId: null,
  activeReviewLevel: null,
  activeReviewerId: null,
  activeReviewerName: null,
  undoReviewId: null,
  workflowDefinitionId: 1,
  workflowDefinitionName: 'Standard',
  requiredReviewLevels: [1, 2],
  canAssign: true,
  canReview: false,
  canConfirm: false,
  canViewAudit: true,
  canChangeWorkflow: true,
};
function setup(
  overrides: Partial<MessageRow> = {},
  allowWorkflow = true,
  allowUndo = true,
) {
  const onChanged = vi.fn();
  const onClose = vi.fn();
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MessageViewPopup
        message={{ ...message, ...overrides }}
        onClose={onClose}
        onChanged={onChanged}
        canReview={(row) => row.canReview === true}
        canConfirm={(row) => row.canConfirm === true}
        allowWorkflow={allowWorkflow}
        allowUndo={allowUndo}
      />
    </QueryClientProvider>,
  );
  return { onChanged, onClose };
}

describe('MessageViewPopup', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getMessage.mockReset().mockResolvedValue({ body: 'SWIFT BODY' });
    getMessageGrid.mockReset().mockResolvedValue({ data: [message] });
  });

  it('loads message content without starting review and loads secondary tabs only on demand', async () => {
    setup({ canReview: true });
    expect(await screen.findByText('SWIFT BODY')).toBeInTheDocument();
    expect(startReview).not.toHaveBeenCalled();
    expect(pdf).not.toHaveBeenCalled();
    expect(audit).not.toHaveBeenCalled();
    expect(
      screen.queryByLabelText('Comment (optional)'),
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: 'PDF' }));
    expect(screen.getByText('PDF preview')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: 'History' }));
    expect(screen.getByText('Audit events')).toBeInTheDocument();
    expect(startReview).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Start review' }));
    expect(
      screen.getByRole('dialog', { name: 'Review message' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('dialog')).toHaveLength(1);
    expect(startReview).toHaveBeenCalled();
  });

  it('prioritizes Confirm and keeps Reassign next to the assignee', () => {
    setup({
      canConfirm: true,
      canReview: true,
      currentAssigneeId: 2,
      currentAssigneeName: 'Sam Lee',
    });
    expect(screen.getByText('Sam Lee')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Reassign' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Start review' }),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(screen.getAllByRole('dialog')).toHaveLength(1);
    expect(
      screen.getByRole('dialog', { name: 'Confirm message' }),
    ).toBeInTheDocument();
  });

  it('does not expose History or mutations without permissions', () => {
    setup(
      {
        canViewAudit: false,
        canAssign: false,
        canReview: false,
        canConfirm: false,
      },
      false,
    );
    expect(
      screen.queryByRole('tab', { name: 'History' }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Change workflow' }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Assign' }),
    ).not.toBeInTheDocument();
  });

  it.each(['Assign', 'Change workflow'])(
    'returns from %s to the same tab with fresh metadata and permissions',
    async (label) => {
      const { onChanged, onClose } = setup();
      fireEvent.click(screen.getByRole('tab', { name: 'History' }));
      fireEvent.click(screen.getByRole('button', { name: label }));
      expect(screen.getAllByRole('dialog')).toHaveLength(1);
      getMessageGrid.mockResolvedValueOnce({
        data: [
          {
            ...message,
            state: 'Assigned',
            currentAssigneeId: 2,
            currentAssigneeName: 'Sam Lee',
            workflowDefinitionName: 'Updated workflow',
            canChangeWorkflow: false,
            canAssign: false,
          },
        ],
      });
      fireEvent.click(
        screen.getByRole('button', {
          name: label === 'Assign' ? 'Save assignment' : 'Save workflow',
        }),
      );
      expect(await screen.findByText('Updated workflow')).toBeInTheDocument();
      expect(screen.getByText('Sam Lee')).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: 'History' })).toHaveAttribute(
        'aria-selected',
        'true',
      );
      expect(
        screen.queryByRole('button', { name: 'Change workflow' }),
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: 'Assign' }),
      ).not.toBeInTheDocument();
      expect(getMessageGrid).toHaveBeenCalledWith({
        filter: ['id', '=', 42],
        take: 1,
      });
      expect(onChanged).toHaveBeenCalledOnce();
      expect(onClose).not.toHaveBeenCalled();
    },
  );

  it('blocks stale actions after a failed refresh, supports retry and removes revoked History access', async () => {
    setup();
    fireEvent.click(screen.getByRole('tab', { name: 'History' }));
    fireEvent.click(screen.getByRole('button', { name: 'Assign' }));
    getMessageGrid.mockRejectedValueOnce(new Error('Offline'));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    const error = await screen.findByRole('alert');
    expect(error).toHaveTextContent('Unable to refresh message');
    expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Change workflow' }),
    ).toBeDisabled();
    getMessageGrid.mockResolvedValueOnce({
      data: [{ ...message, canViewAudit: false }],
    });
    fireEvent.click(within(error).getByRole('button', { name: 'Retry' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Assign' })).toBeEnabled(),
    );
    expect(
      screen.queryByRole('tab', { name: 'History' }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Message' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  it('keeps focus inside the card after changing tabs and closes with Escape', async () => {
    const rects = vi
      .spyOn(HTMLElement.prototype, 'getClientRects')
      .mockReturnValue([new DOMRect(0, 0, 100, 30)] as unknown as DOMRectList);
    const outside = document.createElement('button');
    document.body.append(outside);
    try {
      const { onClose } = setup();
      const first = screen.getByRole('button', { name: 'Change workflow' });
      const last = screen.getByRole('button', { name: 'Close' });
      await waitFor(() => expect(first).toHaveFocus());
      fireEvent.click(screen.getByRole('tab', { name: 'History' }));
      outside.focus();
      expect(first).toHaveFocus();
      fireEvent.keyDown(first, { key: 'Tab', shiftKey: true });
      expect(last).toHaveFocus();
      fireEvent.keyDown(last, { key: 'Tab' });
      expect(first).toHaveFocus();
      fireEvent.keyDown(first, { key: 'Escape' });
      expect(onClose).toHaveBeenCalledOnce();
    } finally {
      outside.remove();
      rects.mockRestore();
    }
  });

  it.each([true, false])(
    'undo returns to History with fresh permissions (audit access: %s)',
    async (canViewAudit) => {
      const { onChanged } = setup({ undoReviewId: 73, canViewAudit });
      expect(
        screen.queryByRole('button', { name: 'Undo approval' }),
      ).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('tab', { name: 'History' }));
      if (!canViewAudit) expect(audit).not.toHaveBeenCalled();
      fireEvent.click(screen.getByRole('button', { name: 'Undo approval' }));
      expect(screen.getAllByRole('dialog')).toHaveLength(1);
      getMessageGrid.mockResolvedValueOnce({
        data: [{ ...message, undoReviewId: null }],
      });
      fireEvent.click(screen.getByRole('button', { name: 'Confirm undo' }));
      await waitFor(() =>
        expect(
          screen.queryByText('Refreshing message…'),
        ).not.toBeInTheDocument(),
      );
      expect(screen.getByRole('tab', { name: 'History' })).toHaveAttribute(
        'aria-selected',
        'true',
      );
      expect(
        screen.queryByRole('button', { name: 'Undo approval' }),
      ).not.toBeInTheDocument();
      expect(onChanged).toHaveBeenCalledOnce();
    },
  );

  it('hides Undo without undo permission even when the row has an approval', () => {
    setup({ undoReviewId: 73 }, true, false);
    fireEvent.click(screen.getByRole('tab', { name: 'History' }));
    expect(
      screen.queryByRole('button', { name: 'Undo approval' }),
    ).not.toBeInTheDocument();
  });

  it('shows a retry for failed body loading', async () => {
    getMessage.mockRejectedValueOnce(new Error('Offline'));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to load raw message',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('SWIFT BODY')).toBeInTheDocument();
  });
});
