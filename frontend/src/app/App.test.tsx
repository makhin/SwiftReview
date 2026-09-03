import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { meModule } = vi.hoisted(() => {
  let resolve!: () => void;
  const loaded = new Promise<void>((done) => {
    resolve = done;
  });

  return { meModule: { loaded, resolve } };
});

vi.mock('devextreme-react/drawer', () => ({
  default: ({ children, render: renderPanel }: React.PropsWithChildren<{
    render: () => React.ReactNode;
  }>) => (
    <div>
      {renderPanel()}
      {children}
    </div>
  ),
}));
vi.mock('devextreme-react/list', () => ({
  default: () => <div aria-label="Application pages" />,
}));
vi.mock('devextreme-react/button', () => ({
  default: () => <button type="button">Navigation</button>,
}));
vi.mock('./providers/ReferenceDataPreloader', () => ({ default: () => null }));

vi.mock('../pages/current-user/currentUserApi', () => ({
  getCurrentUser: vi.fn().mockResolvedValue({
    userId: 42,
    userName: 'Alex Morgan',
    permissions: [],
    branches: [],
    departments: [],
  }),
}));
vi.mock('../pages/current-user/CurrentUserPage', async () => {
  await meModule.loaded;

  return { default: () => <main>Current user page</main> };
});
vi.mock('../pages/messages/MessagesPage', () => ({
  default: () => <main>Messages page</main>,
}));

import App from './App';
import router from './router/router';

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

    await router.navigate('/design-system');
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to open this page',
    );

    await router.navigate('/');
    await waitFor(() => expect(router.state.location.pathname).toBe('/messages'));
    expect(screen.getByText('Messages page')).toBeInTheDocument();
  });
});
