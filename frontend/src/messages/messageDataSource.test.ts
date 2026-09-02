import { describe, expect, it, vi } from 'vitest';

const { getMessageGrid } = vi.hoisted(() => ({ getMessageGrid: vi.fn() }));

vi.mock('./messagesApi', () => ({ getMessageGrid }));

import { messageDataSource } from './messageDataSource';

describe('messageDataSource', () => {
  it('delegates grid loading to the messages API function', async () => {
    const loadOptions = { skip: 20, take: 10 };
    const result = { data: [{ id: 1 }], totalCount: 1 };
    getMessageGrid.mockResolvedValue(result);

    await expect(messageDataSource.load(loadOptions)).resolves.toEqual(result);
    expect(getMessageGrid).toHaveBeenCalledWith(loadOptions);
  });
});
