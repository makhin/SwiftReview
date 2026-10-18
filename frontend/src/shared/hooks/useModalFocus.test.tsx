import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useRef } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { useModalFocus } from './useModalFocus';

afterEach(() => vi.restoreAllMocks());
function Panel({ close }: { close: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  useModalFocus(ref, true, close, ['#background']);
  return (
    <div ref={ref}>
      <button>First</button>
      <button>Last</button>
    </div>
  );
}
it('traps focus, closes on Escape and restores focus and background on unmount', async () => {
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([
    new DOMRect(0, 0, 50, 30),
  ] as unknown as DOMRectList);
  const background = document.createElement('div');
  background.id = 'background';
  const trigger = document.createElement('button');
  background.append(trigger);
  document.body.append(background);
  trigger.focus();
  const close = vi.fn();
  const { unmount } = render(<Panel close={close} />);
  await waitFor(() => expect(screen.getByText('First')).toHaveFocus());
  expect(background.inert).toBe(true);
  fireEvent.keyDown(screen.getByText('First'), { key: 'Tab', shiftKey: true });
  expect(screen.getByText('Last')).toHaveFocus();
  fireEvent.keyDown(screen.getByText('Last'), { key: 'Tab' });
  expect(screen.getByText('First')).toHaveFocus();
  fireEvent.keyDown(document, { key: 'Escape' });
  expect(close).toHaveBeenCalledOnce();
  unmount();
  expect(background.inert).toBeFalsy();
  expect(trigger).toHaveFocus();
  background.remove();
});
