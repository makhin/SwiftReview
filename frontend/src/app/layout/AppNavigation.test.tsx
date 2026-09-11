import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

const { listProps } = vi.hoisted(() => ({ listProps: vi.fn() }));

vi.mock('devextreme-react/list', () => ({
  default: (props: Record<string, unknown>) => {
    listProps(props);
    const items = props.items as Array<{ path: string; text: string; icon: string }>;
    const itemRender = props.itemRender as (item: typeof items[number]) => React.ReactNode;
    return <div aria-label="Application pages">{items.map((item) => <div key={item.path}>{itemRender(item)}</div>)}</div>;
  },
}));
vi.mock('../../shared/api/currentUserApi', () => ({
  getCurrentUser: vi.fn(() => new Promise(() => undefined)),
}));

import { createTestQueryClient } from '../../test/createTestQueryClient';
import AppNavigation from './AppNavigation';

function LocationPath() {
  const location = useLocation();
  return <span data-testid="location">{location.pathname}{location.search}</span>;
}

function renderNavigation(permissions: string[], initialEntry = '/messages', isGlobalAdministrator = false) {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(['current-user'], {
    userId: 1,
    userName: 'alex.morgan',
    permissions,
    isGlobalAdministrator,
    branches: [10],
    departments: [20],
  });
  const onNavigate = vi.fn();

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <AppNavigation onNavigate={onNavigate} />
        <LocationPath />
      </MemoryRouter>
    </QueryClientProvider>,
  );

  return onNavigate;
}

describe('AppNavigation', () => {
  it('shows every page to a global administrator without business roles', () => {
    renderNavigation([], '/messages', true);
    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items.map((item) => item.path)).toEqual(expect.arrayContaining(['/messages', '/messages/assigned', '/admin']));
  });
  it.each([
    { permissions: ['message.view'], assign: false, review: true },
    { permissions: ['message.assign'], assign: true, review: false },
    { permissions: ['message.view', 'review.level1'], assign: false, review: true },
    { permissions: ['message.view', 'review.level2'], assign: false, review: true },
    { permissions: ['message.view', 'review.level3'], assign: false, review: true },
    { permissions: ['message.view', 'message.assign', 'review.level2'], assign: true, review: true },
    { permissions: ['review.undo', 'audit.view'], assign: false, review: false },
  ])('shows pages according to $permissions', ({ permissions, assign, review }) => {
    renderNavigation(permissions);
    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items.some((item) => item.path === '/messages')).toBe(assign);
    expect(items.some((item) => item.path === '/messages/assigned')).toBe(review);
  });

  it('selects the current route and navigates through list items', async () => {
    const onNavigate = renderNavigation(['message.view', 'message.assign', 'review.level1']);
    const user = userEvent.setup();

    expect(screen.getByRole('navigation', { name: 'Application navigation' }))
      .toBeInTheDocument();
    expect(listProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ path: '/messages', text: 'Messages' }),
          expect.objectContaining({
            path: '/messages/assigned',
            text: 'Message Review',
            icon: 'todo',
          }),
          expect.objectContaining({ path: '/me', text: 'User profile', icon: 'user' }),
        ]),
        keyExpr: 'path',
        selectionMode: 'none',
      }),
    );

    expect(screen.getByRole('link', { name: 'Messages' })).toHaveAttribute('aria-current', 'page');
    await user.click(screen.getByRole('link', { name: 'User profile' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/me');
    expect(onNavigate).toHaveBeenCalledOnce();
    expect(screen.getByRole('link', { name: 'User profile' })).toHaveAttribute('aria-current', 'page');
  });

  it('hides the assignment page from reviewers without assign permission', () => {
    renderNavigation(['message.view', 'review.level1']);

    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items).not.toEqual(
      expect.arrayContaining([expect.objectContaining({ path: '/messages' })]),
    );
    expect(items).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ path: '/messages/assigned' }),
      ]),
    );
  });

  it('does not show the removed assignment queue', () => {
    renderNavigation(['message.view', 'message.assign']);

    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items).not.toEqual(expect.arrayContaining([
      expect.objectContaining({ path: '/messages/assigned?scope=assignable' }),
    ]));
  });

  it('preserves the URL user when navigating', async () => {
    const onNavigate = renderNavigation(
      ['message.view', 'review.level1'],
      '/messages?user=alex.morgan',
    );
    const user = userEvent.setup();
    await user.click(screen.getByRole('link', { name: 'Message Review' }));

    expect(screen.getByTestId('location')).toHaveTextContent(
      '/messages/assigned?user=alex.morgan',
    );
    expect(onNavigate).toHaveBeenCalledOnce();
  });
});
