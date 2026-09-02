import CustomStore from 'devextreme/data/custom_store';

import { getMessageGrid } from './messagesApi';
import type { MessageRow } from './messagesApi';

export const messageDataSource = new CustomStore<MessageRow, MessageRow['id']>({
  key: 'id',
  load: getMessageGrid,
});
