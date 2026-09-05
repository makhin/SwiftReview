import { useQuery } from '@tanstack/react-query';
import List from 'devextreme-react/list';
import { useLocation, useNavigate } from 'react-router-dom';

import { currentUserQueryOptions } from '../../pages/current-user/currentUserQueries';
import { canViewAllMessages } from '../../shared/auth/permissions';

type NavigationItem = {
  path: string;
  text: string;
  icon: string;
};

type AppNavigationProps = {
  onNavigate: () => void;
};

export default function AppNavigation({ onNavigate }: AppNavigationProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const { data: currentUser } = useQuery(currentUserQueryOptions());
  const navigationItems: NavigationItem[] = [
    ...(currentUser && canViewAllMessages(currentUser.permissions)
      ? [{ path: '/messages', text: 'All messages', icon: 'email' }]
      : []),
    { path: '/messages/assigned?scope=mine', text: 'Assigned messages', icon: 'user' },
    { path: '/me', text: 'Current user', icon: 'user' },
  ];

  return (
    <aside className="app-sidebar smbc-sidebar" id="application-navigation">
      <nav aria-label="Application navigation">
        <List
          items={navigationItems}
          keyExpr="path"
          displayExpr="text"
          selectionMode="single"
          selectedItemKeys={[
            location.pathname === '/messages/assigned'
              ? '/messages/assigned?scope=mine'
              : location.pathname,
          ]}
          focusStateEnabled
          activeStateEnabled
          onItemClick={({ itemData }) => {
            const item = itemData as NavigationItem;
            void navigate(item.path);
            onNavigate();
          }}
          elementAttr={{ 'aria-label': 'Application pages' }}
        />
      </nav>
    </aside>
  );
}
