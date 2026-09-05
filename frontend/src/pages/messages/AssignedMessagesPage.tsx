import Tabs from 'devextreme-react/tabs';
import { useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

import { createMessageDataSource } from './messageDataSource';
import MessagesGrid from './MessagesGrid';

type AssignmentScope = 'mine' | 'departments';

const scopeItems: Array<{ id: AssignmentScope; text: string }> = [
  { id: 'mine', text: 'Assigned to me' },
  { id: 'departments', text: 'My departments' },
];

function isAssignmentScope(value: unknown): value is AssignmentScope {
  return value === 'mine' || value === 'departments';
}

export default function AssignedMessagesPage() {
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
    <main className="app-content app-page">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">Assigned messages</h1>
          <p className="app-page-subtitle">
            Messages assigned to you or users in your departments.
          </p>
        </div>
      </header>

      <div className="app-toolbar">
        <Tabs
          items={scopeItems}
          keyExpr="id"
          selectedItemKeys={[scope]}
          selectionMode="single"
          onSelectedItemKeysChange={(keys) => {
            if (isAssignmentScope(keys[0])) {
              selectScope(keys[0]);
            }
          }}
          elementAttr={{ 'aria-label': 'Message assignment scope' }}
        />
      </div>

      <MessagesGrid dataSource={dataSource} enableReviewActions={scope === 'mine'} />
    </main>
  );
}
