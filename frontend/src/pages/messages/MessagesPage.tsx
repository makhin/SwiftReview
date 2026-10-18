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
      <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6">
        <PageLoading message="Loading messages…" />
      </main>
    );
  }

  if (currentUserQuery.error) {
    return (
      <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6">
        <PageError
          title="Unable to verify access"
          message="Check your connection and try again."
          actionLabel="Retry"
          onAction={() => void currentUserQuery.refetch()}
        />
      </main>
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
    <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6 max-w-none">
      <header className="flex flex-col items-stretch justify-between gap-4 md:flex-row md:items-start">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold leading-tight">Messages</h1>
          <p className="mt-2 text-fg-muted">
            Find, assign, and manage messages across your accessible scopes.
          </p>
        </div>
      </header>
      <MessagesGrid
        dataSource={messageDataSource}
        enableUndoActions
        enableWorkflowActions
      />
    </main>
  );
}
