import { useQuery } from '@tanstack/react-query';
import List from 'devextreme-react/list';
import { useLocation, useNavigate } from 'react-router-dom';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canViewAllMessages } from '../../shared/auth/permissions';
import { preserveUserQuery } from '../../shared/routing/preserveUserQuery';

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
      ? [{ path: '/messages', text: 'Messages', icon: 'email' }]
      : []),
    { path: '/messages/assigned?scope=mine', text: 'Review queue', icon: 'todo' },
    ...(currentUser?.isGlobalAdministrator ? [{ path: '/admin', text: 'Users & access', icon: 'preferences' }] : []),
    { path: '/me', text: 'User profile', icon: 'user' },
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
            void navigate(preserveUserQuery(item.path, location.search));
            onNavigate();
          }}
          elementAttr={{ 'aria-label': 'Application pages' }}
        />
      </nav>
    </aside>
  );
}
