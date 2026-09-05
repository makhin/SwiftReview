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
});
