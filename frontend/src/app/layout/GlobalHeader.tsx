import { useQuery } from '@tanstack/react-query';
import Button from 'devextreme-react/button';
import { NavLink } from 'react-router-dom';

import { currentUserQueryOptions } from '../../pages/current-user/currentUserQueries';
import './global-header.css';

type GlobalHeaderProps = {
  navigationOpen: boolean;
  showNavigationToggle: boolean;
  onNavigationToggle: () => void;
};

export default function GlobalHeader({
  navigationOpen,
  showNavigationToggle,
  onNavigationToggle,
}: GlobalHeaderProps) {
  const { data: user, isError, isPending } = useQuery(currentUserQueryOptions());
  const userLabel = isPending
    ? 'Loading user…'
    : isError
      ? 'User unavailable'
      : user?.userName;

  return (
    <header className="global-header">
      <div className="global-header__inner">
        <div className="global-header__leading">
          {showNavigationToggle ? (
            <Button
              className="global-header__navigation-button"
              icon="menu"
              stylingMode="text"
              onClick={onNavigationToggle}
              elementAttr={{
                'aria-label': navigationOpen ? 'Close navigation' : 'Open navigation',
                'aria-controls': 'application-navigation',
                'aria-expanded': navigationOpen,
              }}
            />
          ) : null}

          <NavLink className="global-header__brand" to="/" aria-label="SMBC home">
            <img src="/smbc-logo.svg" alt="SMBC" width="146" height="42" />
            <span>Operations Reporting and Processing</span>
          </NavLink>
        </div>

        <div className="global-header__user" aria-live="polite">
          <i className="dx-icon-user" aria-hidden="true" />
          <span>{userLabel}</span>
        </div>
      </div>
    </header>
  );
}
