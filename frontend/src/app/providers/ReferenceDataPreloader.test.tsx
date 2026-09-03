import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const referenceDataApi = vi.hoisted(() => ({
  getBranches: vi.fn().mockResolvedValue([]),
  getDepartments: vi.fn().mockResolvedValue([]),
  getMessageTypes: vi.fn().mockResolvedValue([]),
  getUsers: vi.fn().mockResolvedValue([]),
  getWorkflows: vi.fn().mockResolvedValue([]),
}));

vi.mock('../../shared/api/referenceDataApi', () => referenceDataApi);

import { createTestQueryClient } from '../../test/createTestQueryClient';
import ReferenceDataPreloader from './ReferenceDataPreloader';

describe('ReferenceDataPreloader', () => {
  it('loads every reference dataset without blocking the application', async () => {
    const queryClient = createTestQueryClient();

    const view = render(
      <QueryClientProvider client={queryClient}>
        <ReferenceDataPreloader />
        <div>Application ready</div>
      </QueryClientProvider>,
    );

    expect(screen.getByText('Application ready')).toBeInTheDocument();
    await waitFor(() => {
      expect(referenceDataApi.getUsers).toHaveBeenCalledOnce();
      expect(referenceDataApi.getBranches).toHaveBeenCalledOnce();
      expect(referenceDataApi.getDepartments).toHaveBeenCalledOnce();
      expect(referenceDataApi.getMessageTypes).toHaveBeenCalledOnce();
      expect(referenceDataApi.getWorkflows).toHaveBeenCalledOnce();
    });

    view.unmount();
    render(
      <QueryClientProvider client={queryClient}>
        <ReferenceDataPreloader />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(referenceDataApi.getUsers).toHaveBeenCalledOnce());
  });
});
