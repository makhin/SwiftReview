import { Button } from '@smbc/ui';
import { useId, useRef, useState, type PropsWithChildren } from 'react';

// Native popover provides outside-click/Escape dismissal without grid clipping.
export default function ActionMenu({
  children,
  id,
}: PropsWithChildren<{ id: string }>) {
  const menuId = useId();
  const trigger = useRef<HTMLSpanElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  function show() {
    const bounds = trigger.current?.getBoundingClientRect();
    if (!bounds || !menu.current) return;
    menu.current.style.top = `${Math.min(bounds.bottom + 4, innerHeight - 300)}px`;
    menu.current.style.left = `${Math.max(8, Math.min(bounds.right - 210, innerWidth - 218))}px`;
    menu.current.togglePopover();
  }
  return (
    <span ref={trigger}>
      <Button
        variant="secondary"
        onClick={show}
        devExtremeProps={{
          elementAttr: { id, 'aria-expanded': open, 'aria-controls': menuId },
        }}
      >
        More
      </Button>
      <div
        ref={menu}
        id={menuId}
        popover="auto"
        role="group"
        aria-label="More message actions"
        className="m-0 p-2 w-[210px] max-h-[80vh] overflow-auto bg-surface border border-border rounded-card shadow-popup-elevation"
        onToggle={(event) => {
          const shown = event.newState === 'open';
          setOpen(shown);
          if (shown)
            menu.current
              ?.querySelector<HTMLElement>(
                '[role="button"]:not([aria-disabled="true"])',
              )
              ?.focus();
        }}
        onKeyDown={(event) => {
          if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key))
            return;
          const buttons = Array.from(
            menu.current?.querySelectorAll<HTMLElement>(
              '[role="button"]:not([aria-disabled="true"])',
            ) ?? [],
          );
          if (!buttons.length) return;
          event.preventDefault();
          const index = buttons.indexOf(document.activeElement as HTMLElement);
          const next =
            event.key === 'Home'
              ? 0
              : event.key === 'End'
                ? buttons.length - 1
                : (index +
                    (event.key === 'ArrowDown' ? 1 : -1) +
                    buttons.length) %
                  buttons.length;
          buttons[next]?.focus();
        }}
        onClick={(event) => {
          if (
            (event.target as HTMLElement).closest(
              '[role="button"]:not([aria-disabled="true"])',
            )
          )
            menu.current?.hidePopover();
        }}
      >
        <div className="flex flex-col items-stretch gap-2">{children}</div>
      </div>
    </span>
  );
}
