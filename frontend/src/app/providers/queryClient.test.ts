import { describe, expect, it } from 'vitest';

import { ApiError } from '../../shared/api/errors';
import queryClient from './queryClient';

function getRetryPolicy() {
  const retry = queryClient.getDefaultOptions().queries?.retry;

  if (typeof retry !== 'function') {
    throw new Error('Expected retry to be configured as a function');
  }

  return retry;
}

describe('queryClient', () => {
  it('does not retry client errors', () => {
    const error = new ApiError('Forbidden', 403);

    expect(getRetryPolicy()(0, error)).toBe(false);
  });

  it('limits retries for network and server errors', () => {
    const retry = getRetryPolicy();

    expect(retry(0, new Error('Network error'))).toBe(true);
    expect(retry(1, new ApiError('Unavailable', 503))).toBe(true);
    expect(retry(2, new Error('Network error'))).toBe(false);
  });
});
