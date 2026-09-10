import { afterEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '../../shared/api/errors';
import {
  approveReview,
  cancelReview,
  assignMessage,
  getAssignmentCandidates,
  getMessage,
  getMessageGrid,
  rejectReview,
  startReview,
} from './messagesApi';

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

describe('getMessage', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads message details with the request signal', async () => {
    const details = { id: 42, externalId: 'MSG-0042', body: '{1:F01RAW}' };
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: vi.fn().mockResolvedValue(details),
    });
    vi.stubGlobal('fetch', fetchMock);
    const controller = new AbortController();

    await expect(getMessage(42, controller.signal)).resolves.toEqual(details);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/messages/42',
      expect.objectContaining({ signal: controller.signal }),
    );
  });

  it('normalizes unsuccessful responses into ApiError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 404 }));

    await expect(getMessage(42)).rejects.toEqual(
      new ApiError('Unable to load message (404).', 404),
    );
  });
});

describe('review actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it.each([
    ['start', startReview, { level: 2 }],
    ['cancel', cancelReview, { level: 2 }],
    ['approve', approveReview, { level: 2, comment: 'confirmed' }],
    ['reject', rejectReview, { level: 2, comment: null }],
  ] as const)('posts the %s review action', async (action, request, body) => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204 });
    vi.stubGlobal('fetch', fetchMock);

    if (action === 'start' || action === 'cancel') {
      await request(42, 2);
    } else {
      await request(42, 2, body.comment);
    }

    expect(fetchMock).toHaveBeenCalledWith(
      `/api/messages/42/reviews/${action}`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(body),
      }),
    );
  });

  it('normalizes an unsuccessful review response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 409 }));

    await expect(approveReview(42, 1, null)).rejects.toEqual(
      new ApiError('Unable to approve review (409).', 409),
    );
  });

  it('uses the Problem Details message for a review conflict', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      detail: 'No eligible reviewer is available for review level 2.',
    }), { status: 409, headers: { 'Content-Type': 'application/problem+json' } })));

    await expect(approveReview(42, 1, null)).rejects.toEqual(
      new ApiError('No eligible reviewer is available for review level 2.', 409),
    );
  });
});

describe('manual assignment', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads eligible reviewers for the message', async () => {
    const candidates = [{ id: 2, userName: 'sam.lee', displayName: 'Sam Lee' }];
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(candidates), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })));

    await expect(getAssignmentCandidates(42)).resolves.toEqual(candidates);
    expect(fetch).toHaveBeenCalledWith('/api/messages/42/assignment-candidates',
      expect.objectContaining({ signal: undefined }));
  });

  it.each([
    [false, 'assign'],
    [true, 'reassign'],
  ])('posts the correct assignment action', async (reassign, action) => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204 });
    vi.stubGlobal('fetch', fetchMock);

    await assignMessage(42, 2, reassign);

    expect(fetchMock).toHaveBeenCalledWith(`/api/messages/42/${action}`,
      expect.objectContaining({ method: 'POST', body: JSON.stringify({ assignedTo: 2 }) }));
  });

  it('uses Problem Details for an invalid assignment', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      detail: 'The assignee is not eligible.',
    }), { status: 400, headers: { 'Content-Type': 'application/problem+json' } })));

    await expect(assignMessage(42, 2, false)).rejects.toEqual(
      new ApiError('The assignee is not eligible.', 400),
    );
  });
});
