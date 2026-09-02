import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('../api/client', () => ({ apiClient: { GET: getCurrentUser } }));

import MePage from './MePage';

describe('MePage', () => {
  beforeEach(() => {
    getCurrentUser.mockReset();
  });

  it('shows loading state and then the current user profile', async () => {
    let resolveRequest!: (value: unknown) => void;
    getCurrentUser.mockReturnValue(
      new Promise((resolve) => {
        resolveRequest = resolve;
      }),
    );

    render(<MePage />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading current user…');

    resolveRequest({
      data: {
        userId: 42,
        userName: 'Alex Morgan',
        permissions: ['messages.read', 'messages.assign'],
        branches: [10, 20],
        departments: [],
      },
      response: { status: 200 },
    });

    expect(await screen.findByText('Alex Morgan')).toBeInTheDocument();
    expect(screen.getByText('messages.read, messages.assign')).toBeInTheDocument();
    expect(screen.getByText('10, 20')).toBeInTheDocument();
    expect(screen.getByText('None')).toBeInTheDocument();
  });

  it('shows the response status when no user data is returned', async () => {
    getCurrentUser.mockResolvedValue({ data: undefined, response: { status: 403 } });

    render(<MePage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to load the current user (403).',
    );
  });

  it('shows a fallback for non-Error request failures', async () => {
    getCurrentUser.mockRejectedValue('network unavailable');

    render(<MePage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to load the current user.',
    );
  });

  it('aborts the request and ignores failures after unmounting', async () => {
    let signal: AbortSignal | undefined;
    getCurrentUser.mockImplementation((_path, options) => {
      signal = options.signal;
      return new Promise((_, reject) => {
        signal?.addEventListener('abort', () => reject(new Error('aborted')));
      });
    });

    const { unmount } = render(<MePage />);
    unmount();

    await waitFor(() => expect(signal?.aborted).toBe(true));
  });
});
