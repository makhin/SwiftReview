import { useQuery } from '@tanstack/react-query';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canOpenMessagesPage, canOpenReviewQueue } from '../../shared/auth/permissions';
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

  if (!currentUserQuery.data || !canOpenMessagesPage(currentUserQuery.data.permissions, currentUserQuery.data.isGlobalAdministrator)) {
    const user = currentUserQuery.data;
    const destination = user && canOpenReviewQueue(user.permissions, user.isGlobalAdministrator)
      ? '/messages/assigned' : user?.isGlobalAdministrator ? '/admin' : '/me';
    return <UserPreservingNavigate to={destination} replace />;
  }

  return (
    <main className="app-content app-page app-page--wide">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">Messages</h1>
          <p className="app-page-subtitle">Find, assign, and manage messages across your accessible scopes.</p>
        </div>
      </header>
      <MessagesGrid dataSource={messageDataSource} enableUndoActions enableWorkflowActions />
    </main>
  );
}
