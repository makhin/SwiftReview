import { apiRequest } from './client';
import type {
  MessageStateReferenceDto, ReferenceItemDto, UserSummaryDto, WorkflowSummaryDto,
} from './generated/contracts.generated';

export function getUsers(signal?: AbortSignal): Promise<UserSummaryDto[]> {
  return apiRequest('/api/users', { signal, errorMessage: 'Unable to load users' });
}

export function getBranches(signal?: AbortSignal): Promise<ReferenceItemDto[]> {
  return apiRequest('/api/branches', { signal, errorMessage: 'Unable to load branches' });
}

export function getDepartments(signal?: AbortSignal): Promise<ReferenceItemDto[]> {
  return apiRequest('/api/departments', { signal, errorMessage: 'Unable to load departments' });
}

export function getMessageTypes(signal?: AbortSignal): Promise<string[]> {
  return apiRequest('/api/message-types', { signal, errorMessage: 'Unable to load message types' });
}

export function getMessageStates(signal?: AbortSignal): Promise<MessageStateReferenceDto[]> {
  return apiRequest('/api/message-states', { signal, errorMessage: 'Unable to load message states' });
}

export function getWorkflows(signal?: AbortSignal): Promise<WorkflowSummaryDto[]> {
  return apiRequest('/api/workflows', { signal, errorMessage: 'Unable to load workflows' });
}
