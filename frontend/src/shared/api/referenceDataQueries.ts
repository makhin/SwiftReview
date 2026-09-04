import { queryOptions } from '@tanstack/react-query';

import {
  getBranches,
  getDepartments,
  getMessageStates,
  getMessageTypes,
  getUsers,
  getWorkflows,
} from './referenceDataApi';

const THIRTY_MINUTES = 30 * 60 * 1000;

export const referenceDataKeys = {
  all: ['reference-data'] as const,
  branches: ['reference-data', 'branches'] as const,
  departments: ['reference-data', 'departments'] as const,
  messageStates: ['reference-data', 'message-states'] as const,
  messageTypes: ['reference-data', 'message-types'] as const,
  users: ['reference-data', 'users'] as const,
  workflows: ['reference-data', 'workflows'] as const,
};

export function branchesQueryOptions() {
  return queryOptions({
    queryKey: referenceDataKeys.branches,
    queryFn: ({ signal }) => getBranches(signal),
    staleTime: THIRTY_MINUTES,
  });
}

export function departmentsQueryOptions() {
  return queryOptions({
    queryKey: referenceDataKeys.departments,
    queryFn: ({ signal }) => getDepartments(signal),
    staleTime: THIRTY_MINUTES,
  });
}

export function messageTypesQueryOptions() {
  return queryOptions({
    queryKey: referenceDataKeys.messageTypes,
    queryFn: ({ signal }) => getMessageTypes(signal),
    staleTime: THIRTY_MINUTES,
  });
}

export function messageStatesQueryOptions() {
  return queryOptions({
    queryKey: referenceDataKeys.messageStates,
    queryFn: ({ signal }) => getMessageStates(signal),
    staleTime: THIRTY_MINUTES,
  });
}

export function usersQueryOptions() {
  return queryOptions({
    queryKey: referenceDataKeys.users,
    queryFn: ({ signal }) => getUsers(signal),
    staleTime: THIRTY_MINUTES,
  });
}

export function workflowsQueryOptions() {
  return queryOptions({
    queryKey: referenceDataKeys.workflows,
    queryFn: ({ signal }) => getWorkflows(signal),
    staleTime: THIRTY_MINUTES,
  });
}
