import { useQuery } from '@tanstack/react-query';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import {
  canOpenMessagesPage,
  canOpenReviewQueue,
} from '../../shared/auth/permissions';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import UserPreservingNavigate from '../../shared/routing/UserPreservingNavigate';
import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { messageDataSource } from './api/messageDataSource';
import MessagesGrid from './components/MessagesGrid/MessagesGrid';

export default function AssignedMessagesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  useEffect(() => {
    if (!searchParams.has('scope')) return;
    const next = new URLSearchParams(searchParams);
    next.delete('scope');
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);
  const userQuery = useQuery(currentUserQueryOptions());
  if (userQuery.isPending)
    return (
      <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6">
        <PageLoading message="Loading message review…" />
      </main>
    );
  if (userQuery.error)
    return (
      <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6">
        <PageError
          title="Unable to verify access"
          message="Check your connection and try again."
          actionLabel="Retry"
          onAction={() => void userQuery.refetch()}
        />
      </main>
    );
  const user = userQuery.data;
  if (
    !user ||
    !canOpenReviewQueue(user.permissions, user.isGlobalAdministrator)
  ) {
    const destination =
      user && canOpenMessagesPage(user.permissions, user.isGlobalAdministrator)
        ? '/messages'
        : user?.isGlobalAdministrator
          ? '/admin'
          : '/me';
    return <UserPreservingNavigate to={destination} replace />;
  }
  return (
    <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6 max-w-none">
      <header className="flex flex-col items-stretch justify-between gap-4 md:flex-row md:items-start">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold leading-tight">
            Message Review
          </h1>
          <p className="mt-2 text-fg-muted">
            Review messages available across your accessible scopes.
          </p>
        </div>
      </header>
      <MessagesGrid
        dataSource={messageDataSource}
        enableReviewActions
        enableUndoActions
      />
    </main>
  );
}
