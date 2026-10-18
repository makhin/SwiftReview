import { useCallback, useRef, useState, type PropsWithChildren } from 'react';
import {
  Toast,
} from '@smbc/ui';
import { NotificationContext, type Notify } from './notifications';

type Notice = {
  id: number;
  message: string;
  tone: 'info' | 'success' | 'warning' | 'error';
  duration: number;
};
export default function NotificationProvider({ children }: PropsWithChildren) {
  const nextId = useRef(0);
  const [queue, setQueue] = useState<Notice[]>([]);
  const notify = useCallback<Notify>(
    (message, tone = 'info', duration = 4000) => {
      const notice = { id: ++nextId.current, message, tone, duration };
      setQueue((current) => [...current, notice]);
    },
    [],
  );
  const current = queue[0];
  return (
    <NotificationContext value={notify}>
      {children}
      {current && (
        <Toast
          key={current.id}
          open
          message={current.message}
          tone={current.tone}
          duration={current.duration}
          onClose={() =>
            setQueue((pending) =>
              pending.filter((notice) => notice.id !== current.id),
            )
          }
        />
      )}
    </NotificationContext>
  );
}
