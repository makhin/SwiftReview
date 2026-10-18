import {
  useEffect,
  useEffectEvent,
  useRef,
  useState,
  type RefObject,
} from 'react';
import { useBlocker } from 'react-router-dom';
import type { EditorHandle } from './useEditorDraft';

export function useEditorNavigation(editor: RefObject<EditorHandle | null>) {
  const pending = useRef<((confirmed: boolean) => void) | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const blocker = useBlocker(
    () => !!(editor.current?.isDirty() || editor.current?.isSaving()),
  );
  function finishConfirmation(confirmed: boolean) {
    const resolve = pending.current;
    pending.current = null;
    setConfirmOpen(false);
    resolve?.(confirmed);
  }
  async function leaveEditor() {
    if (pending.current || editor.current?.isSaving()) return false;
    if (!editor.current?.isDirty()) return true;
    return new Promise<boolean>((resolve) => {
      pending.current = resolve;
      setConfirmOpen(true);
    });
  }
  const handleBlockedNavigation = useEffectEvent(() => {
    if (blocker.state !== 'blocked') return;
    // Keep the first destination if Back/another transition occurs while confirming.
    const transition = blocker;
    let active = true;
    void leaveEditor().then((confirmed) => {
      if (active) {
        if (confirmed) transition.proceed();
        else transition.reset();
      }
    });
    return () => {
      active = false;
    };
  });
  useEffect(() => {
    if (blocker.state === 'blocked') return handleBlockedNavigation();
  }, [blocker.state]);
  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (editor.current?.isDirty() || editor.current?.isSaving()) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => {
      window.removeEventListener('beforeunload', beforeUnload);
      pending.current?.(false);
      pending.current = null;
    };
  }, [editor]);
  return { confirmOpen, finishConfirmation, leaveEditor };
}
