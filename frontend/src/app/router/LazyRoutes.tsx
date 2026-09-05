import { lazy } from 'react';

export const CurrentUserPage = lazy(
  () => import('../../pages/current-user/CurrentUserPage'),
);
export const AssignedMessagesPage = lazy(
  () => import('../../pages/messages/AssignedMessagesPage'),
);
export const MessagesPage = lazy(() => import('../../pages/messages/MessagesPage'));
