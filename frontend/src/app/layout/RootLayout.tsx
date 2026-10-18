import PageLayout from '../../shared/components/PageLayout';
import { Drawer } from '@smbc/ui';
import { Suspense, useRef, useState } from 'react';
import { Outlet } from 'react-router-dom';

import PageLoading from '../../shared/components/feedback/PageLoading';
import { useMediaQuery } from '../../shared/hooks/useMediaQuery';
import { useModalFocus } from '../../shared/hooks/useModalFocus';
import AppNavigation from './AppNavigation';
import GlobalHeader from './GlobalHeader';

const MOBILE_LAYOUT_QUERY = '(max-width: 767px)';
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

export default function RootLayout() {
  const isMobile = useMediaQuery(MOBILE_LAYOUT_QUERY);
  const prefersReducedMotion = useMediaQuery(REDUCED_MOTION_QUERY);
  const [mobileNavigationOpen, setMobileNavigationOpen] = useState(false);
  const navigationPanel = useRef<HTMLElement>(null);
  useModalFocus(
    navigationPanel,
    isMobile && mobileNavigationOpen,
    () => setMobileNavigationOpen(false),
    ['.global-header', '#main-content'],
  );
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
          <AppNavigation
            panelRef={navigationPanel}
            mobile={isMobile}
            onNavigate={() => setMobileNavigationOpen(false)}
          />
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
              <PageLayout as="div">
                <PageLoading message="Loading page…" />
              </PageLayout>
            }
          >
            <Outlet />
          </Suspense>
        </div>
      </Drawer>
    </div>
  );
}
