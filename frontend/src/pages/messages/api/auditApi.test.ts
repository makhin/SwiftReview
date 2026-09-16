import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const get = vi.fn();

import { ApiError } from '../../../shared/api/errors';
import { getMessageAudit } from './auditApi';

describe('message audit API', () => {
  afterEach(() => vi.unstubAllGlobals());
  beforeEach(() => {
    get.mockReset();
    vi.stubGlobal('fetch', get);
  });

  it('loads the requested audit page and forwards the abort signal', async () => {
    const controller = new AbortController();
    const result = { items: [], totalCount: 0 };
    get.mockResolvedValue(new Response(JSON.stringify(result)));

    await expect(getMessageAudit(42, 50, 50, controller.signal)).resolves.toEqual(result);
    expect(get).toHaveBeenCalledWith('/api/messages/42/audit?skip=50&take=50', expect.objectContaining({
      signal: controller.signal,
    }));
  });

  it('throws an ApiError containing the response status', async () => {
    get.mockResolvedValue(new Response(null, { status: 403 }));

    await expect(getMessageAudit(42, 0, 50)).rejects.toEqual(
      new ApiError('Unable to load the audit trail (403).', 403),
    );
  });

  it('normalizes network failures', async () => {
    const networkError = new TypeError('fetch failed');
    get.mockRejectedValue(networkError);

    await expect(getMessageAudit(42, 0, 50)).rejects.toMatchObject({
      message: 'Unable to load the audit trail.',
      cause: networkError,
    });
  });
});
