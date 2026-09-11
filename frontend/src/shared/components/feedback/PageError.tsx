import Button from 'devextreme-react/button';

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
    <div className="app-callout app-callout--danger app-page-error" role="alert">
      <strong>{title}</strong>
      <p>{message}</p>
      <Button text={actionLabel} stylingMode="outlined" onClick={onAction} />
    </div>
  );
}
