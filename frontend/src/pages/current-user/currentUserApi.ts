import { apiClient } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type { CurrentUserResponse } from '../../shared/api/generated/contracts.generated';

type CurrentUser = CurrentUserResponse;

export async function getCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  try {
    const { data, response } = await apiClient.GET<CurrentUser>('/api/me', { signal });

    if (!data) {
      throw new ApiError(
        `Unable to load the current user (${response.status}).`,
        response.status,
      );
    }

    return data;
  } catch (error) {
    if (error instanceof ApiError || signal?.aborted) {
      throw error;
    }

    throw new Error('Unable to load the current user.', { cause: error });
  }
}
