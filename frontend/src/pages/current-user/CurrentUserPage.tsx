import { Card } from '@smbc/ui';
import { useQuery } from '@tanstack/react-query';

import { ApiError } from '../../shared/api/errors';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import {
  branchesQueryOptions,
  departmentsQueryOptions,
} from '../../shared/api/referenceDataQueries';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';

function getErrorContent(error: Error) {
  if (error instanceof ApiError && error.status === 401) {
    return {
      title: 'Authentication required',
      message: 'Please sign in again to view your profile.',
    };
  }

  if (error instanceof ApiError && error.status === 403) {
    return {
      title: 'Access denied',
      message: 'You do not have permission to view this profile.',
    };
  }

  if (error instanceof ApiError && error.status >= 500) {
    return {
      title: 'Profile temporarily unavailable',
      message: 'Please wait a moment and try again.',
    };
  }

  return {
    title: 'Unable to load profile',
    message: 'Check your connection and try again.',
  };
}

export default function CurrentUserPage() {
  const {
    data: user,
    error,
    isPending,
    refetch,
  } = useQuery(currentUserQueryOptions());
  const { data: branches } = useQuery({
    ...branchesQueryOptions(),
    enabled: !!user?.permissions.includes('message.view'),
  });
  const { data: departments } = useQuery({
    ...departmentsQueryOptions(),
    enabled: !!user?.permissions.includes('message.view'),
  });
  const errorContent = error ? getErrorContent(error) : undefined;

  return (
    <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6">
      <header className="flex flex-col items-stretch justify-between gap-4 md:flex-row md:items-start">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold leading-tight">
            {user?.displayName ?? 'Current user'}
          </h1>
          <p className="mt-2 text-fg-muted">Identity and access details.</p>
        </div>
      </header>

      <Card className="">
        <Card.Header className="">
          <Card.Title className="">Profile</Card.Title>
        </Card.Header>
        <Card.Body className="" aria-busy={isPending}>
          {errorContent ? (
            <PageError
              title={errorContent.title}
              message={errorContent.message}
              actionLabel="Retry"
              onAction={() => void refetch()}
            />
          ) : user ? (
            <>
              <dl className="grid grid-cols-1 gap-x-4 gap-y-1 md:grid-cols-[minmax(140px,220px)_minmax(0,1fr)] md:gap-y-2 m-0 [&_dt]:text-fg-muted [&_dd]:m-0 [&_dd]:text-fg max-md:[&_dd:not(:last-child)]:mb-2">
                <dt>User ID</dt>
                <dd>{user.userId}</dd>
                <dt>User name</dt>
                <dd>{user.userName}</dd>
                <dt>Global administrator</dt>
                <dd>{user.isGlobalAdministrator ? 'Yes' : 'No'}</dd>
              </dl>
              <section
                className="flex flex-col gap-2 mt-4"
                aria-labelledby="access-by-scope-title"
              >
                <h2
                  className="text-xl font-semibold"
                  id="access-by-scope-title"
                >
                  Access by Scope
                </h2>
                {user.isGlobalAdministrator ? (
                  <p>
                    Full access to all information and actions across all
                    branches and departments.
                  </p>
                ) : user.scopes?.length ? (
                  <div
                    className="w-full overflow-x-auto overscroll-x-contain [&_table]:w-full [&_table]:min-w-[560px] [&_table]:border-collapse [&_th]:px-3 [&_th]:py-2 [&_td]:px-3 [&_td]:py-2 [&_th]:border-b [&_td]:border-b [&_th]:border-border [&_td]:border-border [&_th]:text-left [&_td]:text-left [&_th]:align-top [&_td]:align-top"
                    tabIndex={0}
                    aria-label="Access by scope table"
                  >
                    <table>
                      <thead>
                        <tr>
                          <th>Branch</th>
                          <th>Department</th>
                          <th>Permissions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {user.scopes.map((scope) => (
                          <tr key={`${scope.branchId}-${scope.departmentId}`}>
                            <td>
                              {branches?.find(
                                (b) => String(b.id) === String(scope.branchId),
                              )?.name ?? scope.branchId}
                            </td>
                            <td>
                              {departments?.find(
                                (d) =>
                                  String(d.id) === String(scope.departmentId),
                              )?.name ?? scope.departmentId}
                            </td>
                            <td>{scope.permissions.join(', ') || 'None'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p>No business access.</p>
                )}
              </section>
            </>
          ) : (
            <PageLoading message="Loading current user…" />
          )}
        </Card.Body>
      </Card>
    </main>
  );
}
