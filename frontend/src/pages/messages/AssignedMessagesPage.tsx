import { useQuery } from '@tanstack/react-query';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canOpenMessagesPage, canOpenReviewQueue } from '../../shared/auth/permissions';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import UserPreservingNavigate from '../../shared/routing/UserPreservingNavigate';
import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { messageDataSource } from './messageDataSource';
import MessagesGrid from './MessagesGrid';

export default function AssignedMessagesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  useEffect(() => {
    if (!searchParams.has('scope')) return;
    const next = new URLSearchParams(searchParams);
    next.delete('scope');
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);
  const userQuery = useQuery(currentUserQueryOptions());
  if (userQuery.isPending) return <main className="app-content app-page"><PageLoading message="Loading message review…" /></main>;
  if (userQuery.error) return <main className="app-content app-page"><PageError
    title="Unable to verify access" message="Check your connection and try again."
    actionLabel="Retry" onAction={() => void userQuery.refetch()} /></main>;
  const user = userQuery.data;
  if (!user || !canOpenReviewQueue(user.permissions, user.isGlobalAdministrator)) {
    const destination = user && canOpenMessagesPage(user.permissions, user.isGlobalAdministrator) ? '/messages' : user?.isGlobalAdministrator ? '/admin' : '/me';
    return <UserPreservingNavigate to={destination} replace />;
  }
  return <main className="app-content app-page app-page--wide">
    <header className="app-page-header">
      <div className="app-page-header__main">
        <h1 className="app-page-title">Message Review</h1>
        <p className="app-page-subtitle">Review messages available across your accessible scopes.</p>
      </div>
    </header>
    <MessagesGrid dataSource={messageDataSource} enableReviewActions />
  </main>;
}
