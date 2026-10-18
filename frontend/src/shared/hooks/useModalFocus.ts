import { useEffect, useEffectEvent, type RefObject } from 'react';

const selector =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

// Used by overlay navigation and audit. Inline desktop navigation remains non-modal.
export function useModalFocus(
  panel: RefObject<HTMLElement | null>,
  open: boolean,
  onClose: () => void,
  inertSelectors: string[],
) {
  const close = useEffectEvent(onClose);
  const targets = inertSelectors.join(',');
  useEffect(() => {
    if (!open) return;
    const trigger =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    const inertElements = Array.from(
      document.querySelectorAll<HTMLElement>(targets),
    ).map((element) => ({ element, inert: element.inert }));
    inertElements.forEach(({ element }) => {
      element.inert = true;
    });
    const controls = () =>
      Array.from(
        panel.current?.querySelectorAll<HTMLElement>(selector) ?? [],
      ).filter(
        (element) =>
          element.getAttribute('aria-disabled') !== 'true' &&
          element.getClientRects().length > 0,
      );
    const focusFirst = () => controls()[0]?.focus();
    const frame = requestAnimationFrame(focusFirst);
    const keydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        close();
      }
      if (event.key !== 'Tab') return;
      const elements = controls();
      const first = elements[0],
        last = elements.at(-1);
      if (!first) {
        event.preventDefault();
        return;
      }
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    const focusin = (event: FocusEvent) => {
      if (!panel.current?.contains(event.target as Node)) focusFirst();
    };
    document.addEventListener('keydown', keydown);
    document.addEventListener('focusin', focusin);
    return () => {
      cancelAnimationFrame(frame);
      document.removeEventListener('keydown', keydown);
      document.removeEventListener('focusin', focusin);
      inertElements.forEach(({ element, inert }) => {
        element.inert = inert;
      });
      if (trigger?.isConnected) trigger.focus();
    };
  }, [open, panel, targets]);
}
