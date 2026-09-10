import { useQuery } from '@tanstack/react-query';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canOpenMessagesPage, canReviewMessages } from '../../shared/auth/permissions';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import UserPreservingNavigate from '../../shared/routing/UserPreservingNavigate';
import Tabs from 'devextreme-react/tabs';
import { useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

import { createMessageDataSource } from './messageDataSource';
import MessagesGrid from './MessagesGrid';

type AssignmentScope = 'mine' | 'departments';

const scopeItems: Array<{ id: AssignmentScope; text: string }> = [
  { id: 'mine', text: 'My work' },
  { id: 'departments', text: 'My departments' },
];

function isAssignmentScope(value: unknown): value is AssignmentScope {
  return value === 'mine' || value === 'departments';
}

export default function AssignedMessagesPage() {
  const userQuery = useQuery(currentUserQueryOptions());
  if (userQuery.isPending) return <main className="app-content app-page"><PageLoading message="Loading review queue…" /></main>;
  if (userQuery.error) return <main className="app-content app-page"><PageError
    title="Unable to verify access" message="Check your connection and try again."
    actionLabel="Retry" onAction={() => void userQuery.refetch()} /></main>;
  const user = userQuery.data;
  if (!user || !canReviewMessages(user.permissions, user.isGlobalAdministrator)) {
    const destination = user && canOpenMessagesPage(user.permissions, user.isGlobalAdministrator) ? '/messages' : user?.isGlobalAdministrator ? '/admin' : '/me';
    return <UserPreservingNavigate to={destination} replace />;
  }
  return <ReviewQueue isGlobalAdministrator={user.isGlobalAdministrator} />;
}

function ReviewQueue({ isGlobalAdministrator }: { isGlobalAdministrator: boolean }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const scopeParam = searchParams.get('scope');
  const scope: AssignmentScope = isAssignmentScope(scopeParam) ? scopeParam : 'mine';

  useEffect(() => {
    if (isAssignmentScope(scopeParam)) {
      return;
    }

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('scope', 'mine');
    setSearchParams(nextParams, { replace: true });
  }, [scopeParam, searchParams, setSearchParams]);

  const dataSource = useMemo(() => createMessageDataSource(scope), [scope]);

  function selectScope(nextScope: AssignmentScope) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('scope', nextScope);
    setSearchParams(nextParams);
  }

  return (
    <main className="app-content app-page app-page--wide">
      <div className="app-toolbar">
        <Tabs
          items={isGlobalAdministrator ? scopeItems.map((item) => item.id === 'departments' ? { ...item, text: 'All messages' } : item) : scopeItems}
          keyExpr="id"
          selectedItemKeys={[scope]}
          selectionMode="single"
          onItemClick={({ itemData }) => {
            if (isAssignmentScope(itemData.id)) {
              selectScope(itemData.id);
            }
          }}
          elementAttr={{ 'aria-label': 'Message assignment scope' }}
        />
      </div>

      <MessagesGrid dataSource={dataSource} enableReviewActions={scope === 'mine'} />
    </main>
  );
}
