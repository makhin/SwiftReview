import { QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';

import queryClient from './queryClient';

export default function AppProviders({ children }: PropsWithChildren) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
