import { Suspense } from 'react';
import { Outlet } from 'react-router-dom';

import PageLoading from '../../shared/components/feedback/PageLoading';
import GlobalHeader from './GlobalHeader';

export default function RootLayout() {
  return (
    <div className="site-layout">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <GlobalHeader />
      <div id="main-content">
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
    </div>
  );
}
