import { useQuery } from '@tanstack/react-query';
import List from 'devextreme-react/list';
import { NavLink, useLocation } from 'react-router-dom';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canOpenMessagesPage, canOpenReviewQueue } from '../../shared/auth/permissions';
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
  const { data: currentUser } = useQuery(currentUserQueryOptions());
  const navigationItems: NavigationItem[] = [
    ...(currentUser && canOpenMessagesPage(currentUser.permissions, currentUser.isGlobalAdministrator)
      ? [{ path: '/messages', text: 'Messages', icon: 'email' }]
      : []),
    ...(currentUser && canOpenReviewQueue(currentUser.permissions, currentUser.isGlobalAdministrator)
      ? [{ path: '/messages/assigned', text: 'Message Review', icon: 'todo' }]
      : []),
    ...(currentUser?.isGlobalAdministrator ? [{ path: '/admin', text: 'Users & access', icon: 'preferences' }] : []),
    { path: '/me', text: 'User profile', icon: 'user' },
  ];

  return (
    <aside className="app-sidebar smbc-sidebar" id="application-navigation">
      <nav aria-label="Application navigation">
        <List
          items={navigationItems}
          keyExpr="path"
          selectionMode="none"
          focusStateEnabled={false}
          activeStateEnabled={false}
          itemRender={(item: NavigationItem) => (
            <NavLink
              className={({ isActive }) => `app-navigation__link${isActive ? ' app-navigation__link--active' : ''}`}
              to={preserveUserQuery(item.path, location.search)}
              end
              onClick={onNavigate}
            >
              <i className={`dx-icon-${item.icon}`} aria-hidden="true" />
              <span>{item.text}</span>
            </NavLink>
          )}
          elementAttr={{ 'aria-label': 'Application pages' }}
        />
      </nav>
    </aside>
  );
}
