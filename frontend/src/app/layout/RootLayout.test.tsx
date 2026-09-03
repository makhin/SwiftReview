import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { drawerProps } = vi.hoisted(() => ({ drawerProps: vi.fn() }));

vi.mock('devextreme-react/drawer', () => ({
  default: ({ children, render: renderPanel, ...props }: React.PropsWithChildren<{
    render: () => React.ReactNode;
  }>) => {
    drawerProps(props);
    return (
      <div>
        {renderPanel()}
        {children}
      </div>
    );
  },
}));

vi.mock('./GlobalHeader', () => ({
  default: ({
    navigationOpen,
    showNavigationToggle,
    onNavigationToggle,
  }: {
    navigationOpen: boolean;
    showNavigationToggle: boolean;
    onNavigationToggle: () => void;
  }) => (
    <header>
      <span>{showNavigationToggle ? 'Mobile header' : 'Desktop header'}</span>
      <span>{navigationOpen ? 'Navigation open' : 'Navigation closed'}</span>
      {showNavigationToggle ? (
        <button type="button" onClick={onNavigationToggle}>
          Toggle navigation
        </button>
      ) : null}
    </header>
  ),
}));

vi.mock('./AppNavigation', () => ({
  default: ({ onNavigate }: { onNavigate: () => void }) => (
    <button type="button" onClick={onNavigate}>
      Select page
    </button>
  ),
}));

import RootLayout from './RootLayout';

function mockMediaQueries({ mobile = false, reducedMotion = false } = {}) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      matches: query.includes('max-width') ? mobile : reducedMotion,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  );
}

function renderLayout() {
  return render(
    <MemoryRouter initialEntries={['/messages']}>
      <Routes>
        <Route element={<RootLayout />}>
          <Route path="/messages" element={<main>Messages page</main>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('RootLayout', () => {
  beforeEach(() => {
    drawerProps.mockClear();
    vi.unstubAllGlobals();
  });

  it('keeps the navigation open beside the content on desktop', () => {
    mockMediaQueries();

    renderLayout();

    expect(screen.getByText('Desktop header')).toBeInTheDocument();
    expect(screen.getByText('Navigation open')).toBeInTheDocument();
    expect(drawerProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
        opened: true,
        openedStateMode: 'shrink',
        shading: false,
        animationEnabled: true,
      }),
    );
  });

  it('opens and closes the overlay navigation on mobile', async () => {
    mockMediaQueries({ mobile: true });
    const user = userEvent.setup();

    renderLayout();

    expect(screen.getByText('Mobile header')).toBeInTheDocument();
    expect(screen.getByText('Navigation closed')).toBeInTheDocument();
    expect(drawerProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
        opened: false,
        openedStateMode: 'overlap',
        shading: true,
        closeOnOutsideClick: true,
      }),
    );

    await user.click(screen.getByRole('button', { name: 'Toggle navigation' }));
    expect(screen.getByText('Navigation open')).toBeInTheDocument();
    expect(drawerProps).toHaveBeenLastCalledWith(
      expect.objectContaining({ opened: true }),
    );

    const openDrawerProps = drawerProps.mock.calls.at(-1)?.[0] as {
      onOpenedChange: (opened: boolean) => void;
    };
    act(() => openDrawerProps.onOpenedChange(false));
    expect(screen.getByText('Navigation closed')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Toggle navigation' }));
    await user.click(screen.getByRole('button', { name: 'Select page' }));
    expect(screen.getByText('Navigation closed')).toBeInTheDocument();
  });

  it('disables drawer animation when reduced motion is requested', () => {
    mockMediaQueries({ reducedMotion: true });

    renderLayout();

    expect(drawerProps).toHaveBeenLastCalledWith(
      expect.objectContaining({ animationEnabled: false }),
    );
  });
});
