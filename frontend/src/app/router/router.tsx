import { createBrowserRouter } from 'react-router-dom';

import RootLayout from '../layout/RootLayout';
import UserPreservingNavigate from '../../shared/routing/UserPreservingNavigate';
import { AssignedMessagesPage, CurrentUserPage, MessagesPage } from './LazyRoutes';
import RouteErrorBoundary from './RouteErrorBoundary';

const router = createBrowserRouter([
  {
    element: <RootLayout />,
    errorElement: <RouteErrorBoundary />,
    children: [
      { path: '/', element: <UserPreservingNavigate to="/messages" replace /> },
      { path: '/me', element: <CurrentUserPage /> },
      { path: '/messages', element: <MessagesPage /> },
      { path: '/messages/assigned', element: <AssignedMessagesPage /> },
    ],
  },
]);

export default router;
