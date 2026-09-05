import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

const { listProps } = vi.hoisted(() => ({ listProps: vi.fn() }));

vi.mock('devextreme-react/list', () => ({
  default: (props: Record<string, unknown>) => {
    listProps(props);
    return <div aria-label="Application pages" />;
  },
}));
vi.mock('../../pages/current-user/currentUserApi', () => ({
  getCurrentUser: vi.fn(() => new Promise(() => undefined)),
}));

import { createTestQueryClient } from '../../test/createTestQueryClient';
import AppNavigation from './AppNavigation';

function LocationPath() {
  return <span data-testid="location">{useLocation().pathname}</span>;
}

function renderNavigation(permissions: string[]) {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(['current-user'], {
    userId: 1,
    userName: 'alex.morgan',
    permissions,
    branches: [10],
    departments: [20],
  });
  const onNavigate = vi.fn();

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/messages']}>
        <AppNavigation onNavigate={onNavigate} />
        <LocationPath />
      </MemoryRouter>
    </QueryClientProvider>,
  );

  return onNavigate;
}

describe('AppNavigation', () => {
  it('selects the current route and navigates through list items', async () => {
    const onNavigate = renderNavigation(['message.access.all-departments']);

    expect(screen.getByRole('navigation', { name: 'Application navigation' }))
      .toBeInTheDocument();
    expect(listProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ path: '/messages', text: 'All messages' }),
          expect.objectContaining({
            path: '/messages/assigned?scope=mine',
            text: 'Assigned messages',
          }),
        ]),
        keyExpr: 'path',
        displayExpr: 'text',
        selectedItemKeys: ['/messages'],
      }),
    );

    const props = listProps.mock.calls.at(-1)?.[0] as {
      onItemClick: (event: { itemData: unknown }) => void;
    };

    await act(() =>
      props.onItemClick({
        itemData: { path: '/me', text: 'Current user', icon: 'user' },
      }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent('/me');
    expect(onNavigate).toHaveBeenCalledOnce();
    expect(listProps).toHaveBeenLastCalledWith(
      expect.objectContaining({ selectedItemKeys: ['/me'] }),
    );
  });

  it('hides the all-messages page from users without administrator access', () => {
    renderNavigation(['message.view']);

    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items).not.toEqual(
      expect.arrayContaining([expect.objectContaining({ path: '/messages' })]),
    );
    expect(items).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ path: '/messages/assigned?scope=mine' }),
      ]),
    );
  });
});
