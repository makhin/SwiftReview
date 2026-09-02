import { useQuery } from '@tanstack/react-query';

import { ApiError } from '../../shared/api/errors';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import { currentUserQueryOptions } from './currentUserQueries';

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
  const errorContent = error ? getErrorContent(error) : undefined;

  return (
    <main className="app-content app-page">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">Current user</h1>
          <p className="app-page-subtitle">
            Identity and access details loaded from the backend.
          </p>
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
            <dl className="app-details">
              <dt>User ID</dt>
              <dd>{user.userId}</dd>
              <dt>User name</dt>
              <dd>{user.userName}</dd>
              <dt>Permissions</dt>
              <dd>{user.permissions.join(', ') || 'None'}</dd>
              <dt>Branches</dt>
              <dd>{user.branches.join(', ') || 'None'}</dd>
              <dt>Departments</dt>
              <dd>{user.departments.join(', ') || 'None'}</dd>
            </dl>
          ) : (
            <PageLoading message="Loading current user…" />
          )}
        </div>
      </div>
    </main>
  );
}
