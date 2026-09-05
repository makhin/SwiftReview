import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMessage } = vi.hoisted(() => ({ getMessage: vi.fn() }));

vi.mock('./messagesApi', () => ({ getMessage }));
vi.mock('devextreme-react/popup', () => ({
  default: ({ children, onHiding, title }: PropsWithChildren<{
    onHiding: () => void;
    title: string;
  }>) => (
    <section role="dialog" aria-label={title}>
      {children}
      <button type="button" onClick={onHiding}>Close</button>
    </section>
  ),
}));

import { createTestQueryClient } from '../../test/createTestQueryClient';
import RawMessagePopup from './RawMessagePopup';

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

function renderPopup(onClose = vi.fn()) {
  return {
    onClose,
    ...render(
      <QueryClientProvider client={createTestQueryClient()}>
        <RawMessagePopup message={message} onClose={onClose} />
      </QueryClientProvider>,
    ),
  };
}

describe('RawMessagePopup', () => {
  beforeEach(() => {
    getMessage.mockReset();
  });

  it('loads and preserves the raw message text', async () => {
    getMessage.mockResolvedValue({ body: '{1:F01RAW}\n  {4:PAYLOAD}' });

    renderPopup();

    expect(screen.getByText('MSG-0042')).toBeInTheDocument();
    expect(await screen.findByLabelText('Raw message content')).toHaveTextContent(
      '{1:F01RAW} {4:PAYLOAD}',
    );
    expect(getMessage).toHaveBeenCalledWith(42, expect.anything());
  });

  it('supports retry after loading fails', async () => {
    getMessage
      .mockRejectedValueOnce(new Error('network failure'))
      .mockResolvedValueOnce({ body: null });

    renderPopup();

    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load raw message');
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() =>
      expect(screen.getByText('No raw message content available.')).toBeInTheDocument(),
    );
    expect(getMessage).toHaveBeenCalledTimes(2);
  });

  it('closes through the popup handler', () => {
    getMessage.mockImplementation(() => new Promise(() => undefined));
    const { onClose } = renderPopup();

    fireEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(onClose).toHaveBeenCalledOnce();
  });
});
