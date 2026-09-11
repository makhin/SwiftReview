import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';
import { referenceDataKeys } from '../../shared/api/referenceDataQueries';
import { createTestQueryClient } from '../../test/createTestQueryClient';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('../../shared/api/currentUserApi', () => ({ getCurrentUser }));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, onClick }: { text: string; onClick: () => void }) =>
    <button type="button" onClick={onClick}>{text}</button>,
}));

import CurrentUserPage from './CurrentUserPage';

const currentUser = {
  userId: 42,
  userName: 'alex.morgan',
  displayName: 'Alex Morgan',
  permissions: ['messages.read', 'messages.assign'],
  branches: [10, 20],
  departments: [30, 40],
  isGlobalAdministrator: false,
  scopes: [
    { branchId: 10, departmentId: 30, roleIds: [1], permissions: ['messages.read', 'messages.assign'] },
    { branchId: 20, departmentId: 40, roleIds: [2], permissions: ['messages.read'] },
  ],
};

function renderPage(queryClient = createTestQueryClient()) {
  queryClient.setQueryData(referenceDataKeys.branches, [
    { id: 10, name: 'London' },
    { id: 20, name: 'Dublin' },
  ]);
  queryClient.setQueryData(referenceDataKeys.departments, [
    { id: 30, name: 'Operations' },
    { id: 40, name: 'Compliance' },
  ]);
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

    expect(await screen.findByRole('heading', { name: 'Alex Morgan' })).toBeInTheDocument();
    expect(screen.getByText('Identity and access details.')).toBeInTheDocument();
    expect(screen.getByText('messages.read, messages.assign')).toBeInTheDocument();
    expect(screen.getByText('London')).toBeInTheDocument();
    expect(screen.getByText('Compliance')).toBeInTheDocument();
    expect(screen.getByLabelText('Access by scope table')).toHaveAttribute('tabindex', '0');
  });

  it('shows no business access for empty scopes', async () => {
    getCurrentUser.mockResolvedValue({
      ...currentUser,
      permissions: [],
      branches: [],
      departments: [],
      scopes: [],
    });

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Alex Morgan' })).toBeInTheDocument();
    expect(screen.getByText('No business access.')).toBeInTheDocument();
  });

  it('shows full access for a global administrator with no scopes', async () => {
    getCurrentUser.mockResolvedValue({ ...currentUser, isGlobalAdministrator: true, permissions: [], scopes: [] });
    renderPage();
    expect(await screen.findByText('Full access to all information and actions across all branches and departments.')).toBeInTheDocument();
    expect(screen.queryByText('No business access.')).not.toBeInTheDocument();
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

    expect(await screen.findByRole('heading', { name: 'Alex Morgan' })).toBeInTheDocument();
    expect(getCurrentUser).toHaveBeenCalledTimes(2);
  });

  it('renders cached data and refreshes access when mounted again', async () => {
    getCurrentUser.mockResolvedValue(currentUser);
    const queryClient = createTestQueryClient();
    const firstView = renderPage(queryClient);

    expect(await screen.findByRole('heading', { name: 'Alex Morgan' })).toBeInTheDocument();
    firstView.unmount();

    renderPage(queryClient);

    expect(screen.getByRole('heading', { name: 'Alex Morgan' })).toBeInTheDocument();
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalledTimes(2));
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
