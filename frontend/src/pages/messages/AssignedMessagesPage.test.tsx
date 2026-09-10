import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
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
vi.mock('devextreme-react/tabs', () => ({
  default: ({
    items,
    selectedItemKeys,
    onItemClick,
    elementAttr,
  }: {
    items: Array<{ id: string; text: string }>;
    selectedItemKeys: string[];
    onItemClick: (event: { itemData: { id: string; text: string } }) => void;
    elementAttr: { 'aria-label': string };
  }) => (
    <div aria-label={elementAttr['aria-label']}>
      {items.map((item) => (
        <button
          key={item.id}
          type="button"
          aria-pressed={selectedItemKeys.includes(item.id)}
          onClick={() => onItemClick({ itemData: item })}
        >
          {item.text}
        </button>
      ))}
    </div>
  ),
}));
import AssignedMessagesPage from './AssignedMessagesPage';
import { createTestQueryClient } from '../../test/createTestQueryClient';

function LocationSearch() {
  return <span data-testid="location-search">{useLocation().search}</span>;
}

function renderPage(initialEntry: string, permissions: string[] = ['message.view', 'review.level1']) {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(['current-user'], {
    userId: 1,
    userName: 'alex.morgan',
    permissions,
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
  beforeEach(() => {
    getMessageGrid.mockClear();
    gridProps.mockClear();
  });

  it.each(['review.level1', 'review.level2', 'review.level3'])('allows direct access with %s', (permission) => {
    renderPage('/messages/assigned?scope=mine', [permission]);
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();
  });

  it.each(['mine', 'departments'])('redirects an assign-only user from scope=%s without loading review data', (scope) => {
    renderPage(`/messages/assigned?scope=${scope}`, ['message.view', 'message.assign']);
    expect(screen.getByText('Assignment page')).toBeInTheDocument();
    expect(gridProps).not.toHaveBeenCalled();
    expect(getMessageGrid).not.toHaveBeenCalled();
  });

  it('redirects a view-only user to the profile', () => {
    renderPage('/messages/assigned?scope=mine', ['message.view']);
    expect(screen.getByText('User profile')).toBeInTheDocument();
    expect(gridProps).not.toHaveBeenCalled();
  });

  it('waits for access verification before creating the review grid', () => {
    render(<QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={['/messages/assigned']}><AssignedMessagesPage /></MemoryRouter>
    </QueryClientProvider>);
    expect(screen.getByRole('status')).toHaveTextContent('Loading review queue');
    expect(gridProps).not.toHaveBeenCalled();
  });

  it('loads messages assigned to or actively reviewed by the current user by default', async () => {
    renderPage('/messages/assigned?scope=mine');

    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveClass('app-page--wide');
    expect(screen.getByRole('button', { name: 'My work' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();

    const dataSource = gridProps.mock.calls.at(-1)?.[0].dataSource;
    expect(gridProps.mock.calls.at(-1)?.[0].enableReviewActions).toBe(true);
    await act(() => dataSource.load({ skip: 0, take: 20 }));

    expect(getMessageGrid).toHaveBeenCalledWith({ skip: 0, take: 20 }, 'mine');
  });

  it('passes the department scope without expanding assignees into grid filters', async () => {
    renderPage('/messages/assigned?scope=departments');

    const dataSource = gridProps.mock.calls.at(-1)?.[0].dataSource;
    expect(gridProps.mock.calls.at(-1)?.[0].enableReviewActions).toBe(false);
    await act(() => dataSource.load({ filter: ['state', '=', 'Assigned'] }));

    expect(getMessageGrid).toHaveBeenCalledWith(
      { filter: ['state', '=', 'Assigned'] },
      'departments',
    );
  });

  it('updates only the scope query parameter when switching tabs', async () => {
    renderPage('/messages/assigned?user=admin&scope=mine');

    fireEvent.click(screen.getByRole('button', { name: 'My departments' }));

    await waitFor(() =>
      expect(screen.getByTestId('location-search')).toHaveTextContent(
        '?user=admin&scope=departments',
      ),
    );
  });

  it('normalizes an unknown scope to mine without dropping other parameters', async () => {
    renderPage('/messages/assigned?user=admin&scope=unknown');

    await waitFor(() =>
      expect(screen.getByTestId('location-search')).toHaveTextContent(
        '?user=admin&scope=mine',
      ),
    );
  });

  it('normalizes the removed assignment queue scope to mine', async () => {
    renderPage('/messages/assigned?scope=assignable', ['message.view', 'message.assign', 'review.level1']);

    expect(screen.queryByRole('button', { name: 'Assignment queue' })).not.toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId('location-search')).toHaveTextContent(
        '?scope=mine',
      ),
    );
  });
});
