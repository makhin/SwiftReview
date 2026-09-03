import Drawer from 'devextreme-react/drawer';
import { Suspense, useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';

import PageLoading from '../../shared/components/feedback/PageLoading';
import AppNavigation from './AppNavigation';
import GlobalHeader from './GlobalHeader';

const MOBILE_LAYOUT_QUERY = '(max-width: 760px)';
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(
    () => typeof window.matchMedia === 'function' && window.matchMedia(query).matches,
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
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <GlobalHeader
        navigationOpen={navigationOpen}
        showNavigationToggle={isMobile}
        onNavigationToggle={() => setMobileNavigationOpen((open) => !open)}
      />
      <Drawer
        className="app-shell"
        opened={navigationOpen}
        openedStateMode={isMobile ? 'overlap' : 'shrink'}
        revealMode="slide"
        position="left"
        minSize={0}
        maxSize={248}
        shading={isMobile}
        closeOnOutsideClick={isMobile}
        animationEnabled={!prefersReducedMotion}
        onOpenedChange={(opened) => {
          if (isMobile) {
            setMobileNavigationOpen(opened);
          }
        }}
        render={() => (
          <AppNavigation onNavigate={() => setMobileNavigationOpen(false)} />
        )}
      >
        <div className="app-main" id="main-content">
          <Suspense
            fallback={
              <div className="app-content app-page">
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
