import { createBrowserRouter, Navigate } from 'react-router-dom';

import RootLayout from '../layout/RootLayout';
import { CurrentUserPage, MessagesPage } from './LazyRoutes';
import RouteErrorBoundary from './RouteErrorBoundary';

const router = createBrowserRouter([
  {
    element: <RootLayout />,
    errorElement: <RouteErrorBoundary />,
    children: [
      { path: '/', element: <Navigate to="/messages" replace /> },
      { path: '/me', element: <CurrentUserPage /> },
      { path: '/messages', element: <MessagesPage /> },
    ],
  },
]);

export default router;
