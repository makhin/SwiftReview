import { QueryClient } from '@tanstack/react-query';

import { ApiRequestError } from '../../shared/api/errors';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        if (error instanceof ApiRequestError && error.kind === 'aborted') return false;
        const status = 'status' in error ? error.status : undefined;

        if (typeof status === 'number' && status >= 400 && status < 500) {
          return false;
        }

        return failureCount < 2;
      },
    },
  },
});

export default queryClient;
