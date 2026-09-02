import { lazy } from 'react';

export const DesignSystemPage = lazy(
  () => import('../../pages/design-system/DesignSystemPage'),
);
export const CurrentUserPage = lazy(
  () => import('../../pages/current-user/CurrentUserPage'),
);
export const MessagesPage = lazy(() => import('../../pages/messages/MessagesPage'));
