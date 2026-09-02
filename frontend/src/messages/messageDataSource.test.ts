import { afterEach, describe, expect, it, vi } from 'vitest';

import { messageDataSource } from './messageDataSource';

describe('messageDataSource', () => {
  afterEach(() => {
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
      messageDataSource.load({
        skip: 20,
        take: 10,
        sort: [{ selector: 'receivedAt', desc: true }],
        filter: ['state', '=', 'New'],
        requireTotalCount: true,
      }),
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
  });

  it('uses default paging values', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: vi.fn().mockResolvedValue({ data: [] }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await messageDataSource.load({});

    expect(fetchMock).toHaveBeenCalledWith('/api/messages/grid?skip=0&take=20');
  });

  it('rejects unsuccessful responses with their status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 503 }));

    await expect(messageDataSource.load({})).rejects.toThrow(
      'Unable to load messages (503).',
    );
  });
});
