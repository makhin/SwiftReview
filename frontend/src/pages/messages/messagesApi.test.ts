import { afterEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';
import { getMessageGrid } from './messagesApi';

describe('getMessageGrid', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/');
    window.sessionStorage.clear();
    vi.unstubAllGlobals();
  });

  it('serializes paging and remote operations into the request', async () => {
    const result = { data: [{ id: 1 }], totalCount: 1 };
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: vi.fn().mockResolvedValue(result),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(
      getMessageGrid(
        {
          skip: 20,
          take: 10,
          sort: [{ selector: 'receivedAt', desc: true }],
          filter: ['state', '=', 'New'],
          requireTotalCount: true,
        },
        'departments',
      ),
    ).resolves.toEqual(result);

    const requestUrl = new URL(fetchMock.mock.calls[0][0], 'https://example.test');
    expect(requestUrl.pathname).toBe('/api/messages/grid');
    expect(requestUrl.searchParams.get('skip')).toBe('20');
    expect(requestUrl.searchParams.get('take')).toBe('10');
    expect(requestUrl.searchParams.get('sort')).toBe(
      JSON.stringify([{ selector: 'receivedAt', desc: true }]),
    );
    expect(requestUrl.searchParams.get('filter')).toBe(
      JSON.stringify(['state', '=', 'New']),
    );
    expect(requestUrl.searchParams.get('requireTotalCount')).toBe('true');
    expect(requestUrl.searchParams.get('assignmentScope')).toBe('departments');
  });

  it('uses default paging values', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: vi.fn().mockResolvedValue({ data: [] }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await getMessageGrid({});

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/messages/grid?skip=0&take=20');
    expect(new Headers(init.headers).get('X-Debug-User')).toBe('admin');
  });

  it('normalizes unsuccessful responses into ApiError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 503 }));

    await expect(getMessageGrid({})).rejects.toEqual(
      new ApiError('Unable to load messages (503).', 503),
    );
  });

  it('normalizes network failures and preserves their cause', async () => {
    const networkError = new TypeError('fetch failed');
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(networkError));

    await expect(getMessageGrid({})).rejects.toMatchObject({
      message: 'Unable to load messages.',
      cause: networkError,
    });
  });
});
