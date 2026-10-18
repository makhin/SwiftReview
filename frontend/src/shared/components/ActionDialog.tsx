import { Dialog } from '@smbc/ui';
import type { ComponentProps, PropsWithChildren } from 'react';

type Props = Omit<
  ComponentProps<typeof Dialog>,
  'open' | 'showCloseButton' | 'closeOnOutsideClick'
> & {
  busy?: boolean;
  isLocked?: () => boolean;
};
export default function ActionDialog({
  busy = false,
  isLocked,
  children,
  onClose,
  devExtremeProps,
  ...props
}: Props) {
  return (
    <Dialog
      {...props}
      open
      width="90vw"
      showCloseButton={!busy}
      closeOnOutsideClick={!busy}
      onClose={() => {
        if (!busy && !isLocked?.()) onClose?.();
      }}
      devExtremeProps={{
        showTitle: true,
        dragEnabled: false,
        height: 'auto',
        maxWidth: 520,
        ...devExtremeProps,
      }}
    >
      <div className="flex flex-col gap-4 p-4">{children}</div>
    </Dialog>
  );
}
export function DialogActions({ children }: PropsWithChildren) {
  return <div className="flex flex-row-reverse flex-wrap justify-start gap-2">{children}</div>;
}
