import type { LoadOptions, LoadResultObject } from 'devextreme/common/data';

import { ApiError } from '../../shared/api/errors';
import type { components } from '../../shared/api/schema';

export type MessageRow = components['schemas']['MessageListItemDto'];

const loadOptionNames = [
  'skip',
  'take',
  'sort',
  'filter',
  'group',
  'totalSummary',
  'groupSummary',
  'select',
  'requireTotalCount',
  'requireGroupCount',
] as const;

function buildQuery(loadOptions: LoadOptions<MessageRow>) {
  const query = new URLSearchParams({
    skip: String(loadOptions.skip ?? 0),
    take: String(loadOptions.take ?? 20),
  });

  for (const name of loadOptionNames) {
    const value = loadOptions[name];

    if (value === undefined || name === 'skip' || name === 'take') {
      continue;
    }

    query.set(name, typeof value === 'object' ? JSON.stringify(value) : String(value));
  }

  return query;
}

export async function getMessageGrid(
  loadOptions: LoadOptions<MessageRow>,
): Promise<LoadResultObject<MessageRow>> {
  try {
    const response = await fetch(`/api/messages/grid?${buildQuery(loadOptions)}`);

    if (!response.ok) {
      throw new ApiError(`Unable to load messages (${response.status}).`, response.status);
    }

    return (await response.json()) as LoadResultObject<MessageRow>;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    throw new Error('Unable to load messages.', { cause: error });
  }
}
