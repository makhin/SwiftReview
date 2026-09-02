import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { meModule } = vi.hoisted(() => {
  let resolve!: () => void;
  const loaded = new Promise<void>((done) => {
    resolve = done;
  });

  return { meModule: { loaded, resolve } };
});

vi.mock('../design-system/DesignSystemPage', () => ({
  default: () => <main>Design system page</main>,
}));
vi.mock('../me/MePage', async () => {
  await meModule.loaded;

  return { default: () => <main>Current user page</main> };
});
vi.mock('../messages/MessagesPage', () => ({
  default: () => <main>Messages page</main>,
}));

import App from './App';
import router from './router';

describe('App routing', () => {
  it('renders application routes inside the shared layout', async () => {
    await router.navigate('/me');
    render(<App />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading page…');
    meModule.resolve();

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
