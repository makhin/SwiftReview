type PageLoadingProps = {
  message: string;
};

export default function PageLoading({ message }: PageLoadingProps) {
  return <div role="status">{message}</div>;
}
