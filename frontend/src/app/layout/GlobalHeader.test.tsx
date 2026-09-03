import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '../../test/createTestQueryClient';

const { getCurrentUser } = vi.hoisted(() => ({ getCurrentUser: vi.fn() }));

vi.mock('../../pages/current-user/currentUserApi', () => ({ getCurrentUser }));
vi.mock('devextreme-react/button', () => ({
  default: ({
    elementAttr,
    onClick,
  }: {
    elementAttr: Record<string, unknown>;
    onClick: () => void;
  }) => <button type="button" {...elementAttr} onClick={onClick} />,
}));

import GlobalHeader from './GlobalHeader';

const currentUser = {
  userId: 42,
  userName: 'Alex Morgan',
  permissions: ['messages.read'],
  branches: [10],
  departments: [],
};

function renderHeader(
  props: Partial<React.ComponentProps<typeof GlobalHeader>> = {},
) {
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <GlobalHeader
          navigationOpen={false}
          showNavigationToggle={false}
          onNavigationToggle={() => undefined}
          {...props}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('GlobalHeader', () => {
  beforeEach(() => {
    getCurrentUser.mockReset();
  });

  it('shows the application brand and current user', async () => {
    getCurrentUser.mockResolvedValue(currentUser);

    renderHeader();

    expect(screen.getByRole('link', { name: 'SMBC home' })).toHaveAttribute(
      'href',
      '/',
    );
    expect(screen.getByText('Operations Reporting and Processing')).toBeInTheDocument();
    expect(screen.getByText('Loading user…')).toBeInTheDocument();
    expect(await screen.findByText('Alex Morgan')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('shows a safe fallback when the current user cannot be loaded', async () => {
    getCurrentUser.mockRejectedValue(new Error('private authentication details'));

    renderHeader();

    expect(await screen.findByText('User unavailable')).toBeInTheDocument();
    expect(screen.queryByText('private authentication details')).not.toBeInTheDocument();
  });

  it('exposes the mobile navigation state and toggles it', async () => {
    getCurrentUser.mockResolvedValue(currentUser);
    const onNavigationToggle = vi.fn();
    const user = userEvent.setup();

    const { rerender } = renderHeader({
      showNavigationToggle: true,
      onNavigationToggle,
    });

    const openButton = screen.getByRole('button', { name: 'Open navigation' });
    expect(openButton).toHaveAttribute('aria-expanded', 'false');
    expect(openButton).toHaveAttribute('aria-controls', 'application-navigation');
    await user.click(openButton);
    expect(onNavigationToggle).toHaveBeenCalledOnce();

    rerender(
      <QueryClientProvider client={createTestQueryClient()}>
        <MemoryRouter>
          <GlobalHeader
            navigationOpen
            showNavigationToggle
            onNavigationToggle={onNavigationToggle}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByRole('button', { name: 'Close navigation' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });
});
