import { afterEach, describe, expect, it, vi } from 'vitest';

import { apiClient } from './client';

describe('apiClient', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/');
    window.sessionStorage.clear();
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
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.signal).toBe(controller.signal);
    expect(new Headers(init.headers).get('X-Debug-User')).toBe('supervisor');
  });

  it('uses the URL user for API requests and keeps it for navigation in the same tab', async () => {
    window.history.replaceState(null, '', '/messages?user=6');
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await apiClient.GET('/api/me');
    window.history.replaceState(null, '', '/me');
    await apiClient.GET('/api/me');

    expect(fetchMock).toHaveBeenCalledTimes(2);
    for (const [, init] of fetchMock.mock.calls as [string, RequestInit][]) {
      expect(new Headers(init.headers).get('X-Debug-User')).toBe('6');
    }
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
