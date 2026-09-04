import { describe, expect, it } from 'vitest';

import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  messageTypesQueryOptions,
  referenceDataKeys,
  usersQueryOptions,
  workflowsQueryOptions,
} from './referenceDataQueries';

describe('reference data query options', () => {
  it.each([
    [branchesQueryOptions, referenceDataKeys.branches],
    [departmentsQueryOptions, referenceDataKeys.departments],
    [messageStatesQueryOptions, referenceDataKeys.messageStates],
    [messageTypesQueryOptions, referenceDataKeys.messageTypes],
    [usersQueryOptions, referenceDataKeys.users],
    [workflowsQueryOptions, referenceDataKeys.workflows],
  ] as const)('defines a stable key and thirty-minute stale time', (createOptions, key) => {
    const options = createOptions();

    expect(options.queryKey).toEqual(key);
    expect(options.staleTime).toBe(30 * 60 * 1000);
  });
});
