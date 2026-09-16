import { apiRequest } from './client';
import type { CurrentUserResponse } from './generated/contracts.generated';

export function getCurrentUser(signal?: AbortSignal): Promise<CurrentUserResponse> {
  return apiRequest('/api/me', { signal, errorMessage: 'Unable to load the current user' });
}
