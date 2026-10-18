import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMessageAudit } = vi.hoisted(() => ({ getMessageAudit: vi.fn() }));

vi.mock('../../api/auditApi', () => ({ getMessageAudit }));

import type { AuditEventDto } from '../../../../shared/api/generated/contracts.generated';
import { createTestQueryClient } from '../../../../test/createTestQueryClient';
import AuditTrailContent from './AuditTrailContent';

const message = {
  id: 42,
  externalId: 'MSG-0042',
  sender: 'SENDER',
  receiver: 'RECEIVER',
  branchName: 'Paris',
  departmentIds: [20],
  departmentNames: 'CS',
  currentAssigneeName: null,
  activeReviewerName: null,
  workflowDefinitionName: 'Workflow',
  messageType: 'MT103',
  direction: 'Incoming' as const,
  branchId: 10,
  departmentId: 20,
  state: 'New' as const,
  receivedAt: '2026-09-05T08:00:00Z',
  currentAssigneeId: null,
  activeReviewId: null,
  activeReviewLevel: null,
  activeReviewerId: null,
  undoReviewId: null,
  workflowDefinitionId: 1,
  canReview: false,
  canChangeWorkflow: false,
  requiredReviewLevels: [1, 2, 3],
};

function auditEvent(overrides: Partial<AuditEventDto> = {}): AuditEventDto {
  return {
    id: 1,
    eventType: 'MessageAssigned',
    timestamp: '2026-09-05T09:00:00Z',
    oldState: 'New',
    newState: 'Assigned',
    actor: { userId: 1, userName: 'alex.morgan', displayName: 'Alex Morgan' },
    details: { assigneeId: 2 },
    correlationId: 'internal-correlation-id',
    ...overrides,
  };
}

function renderDrawer(undoReviewId: number | null = null, onUndo?: () => void) {
  return {
    ...render(
      <QueryClientProvider client={createTestQueryClient()}>
        <AuditTrailContent
          message={{ ...message, undoReviewId }}
          onUndo={onUndo}
          users={[
            {
              id: 2,
              userName: 'sam.lee',
              displayName: 'Sam Lee',
              branchIds: [10],
              departmentIds: [20],
            },
          ]}
          messageStates={[
            {
              code: 'New',
              label: 'New',
              description: 'Waiting for first review assignment',
              reviewLevel: 1,
              phase: 'Waiting',
              assignedDescription: null,
            },
            {
              code: 'Assigned',
              label: 'Assigned',
              description: 'Waiting for first review assignment',
              reviewLevel: 1,
              phase: 'Assigned',
              assignedDescription: 'Assigned for first review',
            },
          ]}
        />
      </QueryClientProvider>,
    ),
  };
}

describe('AuditTrailContent', () => {
  beforeEach(() => {
    vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([
      new DOMRect(0, 0, 100, 30),
    ] as unknown as DOMRectList);
    getMessageAudit.mockReset();
  });

  it('shows business details and loads the remaining audit events', async () => {
    getMessageAudit.mockImplementation((_messageId: number, skip: number) =>
      Promise.resolve(
        skip === 0
          ? { items: [auditEvent()], totalCount: 2 }
          : {
              items: [
                auditEvent({
                  id: 2,
                  eventType: 'ReviewApproved',
                  details: { reviewLevel: 1, comment: 'Confirmed' },
                }),
              ],
              totalCount: 2,
            },
      ),
    );

    renderDrawer();

    expect(await screen.findByText('Message assigned')).toBeInTheDocument();
    expect(screen.getByText('Alex Morgan')).toBeInTheDocument();
    expect(screen.getByText('New → Assigned')).toBeInTheDocument();
    expect(screen.getByText('Sam Lee')).toBeInTheDocument();
    expect(
      screen.queryByText('internal-correlation-id'),
    ).not.toBeInTheDocument();
    expect(getMessageAudit).toHaveBeenCalledWith(42, 0, 50, expect.anything());

    fireEvent.click(screen.getByRole('button', { name: 'Load more' }));

    expect(await screen.findByText('Review approved')).toBeInTheDocument();
    expect(screen.getByText('Confirmed')).toBeInTheDocument();
    expect(getMessageAudit).toHaveBeenLastCalledWith(
      42,
      1,
      50,
      expect.anything(),
    );
    expect(
      screen.queryByRole('button', { name: 'Load more' }),
    ).not.toBeInTheDocument();
  });

  it('places Undo only beside the exact server-authorized approval', async () => {
    getMessageAudit.mockResolvedValue({
      items: [
        auditEvent({
          id: 1,
          eventType: 'ReviewApproved',
          details: { reviewId: 72, comment: 'Earlier approval' },
        }),
        auditEvent({
          id: 2,
          eventType: 'ReviewApproved',
          details: { reviewId: 73, comment: 'Latest approval' },
        }),
        auditEvent({
          id: 3,
          eventType: 'MessageCompleted',
          details: { reviewId: 73 },
        }),
      ],
      totalCount: 3,
    });
    const onUndo = vi.fn();
    renderDrawer(73, onUndo);
    await screen.findByText('Earlier approval');
    const button = screen.getByRole('button', { name: 'Undo approval' });
    expect(button.closest('article')).toHaveTextContent('Latest approval');
    fireEvent.click(button);
    expect(onUndo).toHaveBeenCalledOnce();
  });

  it('keeps authorized Undo available if history fails to load', async () => {
    getMessageAudit.mockRejectedValue(new Error('Offline'));
    const onUndo = vi.fn();
    renderDrawer(73, onUndo);
    await screen.findByRole('alert');
    fireEvent.click(screen.getByRole('button', { name: 'Undo approval' }));
    expect(onUndo).toHaveBeenCalledOnce();
  });

  it('supports retry after the initial request fails', async () => {
    getMessageAudit
      .mockRejectedValueOnce(new Error('network failure'))
      .mockResolvedValueOnce({ items: [], totalCount: 0 });

    renderDrawer();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to load audit trail',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() =>
      expect(screen.getByText('No audit events found.')).toBeInTheDocument(),
    );
    expect(getMessageAudit).toHaveBeenCalledTimes(2);
  });

  it('reports a next-page error and retries without losing loaded events', async () => {
    getMessageAudit
      .mockResolvedValueOnce({ items: [auditEvent()], totalCount: 2 })
      .mockRejectedValueOnce(new Error('network failure'))
      .mockResolvedValueOnce({
        items: [auditEvent({ id: 2, eventType: 'ReviewApproved' })],
        totalCount: 2,
      });
    renderDrawer();
    fireEvent.click(await screen.findByRole('button', { name: 'Load more' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to load more events.',
    );
    expect(screen.getByText('Message assigned')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('Review approved')).toBeInTheDocument();
  });
});

vi.mock('@smbc/ui', async () => ({
  ...(await import('../../../../test/smbcMock')),
  Button: ({
    children,
    onClick,
    disabled,
  }: {
    children: string;
    onClick: () => void;
    disabled?: boolean;
  }) => (
    <button onClick={onClick} disabled={disabled}>
      {children}
    </button>
  ),
}));
