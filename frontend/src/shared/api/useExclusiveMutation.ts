import { useRef } from 'react';
import { useMutation, type UseMutationOptions } from '@tanstack/react-query';

// isPending updates on render; this lock also covers multiple calls in the same event turn.
export function useExclusiveMutation<TData, TVariables>(options: UseMutationOptions<TData, Error, TVariables>) {
  const mutation = useMutation({ ...options, retry: false });
  const inFlight = useRef<Promise<TData> | null>(null);
  function mutateAsync(variables: TVariables) {
    if (inFlight.current) return inFlight.current;
    inFlight.current = mutation.mutateAsync(variables).finally(() => { inFlight.current = null; });
    return inFlight.current;
  }
  return {
    ...mutation,
    mutateAsync,
    mutate: (variables: TVariables) => { void mutateAsync(variables).catch(() => {}); },
    isLocked: () => inFlight.current !== null,
  };
}
