import { useQueries } from '@tanstack/react-query';

import {
  branchesQueryOptions,
  departmentsQueryOptions,
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
      messageTypesQueryOptions(),
      workflowsQueryOptions(),
    ],
  });

  return null;
}
