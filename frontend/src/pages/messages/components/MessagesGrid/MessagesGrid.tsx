import MessageRowActions from './MessageRowActions';
import MessageDialogs, { type MessageDialog } from './MessageDialogs';
import { useMediaQuery } from '../../../../shared/hooks/useMediaQuery';
import { TableShell, MultiSelect, DataGrid, Button } from '@smbc/ui';

import { useMemo } from 'react';
import { createMessageDataSource } from '../../api/messageDataSource';
import GridRefreshButton from '../../../../shared/components/GridRefreshButton';

import {
  DataGridColumn as Column,
  DataGridFilterRow as FilterRow,
  DataGridHeaderFilter as HeaderFilter,
  DataGridLookup as Lookup,
  DataGridPager as Pager,
  DataGridPaging as Paging,
  type DataGridRef,
  type CustomStore,
} from '@smbc/ui/data-grid';

import { useQuery } from '@tanstack/react-query';
import { useRef, useState } from 'react';

import { currentUserQueryOptions } from '../../../../shared/api/currentUserQueries';
import { canManageWorkflows } from '../../../../shared/auth/permissions';
import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  usersQueryOptions,
} from '../../../../shared/api/referenceDataQueries';
import MessageStage from '../MessageStage/MessageStage';
import { type MessageRow } from '../../api/messagesApi';
import { messageSummaryQueryOptions } from '../../api/messageQueries';
import GridStateCards from '../../../../shared/components/GridStateCards';
import {
  businessStageCards,
  businessStageFilter,
  businessStageLabel,
  type BusinessStage,
} from '../../model/messageBusinessStages';
import { canReviewMessage, getReviewStep } from '../../model/reviewDecision';

type MessagesGridProps = {
  dataSource: CustomStore<MessageRow, MessageRow['id']>;
  enableReviewActions?: boolean;
  enableUndoActions?: boolean;
  enableWorkflowActions?: boolean;
};

type DialogTrigger = { id: string; fallback: HTMLElement | null };
function rememberTrigger(id: string): DialogTrigger {
  return {
    id,
    fallback:
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null,
  };
}
function restoreTrigger(trigger: DialogTrigger | null) {
  requestAnimationFrame(() =>
    (document.getElementById(trigger?.id ?? '') ?? trigger?.fallback)?.focus(),
  );
}

