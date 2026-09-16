import { QueryClientProvider, QueryObserver } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { createTestQueryClient } from '../../../test/createTestQueryClient';
import { messageKeys, currentUserKey } from '../../../shared/api/queryKeys';
import { referenceDataKeys } from '../../../shared/api/referenceDataQueries';
import { ApiRequestError } from '../../../shared/api/errors';
import { useAssignMessage } from './messageMutations';

const { assignMessage, notify } = vi.hoisted(() => ({ assignMessage: vi.fn(), notify: vi.fn() }));
vi.mock('./messagesApi', () => ({ assignMessage }));
vi.mock('devextreme/ui/notify', () => ({ default: notify }));
beforeEach(() => { vi.resetAllMocks(); assignMessage.mockResolvedValue(undefined); });

function setup(refresh = vi.fn().mockResolvedValue(undefined)) {
  const client = createTestQueryClient();
  const affected = [messageKeys.detail(42), messageKeys.audit(42), messageKeys.candidates('42'), messageKeys.counts];
  const unrelated = [messageKeys.detail(99), referenceDataKeys.workflows, currentUserKey, ['unrelated']];
  [...affected, ...unrelated].forEach((key) => client.setQueryData(key, {}));
  const onSuccess = vi.fn(); const onError = vi.fn();
  const hook = renderHook(() => useAssignMessage(42, { onChanged: refresh, onSuccess, onError }), {
    wrapper: ({ children }: PropsWithChildren) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
  });
  return { ...hook, client, affected, unrelated, onSuccess, onError, refresh };
}

it('invalidates only this message and counters and refreshes CustomStore', async () => {
  const { result, client, affected, unrelated, refresh } = setup();
  await act(async () => { await result.current.mutateAsync({ assignedTo: 2, reassign: false }); });
  for (const key of affected) expect(client.getQueryState(key)?.isInvalidated).toBe(true);
  for (const key of unrelated) expect(client.getQueryState(key)?.isInvalidated).toBe(false);
  expect(refresh).toHaveBeenCalledOnce();
  expect(notify).not.toHaveBeenCalled();
});

it.each(['rejected promise', 'synchronous exception'])('keeps a saved mutation successful when grid refresh fails: %s', async (failure) => {
  const refresh = vi.fn(() => { if (failure === 'synchronous exception') throw new Error('Grid failed'); return Promise.reject(new Error('Grid failed')); });
  const { result, onSuccess, onError } = setup(refresh);
  await act(async () => { await result.current.mutateAsync({ assignedTo: 2, reassign: false }); });
  await waitFor(() => expect(result.current.isSuccess).toBe(true));
  expect(onSuccess).toHaveBeenCalledOnce();
  expect(onError).not.toHaveBeenCalled();
  expect(assignMessage).toHaveBeenCalledOnce();
  expect(notify).toHaveBeenCalledWith(expect.stringContaining('Changes saved'), 'warning', 6000);
});

it('reports failed active-query refetch separately from the saved mutation', async () => {
  const { result, client, onError } = setup();
  const observer = new QueryObserver(client, { queryKey: messageKeys.detail(42), queryFn: () => Promise.reject(new Error('Offline')), staleTime: Infinity, retry: false });
  const unsubscribe = observer.subscribe(() => {});
  try {
    await act(async () => { await result.current.mutateAsync({ assignedTo: 2, reassign: false }); });
    expect(onError).not.toHaveBeenCalled();
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Changes saved'), 'warning', 6000);
  } finally { unsubscribe(); }
});

it('guards repeated calls before render and until refresh completes', async () => {
  let finish!: () => void;
  const refresh = vi.fn(() => new Promise<void>((resolve) => { finish = resolve; }));
  const { result } = setup(refresh);
  let first!: Promise<unknown>; let second!: Promise<unknown>;
  act(() => {
    first = result.current.mutateAsync({ assignedTo: 2, reassign: false });
    second = result.current.mutateAsync({ assignedTo: 3, reassign: false });
  });
  expect(second).toBe(first);
  await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
  act(() => result.current.mutate({ assignedTo: 4, reassign: false }));
  expect(assignMessage).toHaveBeenCalledExactlyOnceWith(42, 2, false);
  await act(async () => { finish(); await first; });
});

it('refreshes uncertain outcomes without reporting them as saved or retrying the write', async () => {
  assignMessage.mockRejectedValue(new ApiRequestError('Unknown result', 'network', { outcomeUnknown: true }));
  const { result, onSuccess, onError, refresh } = setup(vi.fn().mockRejectedValue(new Error('Offline')));
  act(() => result.current.mutate({ assignedTo: 2, reassign: false }));
  await waitFor(() => expect(result.current.isError).toBe(true));
  expect(onSuccess).not.toHaveBeenCalled();
  expect(onError).toHaveBeenCalledOnce();
  expect(refresh).toHaveBeenCalledOnce();
  expect(assignMessage).toHaveBeenCalledOnce();
  expect(notify).toHaveBeenCalledWith(expect.stringContaining('Unable to refresh'), 'warning', 6000);
});
