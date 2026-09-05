import CustomStore from 'devextreme/data/custom_store';

import { getMessageGrid } from './messagesApi';
import type { MessageAssignmentScope, MessageRow } from './messagesApi';

export function createMessageDataSource(assignmentScope?: MessageAssignmentScope) {
  return new CustomStore<MessageRow, MessageRow['id']>({
    key: 'id',
    load: (loadOptions) => {
      if (!assignmentScope) {
        return getMessageGrid(loadOptions);
      }

      return getMessageGrid(loadOptions, assignmentScope);
    },
  });
}

export const messageDataSource = createMessageDataSource();
