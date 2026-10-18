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
      <PageLayout>
        <PageLoading message="Loading message review…" />
      </PageLayout>
    );
  if (userQuery.error)
    return (
      <PageLayout>
        <PageError
          title="Unable to verify access"
          message="Check your connection and try again."
          actionLabel="Retry"
          onAction={() => void userQuery.refetch()}
        />
      </PageLayout>
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
    <PageLayout wide>
      <PageHeader
        title="Message Review"
        description="Review messages available across your accessible scopes."
      />
      <MessagesGrid
        dataSource={messageDataSource}
        enableReviewActions
        enableUndoActions
      />
    </PageLayout>
  );
}
