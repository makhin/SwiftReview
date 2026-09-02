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
      <button className="app-page-error__action" type="button" onClick={onAction}>
        {actionLabel}
      </button>
    </div>
  );
}
