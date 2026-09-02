import DataGrid, {
  Column,
  FilterRow,
  HeaderFilter,
  Pager,
  Paging,
} from 'devextreme-react/data-grid';

import { messageDataSource } from './messageDataSource';

export default function MessagesPage() {
  return (
    <main className="app-content app-page">
      <header className="app-page-header">
        <div className="app-page-header__main">
          <h1 className="app-page-title">Messages</h1>
          <p className="app-page-subtitle">
            Messages available to the current user, loaded from the backend.
          </p>
        </div>
      </header>

      <div className="app-table-shell">
        <DataGrid
          dataSource={messageDataSource}
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
          <Column dataField="branchId" caption="Branch" dataType="number" width={90} />
          <Column
            dataField="departmentId"
            caption="Department"
            dataType="number"
            width={110}
          />
          <Column dataField="state" caption="State" minWidth={180} />
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
          />
        </DataGrid>
      </div>
    </main>
  );
}
