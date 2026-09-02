import { createBrowserRouter, Navigate } from 'react-router-dom';

import { DesignSystemPage, MePage, MessagesPage } from './LazyRoutes';
import RootLayout from './RootLayout';
import RouteErrorBoundary from './RouteErrorBoundary';

const router = createBrowserRouter([
  {
    element: <RootLayout />,
    errorElement: <RouteErrorBoundary />,
    children: [
      { path: '/', element: <Navigate to="/design-system" replace /> },
      { path: '/design-system', element: <DesignSystemPage /> },
      { path: '/me', element: <MePage /> },
      { path: '/messages', element: <MessagesPage /> },
    ],
  },
]);

export default router;
