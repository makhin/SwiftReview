import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';
import { createTestQueryClient } from '../../test/createTestQueryClient';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('./currentUserApi', () => ({ getCurrentUser }));

import CurrentUserPage from './CurrentUserPage';

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
      <CurrentUserPage />
    </QueryClientProvider>,
  );
}

describe('CurrentUserPage', () => {
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

  it.each([
    {
      status: 401,
      title: 'Authentication required',
      message: 'Please sign in again to view your profile.',
    },
    {
      status: 403,
      title: 'Access denied',
      message: 'You do not have permission to view this profile.',
    },
    {
      status: 503,
      title: 'Profile temporarily unavailable',
      message: 'Please wait a moment and try again.',
    },
  ])('shows safe error content for a $status response', async ({ status, title, message }) => {
    getCurrentUser.mockRejectedValue(new ApiError('Internal backend exception', status));

    renderPage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(title);
    expect(alert).toHaveTextContent(message);
    expect(alert).not.toHaveTextContent('Internal backend exception');
    expect(getCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('lets the user retry a failed request', async () => {
    getCurrentUser
      .mockRejectedValueOnce(new Error('Internal network details'))
      .mockResolvedValueOnce(currentUser);
    const user = userEvent.setup();

    renderPage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Unable to load profile');
    expect(alert).toHaveTextContent('Check your connection and try again.');
    expect(alert).not.toHaveTextContent('Internal network details');
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
