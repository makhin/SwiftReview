import { useQuery } from '@tanstack/react-query';
import Tabs from 'devextreme-react/tabs';
import { useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

import { createMessageDataSource } from './messageDataSource';
import { currentUserQueryOptions } from '../current-user/currentUserQueries';
import { canAssignMessages } from '../../shared/auth/permissions';
import MessagesGrid from './MessagesGrid';

type AssignmentScope = 'mine' | 'departments' | 'assignable';

const standardScopeItems: Array<{ id: AssignmentScope; text: string }> = [
  { id: 'mine', text: 'My work' },
  { id: 'departments', text: 'My departments' },
];

function isAssignmentScope(value: unknown): value is AssignmentScope {
  return value === 'mine' || value === 'departments' || value === 'assignable';
}

export default function AssignedMessagesPage() {
  const currentUserQuery = useQuery(currentUserQueryOptions());
  const [searchParams, setSearchParams] = useSearchParams();
  const scopeParam = searchParams.get('scope');
  const scope: AssignmentScope = isAssignmentScope(scopeParam) ? scopeParam : 'mine';
  const mayAssign = currentUserQuery.data
    ? canAssignMessages(currentUserQuery.data.permissions)
    : false;
  const scopeItems = mayAssign
    ? [...standardScopeItems, { id: 'assignable' as const, text: 'Assignment queue' }]
    : standardScopeItems;

  useEffect(() => {
    if (isAssignmentScope(scopeParam) &&
      (scopeParam !== 'assignable' || currentUserQuery.isPending || mayAssign)) {
      return;
    }

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('scope', 'mine');
    setSearchParams(nextParams, { replace: true });
  }, [currentUserQuery.isPending, mayAssign, scopeParam, searchParams, setSearchParams]);

  const dataSource = useMemo(() => createMessageDataSource(scope), [scope]);

  function selectScope(nextScope: AssignmentScope) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('scope', nextScope);
    setSearchParams(nextParams);
  }

  return (
    <main className="app-content app-page">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">
            {scope === 'assignable' ? 'Assignment queue' : 'Assigned messages'}
          </h1>
          <p className="app-page-subtitle">
            {scope === 'assignable'
              ? 'Messages available for assignment or reassignment in your scope.'
              : 'Messages assigned to you, reviews you own, or work in your departments.'}
          </p>
        </div>
      </header>

      <div className="app-toolbar">
        <Tabs
          items={scopeItems}
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
