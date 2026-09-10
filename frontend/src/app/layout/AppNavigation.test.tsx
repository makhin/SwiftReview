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
vi.mock('../../shared/api/currentUserApi', () => ({
  getCurrentUser: vi.fn(() => new Promise(() => undefined)),
}));

import { createTestQueryClient } from '../../test/createTestQueryClient';
import AppNavigation from './AppNavigation';

function LocationPath() {
  const location = useLocation();
  return <span data-testid="location">{location.pathname}{location.search}</span>;
}

function renderNavigation(permissions: string[], initialEntry = '/messages') {
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
      <MemoryRouter initialEntries={[initialEntry]}>
        <AppNavigation onNavigate={onNavigate} />
        <LocationPath />
      </MemoryRouter>
    </QueryClientProvider>,
  );

  return onNavigate;
}

describe('AppNavigation', () => {
  it.each([
    { permissions: ['message.view'], assign: false, review: false },
    { permissions: ['message.assign'], assign: true, review: false },
    { permissions: ['review.level1'], assign: false, review: true },
    { permissions: ['review.level2'], assign: false, review: true },
    { permissions: ['review.level3'], assign: false, review: true },
    { permissions: ['message.assign', 'review.level2'], assign: true, review: true },
    { permissions: ['review.undo', 'audit.view'], assign: false, review: false },
  ])('shows pages according to $permissions', ({ permissions, assign, review }) => {
    renderNavigation(permissions);
    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items.some((item) => item.path === '/messages')).toBe(assign);
    expect(items.some((item) => item.path === '/messages/assigned?scope=mine')).toBe(review);
  });

  it('selects the current route and navigates through list items', async () => {
    const onNavigate = renderNavigation(['message.assign', 'review.level1']);

    expect(screen.getByRole('navigation', { name: 'Application navigation' }))
      .toBeInTheDocument();
    expect(listProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ path: '/messages', text: 'Messages' }),
          expect.objectContaining({
            path: '/messages/assigned?scope=mine',
            text: 'Review queue',
            icon: 'todo',
          }),
          expect.objectContaining({ path: '/me', text: 'User profile', icon: 'user' }),
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
        itemData: { path: '/me', text: 'User profile', icon: 'user' },
      }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent('/me');
    expect(onNavigate).toHaveBeenCalledOnce();
    expect(listProps).toHaveBeenLastCalledWith(
      expect.objectContaining({ selectedItemKeys: ['/me'] }),
    );
  });

  it('hides the assignment page from reviewers without assign permission', () => {
    renderNavigation(['review.level1']);

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

  it('does not show the removed assignment queue', () => {
    renderNavigation(['message.view', 'message.assign']);

    const items = listProps.mock.calls.at(-1)?.[0].items as Array<{ path: string }>;
    expect(items).not.toEqual(expect.arrayContaining([
      expect.objectContaining({ path: '/messages/assigned?scope=assignable' }),
    ]));
  });

  it('preserves the URL user when navigating', async () => {
    const onNavigate = renderNavigation(
      ['review.level1'],
      '/messages?user=alex.morgan',
    );
    const props = listProps.mock.calls.at(-1)?.[0] as {
      onItemClick: (event: { itemData: unknown }) => void;
    };

    await act(() =>
      props.onItemClick({
        itemData: {
          path: '/messages/assigned?scope=mine',
          text: 'Review queue',
          icon: 'todo',
        },
      }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent(
      '/messages/assigned?scope=mine&user=alex.morgan',
    );
    expect(onNavigate).toHaveBeenCalledOnce();
  });
});
