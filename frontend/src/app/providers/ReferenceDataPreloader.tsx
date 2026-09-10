import { useQueries, useQuery } from '@tanstack/react-query';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';

import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  messageTypesQueryOptions,
  usersQueryOptions,
  workflowsQueryOptions,
} from '../../shared/api/referenceDataQueries';

export default function ReferenceDataPreloader() {
  const { data: user } = useQuery(currentUserQueryOptions());
  const enabled = !!user?.permissions.includes('message.view');
  useQueries({
    queries: [
      usersQueryOptions(),
      branchesQueryOptions(),
      departmentsQueryOptions(),
      messageStatesQueryOptions(),
      messageTypesQueryOptions(),
      workflowsQueryOptions(),
    ].map((query) => ({ ...query, enabled })),
  });

  return null;
}
