import { isRouteErrorResponse, useRouteError } from 'react-router-dom';

import { ApiError } from '../api/errors';
import PageError from './PageError';

function getStatus(error: unknown) {
  if (error instanceof ApiError) {
    return error.status;
  }

  return isRouteErrorResponse(error) ? error.status : undefined;
}

export default function RouteErrorBoundary() {
  const error = useRouteError();
  const status = getStatus(error);

  let title = 'Unable to open this page';
  let message = 'The page could not be loaded. Please try again.';

  if (status === 401) {
    title = 'Authentication required';
    message = 'Please sign in again to continue.';
  } else if (status === 403) {
    title = 'Access denied';
    message = 'You do not have permission to open this page.';
  } else if (status !== undefined && status >= 500) {
    title = 'Service temporarily unavailable';
    message = 'Please wait a moment and try again.';
  }

  return (
    <main className="app-content app-page">
      <PageError
        title={title}
        message={message}
        actionLabel="Reload page"
        onAction={() => window.location.reload()}
      />
    </main>
  );
}
