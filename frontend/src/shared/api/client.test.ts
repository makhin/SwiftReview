import { afterEach, describe, expect, it, vi } from 'vitest';

import { apiClient } from './client';

describe('apiClient', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads JSON from the current origin and forwards the abort signal', async () => {
    const controller = new AbortController();
    const response = new Response(JSON.stringify({ id: 42 }), { status: 200 });
    const fetchMock = vi.fn().mockResolvedValue(response);
    vi.stubGlobal('fetch', fetchMock);

    await expect(
      apiClient.GET<{ id: number }>('/api/me', { signal: controller.signal }),
    ).resolves.toEqual({ data: { id: 42 }, response });
    expect(fetchMock).toHaveBeenCalledWith('/api/me', {
      signal: controller.signal,
    });
  });

  it('returns the response without parsing an unsuccessful body', async () => {
    const response = new Response('Forbidden', { status: 403 });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response));

    await expect(apiClient.GET('/api/me')).resolves.toEqual({
      data: undefined,
      response,
    });
  });

  it('does not parse a successful empty response', async () => {
    const response = new Response(null, { status: 204 });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response));

    await expect(apiClient.GET('/api/messages/1/assign')).resolves.toEqual({
      data: undefined,
      response,
    });
  });
});
