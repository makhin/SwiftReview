import { useQuery } from '@tanstack/react-query';
import { smbcLogoUrl } from '@smbc/devextreme-theme/assets';
import { Button } from '@smbc/ui';
import { NavLink, useLocation } from 'react-router-dom';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { preserveUserQuery } from '../../shared/routing/preserveUserQuery';

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
  const location = useLocation();
  const {
    data: user,
    isError,
    isPending,
  } = useQuery(currentUserQueryOptions());
  const userLabel = isPending
    ? 'Loading user…'
    : isError
      ? 'User unavailable'
      : user?.userName;

  return (
    <header className="global-header sticky top-0 z-100 min-h-[var(--global-header-height)] bg-header border-b-[length:var(--global-header-border-width)] border-header-border [--focus-ring-color:var(--focus-ring-color-on-dark)]">
      <div className="relative flex items-center justify-between w-full min-h-[calc(var(--global-header-height)-var(--global-header-border-width))] mx-auto p-4 md:py-5">
        <div className="flex items-center min-w-0 gap-3">
          {showNavigationToggle ? (
            <Button
              ariaLabel={
                navigationOpen ? 'Close navigation' : 'Open navigation'
              }
              icon="menu"
              onClick={onNavigationToggle}
              variant="tertiary"
              devExtremeProps={{
                className: 'global-header__navigation-button',
                elementAttr: {
                  'aria-label': navigationOpen
                    ? 'Close navigation'
                    : 'Open navigation',
                  'aria-controls': 'application-navigation',
                  'aria-expanded': navigationOpen,
                },
              }}
            />
          ) : null}

          <NavLink
            className="global-header__brand inline-flex items-center gap-4 text-fg-inverse no-underline hover:no-underline"
            to={preserveUserQuery('/', location.search)}
            aria-label="SMBC home"
          >
            <img
              className="block w-28 h-auto md:w-[146px] md:h-[42px]"
              src={smbcLogoUrl}
              alt="SMBC"
              width="146"
              height="42"
            />
            <span className="hidden md:block pl-4 border-l border-border-inverse text-nav-secondary text-lg font-light tracking-[0.02em]">
              Operations Reporting and Processing
            </span>
          </NavLink>
        </div>

        <div
          className="flex items-center min-w-0 gap-2 text-fg-inverse text-sm max-md:max-w-[35%]"
          aria-live="polite"
        >
          <i className="dx-icon-user" aria-hidden="true" />
          <span className="overflow-hidden text-ellipsis whitespace-nowrap">
            {userLabel}
          </span>
        </div>
      </div>
    </header>
  );
}
