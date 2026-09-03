import { QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';

import queryClient from './queryClient';
import ReferenceDataPreloader from './ReferenceDataPreloader';

export default function AppProviders({ children }: PropsWithChildren) {
  return (
    <QueryClientProvider client={queryClient}>
      <ReferenceDataPreloader />
      {children}
    </QueryClientProvider>
  );
}
