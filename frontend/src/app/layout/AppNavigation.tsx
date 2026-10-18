import { useQuery } from '@tanstack/react-query';
import { NavLink, useLocation } from 'react-router-dom';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import {
  canOpenMessagesPage,
  canOpenReviewQueue,
} from '../../shared/auth/permissions';
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
    ...(currentUser &&
    canOpenMessagesPage(
      currentUser.permissions,
      currentUser.isGlobalAdministrator,
    )
      ? [{ path: '/messages', text: 'Messages', icon: 'email' }]
      : []),
    ...(currentUser &&
    canOpenReviewQueue(
      currentUser.permissions,
      currentUser.isGlobalAdministrator,
    )
      ? [{ path: '/messages/assigned', text: 'Message Review', icon: 'todo' }]
      : []),
    ...(currentUser?.isGlobalAdministrator
      ? [{ path: '/admin', text: 'Users & access', icon: 'preferences' }]
      : []),
    { path: '/me', text: 'User profile', icon: 'user' },
  ];

  return (
    <aside
      className="w-[248px] h-full min-h-[calc(100vh-var(--global-header-height))] overflow-y-auto bg-nav text-fg-inverse border-r border-border-inverse-subtle [--focus-ring-color:var(--focus-ring-color-on-dark)] smbc-sidebar"
      id="application-navigation"
    >
      <nav className="py-4" aria-label="Application navigation">
        <ul className="m-0 list-none p-0" aria-label="Application pages">
          {navigationItems.map((item) => (
            <li key={item.path}>
              <NavLink
                className={({ isActive }) =>
                  `flex items-center gap-3 w-full min-h-11 px-4 py-2 text-fg-inverse hover:text-fg-inverse hover:no-underline hover:bg-nav-hover${isActive ? ' bg-nav-active shadow-[inset_var(--border-width-emphasis)_0_0_var(--color-action-accent)]' : ''}`
                }
                to={preserveUserQuery(item.path, location.search)}
                end
                onClick={onNavigate}
              >
                <i
                  className={`dx-icon-${item.icon} shrink-0 size-5 text-xl leading-5`}
                  aria-hidden="true"
                />
                <span>{item.text}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}
