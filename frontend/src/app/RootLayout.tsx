import { Outlet } from 'react-router-dom';

import GlobalHeader from './GlobalHeader';

export default function RootLayout() {
  return (
    <div className="site-layout">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <GlobalHeader />
      <div id="main-content">
        <Outlet />
      </div>
    </div>
  );
}
