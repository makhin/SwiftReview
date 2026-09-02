import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../api/errors';
import { createTestQueryClient } from '../test/createTestQueryClient';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('./currentUserApi', () => ({ getCurrentUser }));

import MePage from './MePage';

const currentUser = {
  userId: 42,
  userName: 'Alex Morgan',
  permissions: ['messages.read', 'messages.assign'],
  branches: [10, 20],
  departments: [],
};

function renderPage(queryClient = createTestQueryClient()) {
  return render(
    <QueryClientProvider client={queryClient}>
      <MePage />
    </QueryClientProvider>,
  );
}

describe('MePage', () => {
  beforeEach(() => {
    getCurrentUser.mockReset();
  });

  it('shows loading state and then the current user profile', async () => {
    let resolveRequest!: (value: typeof currentUser) => void;
    getCurrentUser.mockReturnValue(
      new Promise((resolve) => {
        resolveRequest = resolve;
      }),
    );

    renderPage();

    expect(screen.getByRole('status')).toHaveTextContent('Loading current user…');

    resolveRequest(currentUser);

    expect(await screen.findByText('Alex Morgan')).toBeInTheDocument();
    expect(screen.getByText('messages.read, messages.assign')).toBeInTheDocument();
    expect(screen.getByText('10, 20')).toBeInTheDocument();
  });

  it('shows None for empty access lists', async () => {
    getCurrentUser.mockResolvedValue({
      ...currentUser,
      permissions: [],
      branches: [],
      departments: [],
    });

    renderPage();

    expect(await screen.findByText('Alex Morgan')).toBeInTheDocument();
    expect(screen.getAllByText('None')).toHaveLength(3);
  });

  it('shows a client error without automatically retrying', async () => {
    getCurrentUser.mockRejectedValue(
      new ApiError('Unable to load the current user (403).', 403),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to load the current user (403).',
    );
    expect(getCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('lets the user retry a failed request', async () => {
    getCurrentUser
      .mockRejectedValueOnce(new Error('Network unavailable'))
      .mockResolvedValueOnce(currentUser);
    const user = userEvent.setup();

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Network unavailable');
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('Alex Morgan')).toBeInTheDocument();
    expect(getCurrentUser).toHaveBeenCalledTimes(2);
  });

  it('uses fresh cached data when mounted again', async () => {
    getCurrentUser.mockResolvedValue(currentUser);
    const queryClient = createTestQueryClient();
    const firstView = renderPage(queryClient);

    expect(await screen.findByText('Alex Morgan')).toBeInTheDocument();
    firstView.unmount();

    renderPage(queryClient);

    expect(screen.getByText('Alex Morgan')).toBeInTheDocument();
    expect(getCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('aborts the request after unmounting', async () => {
    let signal: AbortSignal | undefined;
    getCurrentUser.mockImplementation((requestSignal) => {
      signal = requestSignal;
      return new Promise(() => undefined);
    });

    const { unmount } = renderPage();
    unmount();

    await waitFor(() => expect(signal?.aborted).toBe(true));
  });
});
