import { Callout, Button } from '@smbc/ui';

type PageErrorProps = {
  title: string;
  message: string;
  actionLabel: string;
  onAction: () => void;
};

export default function PageError({
  title,
  message,
  actionLabel,
  onAction,
}: PageErrorProps) {
  return (
    <Callout
      tone="danger"
      className="flex flex-col items-start gap-2"
      role="alert"
    >
      <strong>{title}</strong>
      <p>{message}</p>
      <Button onClick={onAction} variant="secondary" children={actionLabel} />
    </Callout>
  );
}
