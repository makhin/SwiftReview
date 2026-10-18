import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { MessageRow } from '../../api/messagesApi';
const { getMessagePdf, renderPage, destroy } = vi.hoisted(() => ({
  getMessagePdf: vi.fn(),
  renderPage: vi.fn(),
  destroy: vi.fn(),
}));
vi.mock('pdfjs-dist', () => ({
  GlobalWorkerOptions: {},
  getDocument: () => ({
    destroy,
    promise: Promise.resolve({
      numPages: 1,
      getPage: async () => ({
        getViewport: () => ({ width: 100, height: 100 }),
        render: renderPage,
      }),
    }),
  }),
}));
vi.mock('../../api/messagesApi', () => ({ getMessagePdf }));

import MessagePdfContent from './MessagePdfContent';

describe('PDF preview', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });
  it('renders the authenticated PDF and releases its object URL when closed', async () => {
    const create = vi.fn().mockReturnValue('blob:preview');
    const revoke = vi.fn();
    vi.stubGlobal(
      'URL',
      class extends URL {
        static createObjectURL = create;
        static revokeObjectURL = revoke;
      },
    );
    renderPage.mockReturnValue({ promise: Promise.resolve(), cancel: vi.fn() });
    getMessagePdf.mockResolvedValue({
      arrayBuffer: async () => new ArrayBuffer(10),
    });
    const view = render(
      <MessagePdfContent
        message={{ id: 42, externalId: 'MSG-42' } as MessageRow}
      />,
    );
    await waitFor(() =>
      expect(
        screen.getByRole('link', { name: 'Download PDF' }),
      ).toHaveAttribute('href', 'blob:preview'),
    );
    await waitFor(() => expect(renderPage).toHaveBeenCalled());
    expect(screen.getByRole('img', { name: 'PDF page 1' })).toBeInTheDocument();
    const signal = getMessagePdf.mock.calls.at(-1)![1] as AbortSignal;
    view.unmount();
    expect(signal.aborted).toBe(true);
    expect(revoke).toHaveBeenCalledWith('blob:preview');
    expect(destroy).toHaveBeenCalled();
  });
  it('shows authorization and network errors instead of a broken frame', async () => {
    getMessagePdf.mockRejectedValue(new Error('Access denied'));
    render(
      <MessagePdfContent
        message={{ id: 42, externalId: 'MSG-42' } as MessageRow}
      />,
    );
    expect(await screen.findByRole('alert')).toHaveTextContent('Access denied');
    expect(
      screen.queryByRole('img', { name: 'PDF page 1' }),
    ).not.toBeInTheDocument();
  });
});

vi.mock('@smbc/ui', async () => ({
  ...(await import('../../../../test/smbcMock')),
  ...(() => ({
    Button: ({
      children,
      onClick,
    }: {
      children: string;
      onClick: () => void;
    }) => <button onClick={onClick}>{children}</button>,
  }))(),
}));
