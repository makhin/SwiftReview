import {
  CustomStore,
} from '@smbc/ui/data-grid';

import { getMessageGrid } from './messagesApi';
import type { MessageAssignmentScope, MessageRow } from './messagesApi';

export function createMessageDataSource(assignmentScope?: MessageAssignmentScope, departments?: number[]) {
  return new CustomStore<MessageRow, MessageRow['id']>({
    key: 'id',
    loadMode: 'processed',
    load: (loadOptions) => {
      if (departments?.length) return getMessageGrid(loadOptions, assignmentScope, departments);
      if (!assignmentScope) {
        return getMessageGrid(loadOptions);
      }

      return getMessageGrid(loadOptions, assignmentScope);
    },
  });
}

export const messageDataSource = createMessageDataSource();
