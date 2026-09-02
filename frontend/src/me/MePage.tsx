import { useEffect, useState } from 'react';

import { apiClient } from '../api/client';
import type { components } from '../api/schema';

type CurrentUser = components['schemas']['CurrentUserResponse'];

export default function MePage() {
  const [user, setUser] = useState<CurrentUser>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    const controller = new AbortController();

    const loadCurrentUser = async () => {
      try {
        const { data, response } = await apiClient.GET('/api/me', {
          signal: controller.signal,
        });

        if (!data) {
          throw new Error(`Unable to load the current user (${response.status}).`);
        }

        setUser(data);
      } catch (requestError) {
        if (!controller.signal.aborted) {
          setError(
            requestError instanceof Error
              ? requestError.message
              : 'Unable to load the current user.',
          );
        }
      }
    };

    void loadCurrentUser();

    return () => controller.abort();
  }, []);

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
        <div className="app-card__body" aria-busy={!user && !error}>
          {error ? (
            <div className="app-callout app-callout--danger" role="alert">
              {error}
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
