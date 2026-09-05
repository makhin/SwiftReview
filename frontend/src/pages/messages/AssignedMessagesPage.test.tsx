import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getMessageGrid, gridProps } = vi.hoisted(() => ({
  getMessageGrid: vi.fn().mockResolvedValue({ data: [], totalCount: 0 }),
  gridProps: vi.fn(),
}));

vi.mock('./messagesApi', () => ({ getMessageGrid }));
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
    onSelectedItemKeysChange,
    elementAttr,
  }: {
    items: Array<{ id: string; text: string }>;
    selectedItemKeys: string[];
    onSelectedItemKeysChange: (keys: string[]) => void;
    elementAttr: { 'aria-label': string };
  }) => (
    <div aria-label={elementAttr['aria-label']}>
      {items.map((item) => (
        <button
          key={item.id}
          type="button"
          aria-pressed={selectedItemKeys.includes(item.id)}
          onClick={() => onSelectedItemKeysChange([item.id])}
        >
          {item.text}
        </button>
      ))}
    </div>
  ),
}));

import AssignedMessagesPage from './AssignedMessagesPage';

function LocationSearch() {
  return <span data-testid="location-search">{useLocation().search}</span>;
}

function renderPage(initialEntry: string) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AssignedMessagesPage />
      <LocationSearch />
    </MemoryRouter>,
  );
}

describe('AssignedMessagesPage', () => {
  beforeEach(() => {
    getMessageGrid.mockClear();
    gridProps.mockClear();
  });

  it('loads messages assigned to the current user by default', async () => {
    renderPage('/messages/assigned?scope=mine');

    expect(screen.getByRole('heading', { name: 'Assigned messages' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Assigned to me' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );

    const dataSource = gridProps.mock.calls.at(-1)?.[0].dataSource;
    await act(() => dataSource.load({ skip: 0, take: 20 }));

    expect(getMessageGrid).toHaveBeenCalledWith({ skip: 0, take: 20 }, 'mine');
  });

  it('passes the department scope without expanding assignees into grid filters', async () => {
    renderPage('/messages/assigned?scope=departments');

    const dataSource = gridProps.mock.calls.at(-1)?.[0].dataSource;
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
});
