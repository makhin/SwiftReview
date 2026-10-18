import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { meModule } = vi.hoisted(() => {
  let resolve!: () => void;
  const loaded = new Promise<void>((done) => {
    resolve = done;
  });

  return { meModule: { loaded, resolve } };
});




vi.mock('./providers/ReferenceDataPreloader', () => ({ default: () => null }));

vi.mock('../shared/api/currentUserApi', () => ({
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
vi.mock('../pages/messages/AssignedMessagesPage', () => ({
  default: () => <main>Assigned messages page</main>,
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

    await router.navigate('/messages/assigned?scope=mine');
    expect(await screen.findByText('Assigned messages page')).toBeInTheDocument();

    await router.navigate('/design-system');
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Unable to open this page',
    );

    await router.navigate('/');
    await waitFor(() => expect(router.state.location.pathname).toBe('/messages'));
    expect(screen.getByText('Messages page')).toBeInTheDocument();

    await router.navigate('/?user=alex.morgan');
    await waitFor(() => expect(router.state.location.pathname).toBe('/messages'));
    expect(router.state.location.search).toBe('?user=alex.morgan');
  });
});

vi.mock('@smbc/ui', async () => ({ ...await import('../test/smbcMock'),
...(() => ({
  Drawer: ({ children, panel: renderPanel }: React.PropsWithChildren<{
    panel: () => React.ReactNode;
  }>) => (
    <div>
      {renderPanel()}
      {children}
    </div>
  ),
}))(),
...(() => ({
  Button: () => <button type="button">Navigation</button>,
}))()
}));
