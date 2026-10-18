export default function PageHeader({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <header className="flex flex-col gap-2 min-w-0">
      <h1 className="text-2xl font-semibold leading-tight">{title}</h1>
      <p className="text-fg-muted">{description}</p>
    </header>
  );
}
