import DataGrid, {
  Button as GridButton,
  Column,
  FilterRow,
  HeaderFilter,
  Lookup,
  Pager,
  Paging,
} from 'devextreme-react/data-grid';
import Drawer from 'devextreme-react/drawer';
import type CustomStore from 'devextreme/data/custom_store';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

import { currentUserQueryOptions } from '../current-user/currentUserQueries';
import { canViewAudit } from '../../shared/auth/permissions';
import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  usersQueryOptions,
} from '../../shared/api/referenceDataQueries';
import AuditTrailDrawer from './AuditTrailDrawer';
import type { MessageRow } from './messagesApi';

type MessagesGridProps = {
  dataSource: CustomStore<MessageRow, MessageRow['id']>;
};

const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

function usePrefersReducedMotion() {
  const [matches, setMatches] = useState(
    () => typeof window.matchMedia === 'function' && window.matchMedia(REDUCED_MOTION_QUERY).matches,
  );

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return undefined;
    }

    const mediaQuery = window.matchMedia(REDUCED_MOTION_QUERY);
    const updateMatch = () => setMatches(mediaQuery.matches);

    updateMatch();
    mediaQuery.addEventListener('change', updateMatch);
    return () => mediaQuery.removeEventListener('change', updateMatch);
  }, []);

  return matches;
}

export default function MessagesGrid({ dataSource }: MessagesGridProps) {
  const { data: currentUser } = useQuery(currentUserQueryOptions());
  const { data: users } = useQuery(usersQueryOptions());
  const { data: branches } = useQuery(branchesQueryOptions());
  const { data: departments } = useQuery(departmentsQueryOptions());
  const { data: messageStates } = useQuery(messageStatesQueryOptions());
  const [selectedMessage, setSelectedMessage] = useState<MessageRow | null>(null);
  const auditTriggerRef = useRef<HTMLElement | null>(null);
  const prefersReducedMotion = usePrefersReducedMotion();
  const showAudit = currentUser ? canViewAudit(currentUser.permissions) : false;
  const assigneeUsers = users?.map((user) => {
    const names = user.departmentIds.map(
      (id) => departments?.find((department) => department.id === id)?.name ?? String(id),
    );

    return {
      ...user,
      displayLabel: `${user.displayName} — ${names.length > 0 ? names.join(', ') : 'No departments'}`,
    };
  });

  function closeAudit() {
    setSelectedMessage(null);
    requestAnimationFrame(() => auditTriggerRef.current?.focus());
  }

  useEffect(() => {
    if (!selectedMessage) {
      return undefined;
    }

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeAudit();
      }
    };

    document.addEventListener('keydown', closeOnEscape);
    return () => document.removeEventListener('keydown', closeOnEscape);
  }, [selectedMessage]);

  useEffect(() => {
    if (!selectedMessage) {
      return undefined;
    }

    const appRoot = document.getElementById('root');
    appRoot?.setAttribute('inert', '');
    return () => appRoot?.removeAttribute('inert');
  }, [selectedMessage]);

  return (
    <>
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
          {showAudit && (
            <Column
              type="buttons"
              caption="Actions"
              width={90}
              allowFiltering={false}
              allowSorting={false}
            >
              <GridButton
                text="Audit"
                hint="View audit trail"
                onClick={(event) => {
                  const message = event.row?.data as MessageRow | undefined;

                  if (message) {
                    auditTriggerRef.current =
                      document.activeElement instanceof HTMLElement
                        ? document.activeElement
                        : null;
                    setSelectedMessage(message);
                  }
                }}
              />
            </Column>
          )}
        </DataGrid>
      </div>
      {selectedMessage &&
        createPortal(
          <Drawer
            className="audit-drawer"
            opened
            openedStateMode="overlap"
            revealMode="expand"
            position="right"
            minSize={0}
            maxSize={440}
            animationEnabled={!prefersReducedMotion}
            shading
            closeOnOutsideClick
            onOpenedChange={(opened) => {
              if (!opened) {
                closeAudit();
              }
            }}
            render={() => (
              <AuditTrailDrawer
                message={selectedMessage}
                users={users}
                messageStates={messageStates}
                onClose={closeAudit}
              />
            )}
          >
            <div className="audit-drawer__view" aria-hidden="true" />
          </Drawer>,
          document.body,
        )}
    </>
  );
}
