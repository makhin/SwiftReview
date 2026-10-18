import { describe, expect, it, vi } from 'vitest';

const { getMessageGrid } = vi.hoisted(() => ({ getMessageGrid: vi.fn() }));

vi.mock('./messagesApi', () => ({ getMessageGrid }));

import { createMessageDataSource, messageDataSource } from './messageDataSource';

describe('messageDataSource', () => {
  it('delegates grid loading to the messages API function', async () => {
    const loadOptions = { skip: 20, take: 10 };
    const result = { data: [{ id: 1 }], totalCount: 1 };
    getMessageGrid.mockResolvedValue(result);

    await expect(messageDataSource.load(loadOptions)).resolves.toEqual(result);
    expect(getMessageGrid).toHaveBeenCalledWith(loadOptions);
  });

  it('passes the assignment scope separately from grid load options', async () => {
    getMessageGrid.mockResolvedValue({ data: [], totalCount: 0 });
    const dataSource = createMessageDataSource('mine');

    await dataSource.load({ skip: 0, take: 20 });

    expect(getMessageGrid).toHaveBeenCalledWith({ skip: 0, take: 20 }, 'mine');
  });
  it('keeps multiple department filters separate from DevExtreme options', async () => {
    getMessageGrid.mockResolvedValue({ data: [], totalCount: 0 });
    await createMessageDataSource(undefined, [1, 2]).load({ skip: 0, take: 20 });
    expect(getMessageGrid).toHaveBeenCalledWith({ skip: 0, take: 20 }, undefined, [1, 2]);
  });

  it('reloads a single message independently of the grid page and department filters', async () => {
    const row = { id: 42, canChangeWorkflow: false };
    getMessageGrid.mockResolvedValue({ data: [row] });
    await expect(createMessageDataSource('mine', [20]).byKey(42)).resolves.toEqual(row);
    expect(getMessageGrid).toHaveBeenLastCalledWith({ filter: ['id', '=', 42], take: 1 });
  });

  it('fails clearly when the message is no longer accessible', async () => {
    getMessageGrid.mockResolvedValue({ data: [] });
    await expect(messageDataSource.byKey(42)).rejects.toThrow('Message is no longer available.');
  });
});
