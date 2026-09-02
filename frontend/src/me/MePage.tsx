import { useQuery } from '@tanstack/react-query';

import { currentUserQueryOptions } from './currentUserQueries';

export default function MePage() {
  const { data: user, error, isPending, refetch } = useQuery(currentUserQueryOptions());

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
          {error ? (
            <div className="app-callout app-callout--danger" role="alert">
              <div>{error.message}</div>
              <button type="button" onClick={() => void refetch()}>
                Retry
              </button>
            </div>
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
            <div role="status">Loading current user…</div>
          )}
        </div>
      </div>
    </main>
  );
}
