import DataGrid, {
  Column,
  FilterRow,
  HeaderFilter,
  Lookup,
  Pager,
  Paging,
} from 'devextreme-react/data-grid';
import type CustomStore from 'devextreme/data/custom_store';
import { useQuery } from '@tanstack/react-query';

import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  usersQueryOptions,
} from '../../shared/api/referenceDataQueries';
import type { MessageRow } from './messagesApi';

type MessagesGridProps = {
  dataSource: CustomStore<MessageRow, MessageRow['id']>;
};

export default function MessagesGrid({ dataSource }: MessagesGridProps) {
  const { data: users } = useQuery(usersQueryOptions());
  const { data: branches } = useQuery(branchesQueryOptions());
  const { data: departments } = useQuery(departmentsQueryOptions());
  const { data: messageStates } = useQuery(messageStatesQueryOptions());
  const assigneeUsers = users?.map((user) => {
    const names = user.departmentIds.map(
      (id) => departments?.find((department) => department.id === id)?.name ?? String(id),
    );

    return {
      ...user,
      displayLabel: `${user.displayName} — ${names.length > 0 ? names.join(', ') : 'No departments'}`,
    };
  });

  return (
    <div className="app-table-shell">
      <DataGrid
        dataSource={dataSource}
        remoteOperations
        showBorders={false}
        rowAlternationEnabled
        hoverStateEnabled
        columnAutoWidth
        elementAttr={{ 'aria-label': 'Messages' }}
        noDataText="No messages found"
      >
        <FilterRow visible />
        <HeaderFilter visible />
        <Paging defaultPageSize={20} />
        <Pager
          visible
          showInfo
          showPageSizeSelector
          allowedPageSizes={[10, 20, 50]}
        />

        <Column dataField="externalId" caption="External ID" minWidth={140} />
        <Column dataField="messageType" caption="Message type" minWidth={120} />
        {/* Server-side sorting supports lookup IDs, not their displayed labels. */}
        <Column
          dataField="branchId"
          caption="Branch"
          dataType="number"
          width={90}
          allowSorting={false}
        >
          {branches && (
            <Lookup dataSource={branches} valueExpr="id" displayExpr="name" />
          )}
        </Column>
        <Column
          dataField="departmentId"
          caption="Department"
          dataType="number"
          width={110}
          allowSorting={false}
        >
          {departments && (
            <Lookup dataSource={departments} valueExpr="id" displayExpr="name" />
          )}
        </Column>
        <Column dataField="state" caption="State" minWidth={180}>
          {messageStates && (
            <Lookup dataSource={messageStates} valueExpr="code" displayExpr="label" />
          )}
        </Column>
        <Column
          dataField="receivedAt"
          caption="Received"
          dataType="datetime"
          format="dd MMM yyyy, HH:mm"
          minWidth={160}
        />
        <Column dataField="account" caption="Account" minWidth={130} />
        <Column dataField="currency" caption="CCY" width={80} />
        <Column
          dataField="amount"
          caption="Amount"
          dataType="number"
          alignment="right"
          format={{ type: 'fixedPoint', precision: 2 }}
          minWidth={120}
        />
        <Column
          dataField="currentAssigneeId"
          caption="Assignee"
          dataType="number"
          width={100}
          allowSorting={false}
        >
          {assigneeUsers && (
            <Lookup dataSource={assigneeUsers} valueExpr="id" displayExpr="displayLabel" />
          )}
        </Column>
      </DataGrid>
    </div>
  );
}
