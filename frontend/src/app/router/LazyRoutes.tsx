import { lazy } from 'react';

export const CurrentUserPage = lazy(
  () => import('../../pages/current-user/CurrentUserPage'),
);
export const MessagesPage = lazy(() => import('../../pages/messages/MessagesPage'));
