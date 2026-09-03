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

import AppNavigation from './AppNavigation';

function LocationPath() {
  return <span data-testid="location">{useLocation().pathname}</span>;
}

describe('AppNavigation', () => {
  it('selects the current route and navigates through list items', async () => {
    const onNavigate = vi.fn();

    render(
      <MemoryRouter initialEntries={['/messages']}>
        <AppNavigation onNavigate={onNavigate} />
        <LocationPath />
      </MemoryRouter>,
    );

    expect(screen.getByRole('navigation', { name: 'Application navigation' }))
      .toBeInTheDocument();
    expect(listProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
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
});
