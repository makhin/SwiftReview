import { apiClient } from './client';
import { ApiError } from './errors';
import type { components } from './schema';

type ReferenceItem = components['schemas']['ReferenceItemDto'];
type UserSummary = components['schemas']['UserSummaryDto'];
type WorkflowSummary = components['schemas']['WorkflowSummaryDto'];

async function getReferenceData<T>(
  name: string,
  request: () => Promise<{ data?: T; response: Response }>,
  signal?: AbortSignal,
): Promise<T> {
  try {
    const { data, response } = await request();

    if (!data) {
      throw new ApiError(`Unable to load ${name} (${response.status}).`, response.status);
    }

    return data;
  } catch (error) {
    if (error instanceof ApiError || signal?.aborted) {
      throw error;
    }

    throw new Error(`Unable to load ${name}.`, { cause: error });
  }
}

export function getUsers(signal?: AbortSignal): Promise<UserSummary[]> {
  return getReferenceData('users', () => apiClient.GET('/api/users', { signal }), signal);
}

export function getBranches(signal?: AbortSignal): Promise<ReferenceItem[]> {
  return getReferenceData(
    'branches',
    () => apiClient.GET('/api/branches', { signal }),
    signal,
  );
}

export function getDepartments(signal?: AbortSignal): Promise<ReferenceItem[]> {
  return getReferenceData(
    'departments',
    () => apiClient.GET('/api/departments', { signal }),
    signal,
  );
}

export function getMessageTypes(signal?: AbortSignal): Promise<string[]> {
  return getReferenceData(
    'message types',
    () => apiClient.GET('/api/message-types', { signal }),
    signal,
  );
}

export function getWorkflows(signal?: AbortSignal): Promise<WorkflowSummary[]> {
  return getReferenceData(
    'workflows',
    () => apiClient.GET('/api/workflows', { signal }),
    signal,
  );
}
