import { lazy, type ReactNode } from 'react';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';

import { ApiError } from '../api/errors';
import RootLayout from './RootLayout';
import RouteErrorBoundary from './RouteErrorBoundary';

function renderBrokenRoute(element: ReactNode) {
  const router = createMemoryRouter([
    {
      element: <RootLayout />,
      errorElement: <RouteErrorBoundary />,
      children: [{ path: '/', element }],
    },
  ]);

  return render(<RouterProvider router={router} />);
}

function renderLoaderError(status: number) {
  const router = createMemoryRouter([
    {
      path: '/',
      loader: () => {
        throw new Response(null, { status });
      },
      element: <main>Page</main>,
      errorElement: <RouteErrorBoundary />,
    },
  ]);

  return render(<RouterProvider router={router} />);
}

describe('RouteErrorBoundary', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('handles render errors without exposing internal details', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    function BrokenPage(): never {
      throw new Error('Sensitive render stack details');
    }

    renderBrokenRoute(<BrokenPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Unable to open this page');
    expect(alert).not.toHaveTextContent('Sensitive render stack details');
    expect(screen.getByRole('button', { name: 'Reload page' })).toBeInTheDocument();
  });

  it('handles a rejected lazy route without exposing chunk details', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const BrokenChunk = lazy(() =>
      Promise.reject(new Error('Failed to fetch private-module.js')),
    );

    renderBrokenRoute(<BrokenChunk />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('The page could not be loaded. Please try again.');
    expect(alert).not.toHaveTextContent('private-module.js');
  });

  it('handles authorization errors separately', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    function ForbiddenPage(): never {
      throw new ApiError('Sensitive authorization details', 403);
    }

    renderBrokenRoute(<ForbiddenPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Access denied');
    expect(alert).toHaveTextContent('You do not have permission to open this page.');
    expect(alert).not.toHaveTextContent('Sensitive authorization details');
  });

  it.each([
    {
      status: 401,
      title: 'Authentication required',
      message: 'Please sign in again to continue.',
    },
    {
      status: 503,
      title: 'Service temporarily unavailable',
      message: 'Please wait a moment and try again.',
    },
  ])('shows safe content for a $status route response', async ({ status, title, message }) => {
    renderLoaderError(status);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(title);
    expect(alert).toHaveTextContent(message);
  });
});
