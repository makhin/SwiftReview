import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiRequest } from './client';
import { ApiError, ApiRequestError } from './errors';

const options = { errorMessage: 'Unable to save' };

describe('apiRequest', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/');
    window.sessionStorage.clear();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it('returns JSON, forwards the signal, and adds headers', async () => {
    const signal = new AbortController().signal;
    const fetchMock = vi.fn().mockResolvedValue(new Response('{"id":42}'));
    vi.stubGlobal('fetch', fetchMock);
    await expect(apiRequest('/api/me', { ...options, signal, headers: { 'X-Test': 'yes' } })).resolves.toEqual({ id: 42 });
    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(path).toBe('/api/me');
    expect(init.signal).toBe(signal);
    const headers = new Headers(init.headers);
    expect(headers.get('X-Test')).toBe('yes');
    expect(headers.get('X-Debug-User')).toBe('admin');
    expect(headers.get('Accept')).toContain('application/problem+json');
    expect(headers.has('Content-Type')).toBe(false);
  });

  it('keeps a URL debug user for navigation in the same tab', async () => {
    window.history.replaceState(null, '', '/messages?user=5');
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await apiRequest('/api/test', { ...options, responseType: 'none' });
    window.history.replaceState(null, '', '/me');
    await apiRequest('/api/test', { ...options, responseType: 'none' });
    for (const [, init] of fetchMock.mock.calls as [string, RequestInit][]) {
      expect(new Headers(init.headers).get('X-Debug-User')).toBe('5');
    }
  });

  it('never adds a debug identity in production', async () => {
    vi.stubEnv('DEV', false);
    window.history.replaceState(null, '', '/?user=5');
    const fetchMock = vi.fn().mockResolvedValue(new Response('null'));
    vi.stubGlobal('fetch', fetchMock);
    await apiRequest('/api/test', options);
    expect(new Headers(fetchMock.mock.calls[0][1].headers).has('X-Debug-User')).toBe(false);
  });

  it('encodes a JSON body once', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await apiRequest('/api/test', { ...options, method: 'PUT', body: { id: '9007199254740993' }, responseType: 'none' });
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.body).toBe('{"id":"9007199254740993"}');
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
  });

  it.each([200, 204, 205])('accepts empty %s for a void endpoint', async (status) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status })));
    await expect(apiRequest('/api/test', { ...options, responseType: 'none' })).resolves.toBeUndefined();
  });

  it.each(['', '{broken'])('rejects missing or malformed required JSON: %j', async (body) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(body)));
    await expect(apiRequest('/api/test', options)).rejects.toMatchObject({
      kind: 'invalid-response', status: 200, outcomeUnknown: false,
    });
  });

  it('does not pretend a required JSON response exists for 204', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
    await expect(apiRequest('/api/test', { ...options, method: 'POST' })).rejects.toMatchObject({
      kind: 'invalid-response', status: 204, outcomeUnknown: true,
    });
  });

  it('preserves Problem Details extensions while trusting the HTTP status', async () => {
    const problem = { type: '/validation', title: 'Invalid input', detail: 'Choose a role.', status: 500,
      instance: '/requests/1', traceId: 'abc', errors: { role: ['Required'] } };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(problem), { status: 400 })));
    await expect(apiRequest('/api/test', options)).rejects.toMatchObject({
      name: 'ApiError', status: 400, message: 'Choose a role.', problem,
    });
  });

  it.each([
    ['{"title":"Forbidden","detail":" "}', 'Forbidden'],
    ['{"title":{},"detail":["bad"],"status":"400","type":7,"instance":false}', 'Unable to save (400).'],
    ['<html>proxy error</html>', 'Unable to save (400).'],
    ['null', 'Unable to save (400).'],
    ['[]', 'Unable to save (400).'],
    ['', 'Unable to save (400).'],
  ])('handles error body %s safely', async (body, message) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(body, { status: 400 })));
    await expect(apiRequest('/api/test', options)).rejects.toMatchObject({ status: 400, message });
  });

  it('preserves HTTP rejection even when reading the error body fails', async () => {
    const response = new Response(null, { status: 503 });
    vi.spyOn(response, 'json').mockRejectedValue(new TypeError('connection lost'));
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response));
    await expect(apiRequest('/api/test', { ...options, method: 'POST' })).rejects.toBeInstanceOf(ApiError);
  });

  it.each(['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const)('classifies a lost %s response without retrying', async (method) => {
    const cause = new TypeError('connection lost');
    const fetchMock = vi.fn().mockRejectedValue(cause);
    vi.stubGlobal('fetch', fetchMock);
    await expect(apiRequest('/api/test', { ...options, method })).rejects.toMatchObject({
      kind: 'network', outcomeUnknown: method !== 'GET', cause,
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('does not send a pre-cancelled mutation', async () => {
    const controller = new AbortController();
    controller.abort();
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    await expect(apiRequest('/api/test', { ...options, method: 'POST', signal: controller.signal })).rejects.toMatchObject({
      name: 'AbortError', kind: 'aborted', outcomeUnknown: false,
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each(['GET', 'POST'] as const)('classifies in-flight %s cancellation', async (method) => {
    const controller = new AbortController();
    vi.stubGlobal('fetch', vi.fn().mockImplementation(() => {
      controller.abort();
      return Promise.reject(controller.signal.reason);
    }));
    await expect(apiRequest('/api/test', { ...options, method, signal: controller.signal })).rejects.toMatchObject({
      kind: 'aborted', outcomeUnknown: method !== 'GET',
    });
  });

  it.each(['network', 'aborted'] as const)('handles %s failure while reading a successful response', async (kind) => {
    const response = new Response('{"id":1}');
    const cause = kind === 'aborted' ? new DOMException('cancelled', 'AbortError') : new TypeError('lost body');
    vi.spyOn(response, 'text').mockRejectedValue(cause);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response));
    await expect(apiRequest('/api/test', { ...options, method: 'POST' })).rejects.toMatchObject({
      kind, cause, status: 200, outcomeUnknown: true,
      message: expect.stringContaining('The result is unknown'),
    });
  });

  it('uses a typed error for invalid successful mutation JSON', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('broken')));
    await expect(apiRequest('/api/test', { ...options, method: 'POST' })).rejects.toBeInstanceOf(ApiRequestError);
  });
});
