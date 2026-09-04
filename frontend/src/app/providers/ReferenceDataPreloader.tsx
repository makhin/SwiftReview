import { useQueries } from '@tanstack/react-query';

import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  messageTypesQueryOptions,
  usersQueryOptions,
  workflowsQueryOptions,
} from '../../shared/api/referenceDataQueries';

export default function ReferenceDataPreloader() {
  useQueries({
    queries: [
      usersQueryOptions(),
      branchesQueryOptions(),
      departmentsQueryOptions(),
      messageStatesQueryOptions(),
      messageTypesQueryOptions(),
      workflowsQueryOptions(),
    ],
  });

  return null;
}
