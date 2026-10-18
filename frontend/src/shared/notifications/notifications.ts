import { createContext, useContext } from 'react';

export type Notify = (
  message: string,
  tone?: 'info' | 'success' | 'warning' | 'error',
  duration?: number,
) => void;
export const NotificationContext = createContext<Notify>(() => {
  throw new Error('NotificationProvider is missing');
});
export const useNotify = () => useContext(NotificationContext);
