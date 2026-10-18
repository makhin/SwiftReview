import { LoadingIndicator } from '@smbc/ui';

type PageLoadingProps = {
  message: string;
};

export default function PageLoading({ message }: PageLoadingProps) {
  return (
    <div className="flex items-center gap-2" role="status">
      <LoadingIndicator
        size={24}
        devExtremeProps={{ elementAttr: { 'aria-hidden': 'true' } }}
      />
      <span>{message}</span>
    </div>
  );
}
