import { useQuery } from '@tanstack/react-query';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canViewAllMessages } from '../../shared/auth/permissions';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import UserPreservingNavigate from '../../shared/routing/UserPreservingNavigate';
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
    return <UserPreservingNavigate to="/messages/assigned?scope=mine" replace />;
  }

  return (
    <main className="app-content app-page app-page--wide">
      <MessagesGrid dataSource={messageDataSource} />
    </main>
  );
}
