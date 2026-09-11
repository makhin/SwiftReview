import LoadIndicator from 'devextreme-react/load-indicator';

type PageLoadingProps = {
  message: string;
};

export default function PageLoading({ message }: PageLoadingProps) {
  return <div className="app-page-loading" role="status">
    <LoadIndicator height={24} width={24} elementAttr={{ 'aria-hidden': 'true' }} />
    <span>{message}</span>
  </div>;
}
