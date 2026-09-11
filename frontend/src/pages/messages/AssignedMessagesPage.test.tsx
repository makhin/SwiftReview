import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMessageGrid, gridProps } = vi.hoisted(() => ({
  getMessageGrid: vi.fn().mockResolvedValue({ data: [], totalCount: 0 }),
  gridProps: vi.fn(),
}));

vi.mock('./messagesApi', () => ({ getMessageGrid }));
vi.mock('../../shared/api/currentUserApi', () => ({ getCurrentUser: vi.fn(() => new Promise(() => undefined)) }));
vi.mock('./MessagesGrid', () => ({
  default: (props: Record<string, unknown>) => {
    gridProps(props);
    return <div aria-label="Messages" />;
  },
}));
import AssignedMessagesPage from './AssignedMessagesPage';
import { createTestQueryClient } from '../../test/createTestQueryClient';

function LocationSearch() {
  return <span data-testid="location-search">{useLocation().search}</span>;
}

function renderPage(initialEntry: string, permissions: string[] = ['message.view', 'review.level1'], isGlobalAdministrator = false) {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(['current-user'], {
    userId: 1,
    userName: 'alex.morgan',
    permissions,
    isGlobalAdministrator,
    branches: [10],
    departments: [20],
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/messages/assigned" element={<AssignedMessagesPage />} />
          <Route path="/messages" element={<main>Assignment page</main>} />
          <Route path="/me" element={<main>User profile</main>} />
        </Routes>
        <LocationSearch />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('AssignedMessagesPage', () => {
  beforeEach(() => { getMessageGrid.mockClear(); gridProps.mockClear(); });
  it.each([{ permissions: ['message.view'] }, { permissions: ['message.view', 'review.level1'] }, { permissions: ['message.view', 'message.assign'] }])('opens the complete queue with $permissions', async ({ permissions }) => {
    renderPage('/messages/assigned', permissions);
    expect(screen.getByRole('heading', { level: 1, name: 'Message Review' })).toBeInTheDocument();
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    const props = gridProps.mock.calls.at(-1)![0];
    expect(props.enableReviewActions).toBe(true);
    await act(() => props.dataSource.load({ skip: 0, take: 20 }));
    expect(getMessageGrid).toHaveBeenCalledWith({ skip: 0, take: 20 });
  });
  it('allows administrators without roles', () => {
    renderPage('/messages/assigned', [], true);
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();
  });
  it('requires view access for ordinary users', () => {
    renderPage('/messages/assigned', ['review.level1']);
    expect(screen.getByText('User profile')).toBeInTheDocument();
    expect(gridProps).not.toHaveBeenCalled();
  });
  it('waits for access verification', () => {
    render(<QueryClientProvider client={createTestQueryClient()}><MemoryRouter><AssignedMessagesPage /></MemoryRouter></QueryClientProvider>);
    expect(screen.getByRole('status')).toHaveTextContent('Loading message review');
    expect(gridProps).not.toHaveBeenCalled();
  });
  it.each(['mine', 'departments', 'assignable'])('removes the old %s scope without dropping other parameters', async (scope) => {
    renderPage(`/messages/assigned?user=admin&scope=${scope}`);
    await waitFor(() => expect(screen.getByTestId('location-search').textContent).toBe('?user=admin'));
    const props = gridProps.mock.calls.at(-1)![0];
    await act(() => props.dataSource.load({ skip: 0, take: 20 }));
    expect(getMessageGrid).toHaveBeenCalledWith({ skip: 0, take: 20 });
  });
});
