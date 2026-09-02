import createClient from 'openapi-fetch';
import { describe, expect, it, vi } from 'vitest';

vi.mock('openapi-fetch', () => ({ default: vi.fn(() => ({ GET: vi.fn() })) }));

describe('apiClient', () => {
  it('creates a client using the current origin', async () => {
    const { apiClient } = await import('./client');

    expect(createClient).toHaveBeenCalledWith();
    expect(apiClient).toHaveProperty('GET');
  });
});
