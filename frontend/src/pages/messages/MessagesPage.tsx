import { useQuery } from '@tanstack/react-query';
import { Navigate } from 'react-router-dom';

import { currentUserQueryOptions } from '../current-user/currentUserQueries';
import { canViewAllMessages } from '../../shared/auth/permissions';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import { messageDataSource } from './messageDataSource';
import MessagesGrid from './MessagesGrid';

export default function MessagesPage() {
  const currentUserQuery = useQuery(currentUserQueryOptions());

  if (currentUserQuery.isPending) {
    return (
      <main className="app-content app-page">
        <PageLoading message="Loading messages…" />
      </main>
    );
  }

  if (currentUserQuery.error) {
    return (
      <main className="app-content app-page">
        <PageError
          title="Unable to verify access"
          message="Check your connection and try again."
          actionLabel="Retry"
          onAction={() => void currentUserQuery.refetch()}
        />
      </main>
    );
  }

  if (!currentUserQuery.data || !canViewAllMessages(currentUserQuery.data.permissions)) {
    return <Navigate to="/messages/assigned?scope=mine" replace />;
  }

  return (
    <main className="app-content app-page">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">All messages</h1>
          <p className="app-page-subtitle">
            Messages available to the current user, loaded from the backend.
          </p>
        </div>
      </header>

      <MessagesGrid dataSource={messageDataSource} />
    </main>
  );
}
