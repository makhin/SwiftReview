import PageHeader from '../../shared/components/PageHeader';
import PageLayout from '../../shared/components/PageLayout';
import { useQuery } from '@tanstack/react-query';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import {
  canOpenMessagesPage,
  canOpenReviewQueue,
} from '../../shared/auth/permissions';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import UserPreservingNavigate from '../../shared/routing/UserPreservingNavigate';
import { messageDataSource } from './api/messageDataSource';
import MessagesGrid from './components/MessagesGrid/MessagesGrid';

export default function MessagesPage() {
  const currentUserQuery = useQuery(currentUserQueryOptions());

  if (currentUserQuery.isPending) {
    return (
      <PageLayout>
        <PageLoading message="Loading messages…" />
      </PageLayout>
    );
  }

  if (currentUserQuery.error) {
    return (
      <PageLayout>
        <PageError
          title="Unable to verify access"
          message="Check your connection and try again."
          actionLabel="Retry"
          onAction={() => void currentUserQuery.refetch()}
        />
      </PageLayout>
    );
  }

  if (
    !currentUserQuery.data ||
    !canOpenMessagesPage(
      currentUserQuery.data.permissions,
      currentUserQuery.data.isGlobalAdministrator,
    )
  ) {
    const user = currentUserQuery.data;
    const destination =
      user && canOpenReviewQueue(user.permissions, user.isGlobalAdministrator)
        ? '/messages/assigned'
        : user?.isGlobalAdministrator
          ? '/admin'
          : '/me';
    return <UserPreservingNavigate to={destination} replace />;
  }

  return (
    <PageLayout wide>
      <PageHeader
        title="Messages"
        description="Find, assign, and manage messages across your accessible scopes."
      />
      <MessagesGrid
        dataSource={messageDataSource}
        enableUndoActions
        enableWorkflowActions
      />
    </PageLayout>
  );
}
