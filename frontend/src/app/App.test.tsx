import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../design-system/DesignSystemPage', () => ({
  default: () => <main>Design system page</main>,
}));
vi.mock('../me/MePage', () => ({ default: () => <main>Current user page</main> }));
vi.mock('../messages/MessagesPage', () => ({
  default: () => <main>Messages page</main>,
}));

import App from './App';
import router from './router';

describe('App routing', () => {
  it('renders application routes inside the shared layout', async () => {
    await router.navigate('/me');
    render(<App />);

    expect(await screen.findByText('Current user page')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Skip to main content' })).toHaveAttribute(
      'href',
      '#main-content',
    );

    await router.navigate('/messages');
    expect(await screen.findByText('Messages page')).toBeInTheDocument();

    await router.navigate('/');
    expect(await screen.findByText('Design system page')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/design-system');
  });
});
