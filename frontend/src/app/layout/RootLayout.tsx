import { Drawer } from '@smbc/ui';
import { Suspense, useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';

import PageLoading from '../../shared/components/feedback/PageLoading';
import AppNavigation from './AppNavigation';
import GlobalHeader from './GlobalHeader';

const MOBILE_LAYOUT_QUERY = '(max-width: 767px)';
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(
    () =>
      typeof window.matchMedia === 'function' &&
      window.matchMedia(query).matches,
  );

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return undefined;
    }

    const mediaQuery = window.matchMedia(query);
    const updateMatch = () => setMatches(mediaQuery.matches);

    updateMatch();
    mediaQuery.addEventListener('change', updateMatch);
    return () => mediaQuery.removeEventListener('change', updateMatch);
  }, [query]);

  return matches;
}

export default function RootLayout() {
  const isMobile = useMediaQuery(MOBILE_LAYOUT_QUERY);
  const prefersReducedMotion = useMediaQuery(REDUCED_MOTION_QUERY);
  const [mobileNavigationOpen, setMobileNavigationOpen] = useState(false);
  const navigationOpen = !isMobile || mobileNavigationOpen;

  return (
    <div className="site-layout">
      <a
        className="skip-link fixed top-2 left-2 z-200 px-3 py-2 bg-surface text-link"
        href="#main-content"
      >
        Skip to main content
      </a>
      <GlobalHeader
        navigationOpen={navigationOpen}
        showNavigationToggle={isMobile}
        onNavigationToggle={() => setMobileNavigationOpen((open) => !open)}
      />
      <Drawer
        open={navigationOpen}
        onOpenChange={(opened) => {
          if (isMobile) {
            setMobileNavigationOpen(opened);
          }
        }}
        panel={() => (
          <AppNavigation onNavigate={() => setMobileNavigationOpen(false)} />
        )}
        devExtremeProps={{
          className: 'min-h-[calc(100vh-var(--global-header-height))] bg-page',
          openedStateMode: isMobile ? 'overlap' : 'shrink',
          revealMode: 'slide',
          position: 'left',
          minSize: 0,
          maxSize: 248,
          shading: isMobile,
          closeOnOutsideClick: isMobile,
          animationEnabled: !prefersReducedMotion,
        }}
      >
        <div
          className="min-w-0 min-h-[calc(100vh-var(--global-header-height))]"
          id="main-content"
        >
          <Suspense
            fallback={
              <div className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6">
                <PageLoading message="Loading page…" />
              </div>
            }
          >
            <Outlet />
          </Suspense>
        </div>
      </Drawer>
    </div>
  );
}
