import type { LoadOptions, LoadResultObject } from 'devextreme/common/data';

import { apiFetch } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type { MessageListItemDto } from '../../shared/api/generated/contracts.generated';

export type MessageRow = MessageListItemDto;
export type MessageAssignmentScope = 'mine' | 'departments';

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

function buildQuery(
  loadOptions: LoadOptions<MessageRow>,
  assignmentScope?: MessageAssignmentScope,
) {
  const query = new URLSearchParams({
    skip: String(loadOptions.skip ?? 0),
    take: String(loadOptions.take ?? 20),
  });

  if (assignmentScope) {
    query.set('assignmentScope', assignmentScope);
  }

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
  assignmentScope?: MessageAssignmentScope,
): Promise<LoadResultObject<MessageRow>> {
  try {
    const response = await apiFetch(
      `/api/messages/grid?${buildQuery(loadOptions, assignmentScope)}`,
    );

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
