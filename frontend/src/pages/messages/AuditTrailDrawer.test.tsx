import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMessageAudit } = vi.hoisted(() => ({ getMessageAudit: vi.fn() }));

vi.mock('./auditApi', () => ({ getMessageAudit }));

import type { AuditEventDto } from '../../shared/api/generated/contracts.generated';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import AuditTrailDrawer from './AuditTrailDrawer';

const message = {
  id: 42,
  externalId: 'MSG-0042',
  messageType: 'MT103',
  branchId: 10,
  departmentId: 20,
  state: 'New' as const,
  receivedAt: '2026-09-05T08:00:00Z',
  currentAssigneeId: null,
  activeReviewId: null,
  activeReviewLevel: null,
  activeReviewerId: null,
  account: null,
  currency: null,
  amount: null,
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

function renderDrawer(onClose = vi.fn()) {
  return {
    onClose,
    ...render(
      <QueryClientProvider client={createTestQueryClient()}>
        <AuditTrailDrawer
          message={message}
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
            { code: 'New', label: 'New' },
            { code: 'Assigned', label: 'Assigned' },
          ]}
          onClose={onClose}
        />
      </QueryClientProvider>,
    ),
  };
}

describe('AuditTrailDrawer', () => {
  beforeEach(() => {
    getMessageAudit.mockReset();
  });

  it('shows business details and loads the remaining audit events', async () => {
    getMessageAudit.mockImplementation(
      (_messageId: number, skip: number) =>
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
    expect(screen.queryByText('internal-correlation-id')).not.toBeInTheDocument();
    expect(getMessageAudit).toHaveBeenCalledWith(42, 0, 50, expect.anything());

    fireEvent.click(screen.getByRole('button', { name: 'Load more' }));

    expect(await screen.findByText('Review approved')).toBeInTheDocument();
    expect(screen.getByText('Confirmed')).toBeInTheDocument();
    expect(getMessageAudit).toHaveBeenLastCalledWith(42, 1, 50, expect.anything());
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument();
  });

  it('supports retry after the initial request fails', async () => {
    getMessageAudit
      .mockRejectedValueOnce(new Error('network failure'))
      .mockResolvedValueOnce({ items: [], totalCount: 0 });

    renderDrawer();

    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load audit trail');
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(screen.getByText('No audit events found.')).toBeInTheDocument());
    expect(getMessageAudit).toHaveBeenCalledTimes(2);
  });

  it('focuses the close button and calls the close handler', async () => {
    getMessageAudit.mockResolvedValue({ items: [], totalCount: 0 });
    const onClose = vi.fn();

    renderDrawer(onClose);

    const closeButton = screen.getByRole('button', { name: 'Close audit trail' });
    await waitFor(() => expect(closeButton).toHaveFocus());
    fireEvent.click(closeButton);

    expect(onClose).toHaveBeenCalledOnce();
  });

  it('keeps keyboard focus inside the modal drawer', async () => {
    getMessageAudit.mockResolvedValue({ items: [auditEvent()], totalCount: 2 });
    const outsideButton = document.createElement('button');
    document.body.append(outsideButton);

    renderDrawer();

    const closeButton = screen.getByRole('button', { name: 'Close audit trail' });
    const loadMoreButton = await screen.findByRole('button', { name: 'Load more' });
    await waitFor(() => expect(closeButton).toHaveFocus());

    fireEvent.keyDown(closeButton, { key: 'Tab', shiftKey: true });
    expect(loadMoreButton).toHaveFocus();

    fireEvent.keyDown(loadMoreButton, { key: 'Tab' });
    expect(closeButton).toHaveFocus();

    outsideButton.focus();
    expect(closeButton).toHaveFocus();
    outsideButton.remove();
  });
});
