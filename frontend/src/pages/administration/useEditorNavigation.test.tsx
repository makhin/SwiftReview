import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { createMemoryRouter, Link, RouterProvider } from 'react-router-dom';
import { expect, it } from 'vitest';
import type { EditorHandle } from './useEditorDraft';
import { useEditorNavigation } from './useEditorNavigation';

function setup() {
  const state = { dirty: true, saving: false };
  const editor: { current: EditorHandle } = {
    current: { isDirty: () => state.dirty, isSaving: () => state.saving },
  };
  function Editor() {
    const guard = useEditorNavigation(editor);
    return (
      <>
        <Link to="/me">Profile</Link>
        {guard.confirmOpen && (
          <div role="dialog">
            <button onClick={() => guard.finishConfirmation(false)}>
              Keep editing
            </button>
            <button onClick={() => guard.finishConfirmation(true)}>
              Discard changes
            </button>
          </div>
        )}
      </>
    );
  }
  const router = createMemoryRouter(
    [
      { path: '/admin', element: <Editor /> },
      { path: '/me', element: <h1>Profile page</h1> },
      { path: '/other', element: <h1>Other page</h1> },
    ],
    { initialEntries: ['/me', '/admin'] },
  );
  render(<RouterProvider router={router} />);
  return { state, router };
}
it('cancels route and history transitions, and proceeds only after discard', async () => {
  const { router } = setup();
  fireEvent.click(screen.getByText('Profile'));
  fireEvent.click(await screen.findByText('Keep editing'));
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
  expect(router.state.location.pathname).toBe('/admin');
  await act(() => router.navigate(-1));
  fireEvent.click(await screen.findByText('Keep editing'));
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
  expect(router.state.location.pathname).toBe('/admin');
  fireEvent.click(screen.getByText('Profile'));
  fireEvent.click(await screen.findByText('Discard changes'));
  expect(await screen.findByText('Profile page')).toBeInTheDocument();
});
it('blocks navigation during saving even when the draft is already clean', async () => {
  const { state, router } = setup();
  state.dirty = false;
  state.saving = true;
  fireEvent.click(screen.getByText('Profile'));
  await waitFor(() =>
    expect(
      [...router.state.blockers.values()].every(
        (blocker) => blocker.state === 'unblocked',
      ),
    ).toBe(true),
  );
  expect(router.state.location.pathname).toBe('/admin');
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  state.saving = false;
  fireEvent.click(screen.getByText('Profile'));
  expect(await screen.findByText('Profile page')).toBeInTheDocument();
});
it('protects browser unload only while dirty or saving', () => {
  const { state } = setup();
  const dirty = new Event('beforeunload', { cancelable: true });
  window.dispatchEvent(dirty);
  expect(dirty.defaultPrevented).toBe(true);
  state.dirty = false;
  const clean = new Event('beforeunload', { cancelable: true });
  window.dispatchEvent(clean);
  expect(clean.defaultPrevented).toBe(false);
  state.saving = true;
  const saving = new Event('beforeunload', { cancelable: true });
  window.dispatchEvent(saving);
  expect(saving.defaultPrevented).toBe(true);
});

it('keeps the first destination when a second navigation is attempted during confirmation', async () => {
  const { router } = setup();
  fireEvent.click(screen.getByText('Profile'));
  await screen.findByRole('dialog');
  await act(() => router.navigate('/other'));
  fireEvent.click(screen.getByText('Discard changes'));
  expect(await screen.findByText('Profile page')).toBeInTheDocument();
});