export default function MessagesGrid({
  dataSource,
  enableReviewActions = false,
  enableUndoActions = false,
  enableWorkflowActions = false,
}: MessagesGridProps) {
  const [departmentIds, setDepartmentIds] = useState<number[]>([]);
  const [dialog, setDialog] = useState<MessageDialog | null>(null);
  const filteredSource = useMemo(
    () =>
      departmentIds.length
        ? createMessageDataSource(undefined, departmentIds)
        : dataSource,
    [departmentIds, dataSource],
  );
  const { data: currentUser } = useQuery(currentUserQueryOptions());
  const [selectedStage, setSelectedStage] = useState<BusinessStage | null>(
    null,
  );
  const summaryQuery = useQuery(messageSummaryQueryOptions(currentUser));
  const { data: users } = useQuery(usersQueryOptions());
  const { data: branches } = useQuery(branchesQueryOptions());
  const { data: departments } = useQuery(departmentsQueryOptions());
  const { data: messageStates } = useQuery(messageStatesQueryOptions());
  const stateCards = businessStageCards(
    summaryQuery.isError ? undefined : summaryQuery.data,
  );
  const dialogTriggerRef = useRef<DialogTrigger | null>(null);
  const dataGridRef = useRef<DataGridRef<MessageRow, MessageRow['id']>>(null);
  const prefersReducedMotion = useMediaQuery(
    '(prefers-reduced-motion: reduce)',
  );
  const showUndo =
    enableUndoActions &&
    currentUser != null &&
    (currentUser.isGlobalAdministrator ||
      currentUser.permissions.includes('review.undo'));
  const showWorkflow =
    enableWorkflowActions &&
    currentUser != null &&
    canManageWorkflows(
      currentUser.permissions,
      currentUser.isGlobalAdministrator,
    );
  const assigneeUsers = users?.map((user) => {
    const names = user.departmentIds.map(
      (id) =>
        departments?.find((department) => department.id === id)?.name ??
        String(id),
    );

    return {
      ...user,
      displayLabel: `${user.displayName} — ${names.length > 0 ? names.join(', ') : 'No departments'}`,
    };
  });

  function refreshGrid() {
    return dataGridRef.current?.instance().refresh();
  }

  async function refreshMessages() {
    await Promise.all([
      refreshGrid(),
      summaryQuery.refetch({ throwOnError: true }),
    ]);
  }

  function selectState(value: string | null) {
    const stage = value as BusinessStage | null;
    setSelectedStage(stage);
    const grid = dataGridRef.current?.instance();
    if (!grid) return;
    grid.beginUpdate();
    try {
      grid.pageIndex(0);
      grid.filter(businessStageFilter(stage));
    } finally {
      grid.endUpdate();
    }
  }

  function closeDialog() {
    setDialog(null);
    restoreTrigger(dialogTriggerRef.current);
  }
  function openDialog(kind: MessageDialog['kind'], message: MessageRow) {
    const active =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    dialogTriggerRef.current = rememberTrigger(
      active?.closest('[popover]')
        ? `message-more-${message.id}`
        : (active?.id ?? ''),
    );
    setDialog({ kind, message });
  }

  function canShowReviewAction(message: MessageRow | undefined) {
    if (!currentUser || !message) {
      return false;
    }

    return (
      (enableReviewActions || currentUser.isGlobalAdministrator) &&
      canReviewMessage(message)
    );
  }

  return (
    <>
      <section
        className="flex min-w-0 flex-col gap-2"
        aria-label="Message state filters"
      >
        <GridStateCards
          items={stateCards}
          value={selectedStage}
          onChange={selectState}
        />
        <p className="text-fg-muted text-xs">
          Counts include all accessible messages, before grid filters. Rejected
          and 3rd confirmation messages appear in Total Messages.
        </p>
        {summaryQuery.isError && (
          <div className="flex items-center gap-2" role="alert">
            <span>Unable to load counts. Check your connection and retry.</span>
            <Button
              onClick={() => void summaryQuery.refetch({ throwOnError: true })}
              variant="secondary"
              children="Retry counts"
            />
          </div>
        )}
      </section>
      <section
        className="message-grid-results flex min-w-0 flex-col gap-2"
        aria-label="Message results"
      >
        <div className="message-grid-toolbar flex flex-wrap items-center justify-between gap-3 min-h-9 max-sm:items-start">
          <p className="flex items-center gap-2 m-0 text-fg-muted text-xs">
            <span
              className="w-1 h-5 shrink-0 rounded-[var(--space-half)] bg-primary"
              aria-hidden="true"
            />
            Messages that need your attention are marked with a green line.
          </p>
          <div className="flex-[0_1_260px] min-w-[180px] max-w-full">
            <MultiSelect
              options={departments ?? []}
              value={departmentIds}
              optionValue="id"
              optionLabel="name"
              searchable
              placeholder="All departments"
              onChange={(event) => {
                setDepartmentIds(event);
                dataGridRef.current?.instance().pageIndex(0);
              }}
              devExtremeProps={{
                width: '100%',
                showDropDownButton: true,
                inputAttr: { 'aria-label': 'Filter departments' },
              }}
            />
          </div>
          <GridRefreshButton refresh={refreshMessages} />
        </div>
        <TableShell>
          <DataGrid
            ref={dataGridRef}
            dataSource={filteredSource}
            devExtremeProps={{
              width: '100%',
              remoteOperations: true,
              onRowPrepared: (event) => {
                if (event.rowType !== 'data' || !event.data) return;
                const level = getReviewStep(event.data.state)?.level;
                event.rowElement.classList.toggle(
                  'message-assigned-to-you',
                  currentUser != null &&
                    event.data.currentAssigneeId != null &&
                    String(event.data.currentAssigneeId) ===
                      String(currentUser.userId),
                );
                for (const candidate of [1, 2, 3])
                  event.rowElement.classList.toggle(
                    `message-review-level-${candidate}`,
                    level === candidate,
                  );
              },
              showBorders: false,
              rowAlternationEnabled: true,
              hoverStateEnabled: true,
              columnAutoWidth: true,
              elementAttr: { 'aria-label': 'Messages' },
              noDataText: 'No messages found',
            }}
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

            <Column
              dataField="externalId"
              caption="External ID"
              minWidth={140}
            />
            <Column dataField="sender" caption="Sender" minWidth={120} />
            <Column dataField="receiver" caption="Receiver" minWidth={120} />
            <Column
              dataField="messageType"
              caption="Message type"
              width={110}
            />
            <Column dataField="direction" caption="Direction" width={115}>
              <Lookup dataSource={['Incoming', 'Outgoing']} />
            </Column>
            {/* Server-side sorting supports lookup IDs, not their displayed labels. */}
            <Column
              dataField="branchId"
              caption="Branch"
              dataType="number"
              width={90}
              allowSorting={false}
            >
              {branches && (
                <Lookup
                  dataSource={branches}
                  valueExpr="id"
                  displayExpr="name"
                />
              )}
            </Column>
            <Column
              dataField="departmentNames"
              caption="Departments"
              minWidth={150}
              allowFiltering={false}
              allowSorting={false}
              allowGrouping={false}
            />
            <Column
              dataField="state"
              caption="Stage"
              minWidth={180}
              cellRender={(cell) => {
                const message = cell.data as MessageRow;
                const metadata = messageStates?.find(
                  (item) => item.code === message.state,
                );
                return (
                  <MessageStage
                    state={message.state}
                    metadata={metadata}
                    businessLabel={businessStageLabel(
                      message.state,
                      metadata?.label ?? message.state,
                    )}
                    requiredLevels={message.requiredReviewLevels.map(Number)}
                    hasAssignee={message.currentAssigneeId != null}
                  />
                );
              }}
            >
              {messageStates && (
                <Lookup
                  dataSource={messageStates}
                  valueExpr="code"
                  displayExpr="label"
                />
              )}
            </Column>
            <Column
              dataField="receivedAt"
              caption="Received"
              dataType="date"
              format="dd MMM yyyy"
              filterOperations={['=', '<>', '<', '>', '<=', '>=', 'between']}
              minWidth={160}
            />
            <Column
              dataField="currentAssigneeId"
              caption="Assignee"
              dataType="number"
              width={100}
              allowSorting={false}
            >
              {assigneeUsers && (
                <Lookup
                  dataSource={assigneeUsers}
                  valueExpr="id"
                  displayExpr="displayLabel"
                />
              )}
            </Column>
            <Column
              caption="Actions"
              width={210}
              allowFiltering={false}
              allowSorting={false}
              cellRender={({ data: message }: { data: MessageRow }) => (
                <MessageRowActions
                  message={message}
                  open={openDialog}
                  review={canShowReviewAction(message)}
                  confirm={
                    (enableReviewActions ||
                      currentUser?.isGlobalAdministrator === true) &&
                    message.canConfirm === true
                  }
                  undo={
                    showUndo &&
                    (currentUser?.isGlobalAdministrator === true ||
                      message.undoReviewId != null)
                  }
                  workflow={showWorkflow && message.canChangeWorkflow === true}
                  onChanged={refreshGrid}
                />
              )}
            />
          </DataGrid>
        </TableShell>
      </section>
      <MessageDialogs
        dialog={dialog}
        close={closeDialog}
        refresh={refreshGrid}
        canReview={canShowReviewAction}
        users={users}
        messageStates={messageStates}
        prefersReducedMotion={prefersReducedMotion}
      />
    </>
  );
}
