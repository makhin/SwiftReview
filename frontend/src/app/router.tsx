import { createBrowserRouter, Navigate } from 'react-router-dom';

import { DesignSystemPage, MePage, MessagesPage } from './LazyRoutes';
import RootLayout from './RootLayout';

const router = createBrowserRouter([
  {
    element: <RootLayout />,
    children: [
      { path: '/', element: <Navigate to="/design-system" replace /> },
      { path: '/design-system', element: <DesignSystemPage /> },
      { path: '/me', element: <MePage /> },
      { path: '/messages', element: <MessagesPage /> },
    ],
  },
]);

export default router;
