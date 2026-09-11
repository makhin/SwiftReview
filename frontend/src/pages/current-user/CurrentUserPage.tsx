import { useQuery } from '@tanstack/react-query';

import { ApiError } from '../../shared/api/errors';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import {
  branchesQueryOptions,
  departmentsQueryOptions,
} from '../../shared/api/referenceDataQueries';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import './current-user.css';

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
  const { data: user, error, isPending, refetch } = useQuery(currentUserQueryOptions());
  const { data: branches } = useQuery({ ...branchesQueryOptions(), enabled: !!user?.permissions.includes('message.view') });
  const { data: departments } = useQuery({ ...departmentsQueryOptions(), enabled: !!user?.permissions.includes('message.view') });
  const errorContent = error ? getErrorContent(error) : undefined;

  return (
    <main className="app-content app-page">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">{user?.displayName ?? 'Current user'}</h1>
          <p className="app-page-subtitle">Identity and access details.</p>
        </div>
      </header>

      <div className="app-card">
        <div className="app-card__header">
          <div className="app-card__title">Profile</div>
        </div>
        <div className="app-card__body" aria-busy={isPending}>
          {errorContent ? (
            <PageError
              title={errorContent.title}
              message={errorContent.message}
              actionLabel="Retry"
              onAction={() => void refetch()}
            />
          ) : user ? (
            <>
              <dl className="app-details">
                <dt>User ID</dt>
                <dd>{user.userId}</dd>
                <dt>User name</dt>
                <dd>{user.userName}</dd>
                <dt>Global administrator</dt>
                <dd>{user.isGlobalAdministrator ? 'Yes' : 'No'}</dd>
              </dl>
              <section className="current-user-access" aria-labelledby="access-by-scope-title">
                <h2 id="access-by-scope-title">Access by Scope</h2>
                {user.isGlobalAdministrator ? (
                  <p>Full access to all information and actions across all branches and departments.</p>
                ) : user.scopes?.length ? (
                  <div className="app-table-scroll" tabIndex={0} aria-label="Access by scope table">
                    <table>
                      <thead><tr><th>Branch</th><th>Department</th><th>Permissions</th></tr></thead>
                      <tbody>{user.scopes.map((scope) => <tr key={`${scope.branchId}-${scope.departmentId}`}>
                        <td>{branches?.find((b) => String(b.id) === String(scope.branchId))?.name ?? scope.branchId}</td>
                        <td>{departments?.find((d) => String(d.id) === String(scope.departmentId))?.name ?? scope.departmentId}</td>
                        <td>{scope.permissions.join(', ') || 'None'}</td>
                      </tr>)}</tbody>
                    </table>
                  </div>
                ) : <p>No business access.</p>}
              </section>
            </>
          ) : (
            <PageLoading message="Loading current user…" />
          )}
        </div>
      </div>
    </main>
  );
}
