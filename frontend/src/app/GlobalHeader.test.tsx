import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import GlobalHeader from './GlobalHeader';

describe('GlobalHeader', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'scrollY', {
      configurable: true,
      value: 0,
      writable: true,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('opens and closes the navigation menu', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <GlobalHeader />
      </MemoryRouter>,
    );

    const menuButton = screen.getByRole('button', { name: 'Open navigation' });

    expect(menuButton).toHaveAttribute('aria-expanded', 'false');

    await user.click(menuButton);

    expect(screen.getByRole('button', { name: 'Close navigation' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );

    await user.click(screen.getByRole('link', { name: 'Messages' }));

    expect(screen.getByRole('button', { name: 'Open navigation' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );

    await user.click(screen.getByRole('button', { name: 'Open navigation' }));
    await user.click(screen.getByRole('link', { name: 'Current user' }));
    expect(screen.getByRole('button', { name: 'Open navigation' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Open navigation' }));
    await user.click(screen.getByRole('link', { name: 'Design system' }));
    expect(screen.getByRole('button', { name: 'Open navigation' })).toBeInTheDocument();
  });

  it('hides while scrolling down and returns while scrolling up or focused', () => {
    let frameCallback: FrameRequestCallback | undefined;
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback) => {
      frameCallback = callback;
      return 1;
    });

    const { container } = render(
      <MemoryRouter>
        <GlobalHeader />
      </MemoryRouter>,
    );
    const header = container.querySelector('header')!;
    Object.defineProperty(header, 'offsetHeight', { value: 50 });

    window.scrollY = 100;
    fireEvent.scroll(window);
    act(() => frameCallback?.(0));
    expect(header).toHaveAttribute('data-hidden', 'true');

    window.scrollY = 95;
    fireEvent.scroll(window);
    act(() => frameCallback?.(0));
    expect(header).toHaveAttribute('data-hidden', 'true');

    window.scrollY = 80;
    fireEvent.scroll(window);
    act(() => frameCallback?.(0));
    expect(header).toHaveAttribute('data-hidden', 'false');

    window.scrollY = 100;
    fireEvent.scroll(window);
    act(() => frameCallback?.(0));
    expect(header).toHaveAttribute('data-hidden', 'true');

    fireEvent.focus(header);
    expect(header).toHaveAttribute('data-hidden', 'false');

    window.scrollY = -10;
    fireEvent.scroll(window);
    act(() => frameCallback?.(0));
    expect(header).toHaveAttribute('data-hidden', 'false');
  });

  it('cancels a queued animation frame when unmounted', () => {
    vi.spyOn(window, 'requestAnimationFrame').mockReturnValue(7);
    const cancelAnimationFrame = vi.spyOn(window, 'cancelAnimationFrame');

    const { unmount } = render(
      <MemoryRouter>
        <GlobalHeader />
      </MemoryRouter>,
    );

    fireEvent.scroll(window);
    unmount();

    expect(cancelAnimationFrame).toHaveBeenCalledWith(7);
  });
});
