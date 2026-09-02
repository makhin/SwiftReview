import { createBrowserRouter, Navigate } from 'react-router-dom';

import DesignSystemPage from '../design-system/DesignSystemPage';
import MessagesPage from '../messages/MessagesPage';
import RootLayout from './RootLayout';

const router = createBrowserRouter([
  {
    element: <RootLayout />,
    children: [
      { path: '/', element: <Navigate to="/design-system" replace /> },
      { path: '/design-system', element: <DesignSystemPage /> },
      { path: '/messages', element: <MessagesPage /> },
    ],
  },
]);

export default router;
