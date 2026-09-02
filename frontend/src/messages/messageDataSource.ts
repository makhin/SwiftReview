import type { LoadOptions, LoadResultObject } from 'devextreme/common/data';
import CustomStore from 'devextreme/data/custom_store';

export type MessageRow = {
  id: number;
  externalId: string;
  messageType: string;
  branchId: number;
  departmentId: number;
  state: string;
  receivedAt: string;
  currentAssigneeId: number | null;
  account: string | null;
  currency: string | null;
  amount: number | null;
};

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

export const messageDataSource = new CustomStore<MessageRow, number>({
  key: 'id',
  load: async (loadOptions) => {
    const response = await fetch(`/api/messages/grid?${buildQuery(loadOptions)}`);

    if (!response.ok) {
      throw new Error(`Unable to load messages (${response.status}).`);
    }

    return (await response.json()) as LoadResultObject<MessageRow>;
  },
});
