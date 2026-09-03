import { useQueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import AppProviders from './AppProviders';
import queryClient from './queryClient';

vi.mock('./ReferenceDataPreloader', () => ({ default: () => null }));

function QueryClientProbe() {
  const providedClient = useQueryClient();

  return <div>{providedClient === queryClient ? 'query client available' : 'wrong client'}</div>;
}

describe('AppProviders', () => {
  it('provides the application QueryClient to its children', () => {
    render(
      <AppProviders>
        <QueryClientProbe />
      </AppProviders>,
    );

    expect(screen.getByText('query client available')).toBeInTheDocument();
  });
});
