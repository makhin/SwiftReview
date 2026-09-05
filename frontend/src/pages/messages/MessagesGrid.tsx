import DataGrid, {
  Button as GridButton,
  Column,
  FilterRow,
  HeaderFilter,
  Lookup,
  Pager,
  Paging,
} from 'devextreme-react/data-grid';
import type { DataGridRef } from 'devextreme-react/data-grid';
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
import RawMessagePopup from './RawMessagePopup';
import ReviewDecisionPopup from './ReviewDecisionPopup';
import { canReviewMessage, type ReviewDecision } from './reviewDecision';

type MessagesGridProps = {
  dataSource: CustomStore<MessageRow, MessageRow['id']>;
  enableReviewActions?: boolean;
};

type SelectedReviewAction = {
  decision: ReviewDecision;
  message: MessageRow;
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

export default function MessagesGrid({
  dataSource,
  enableReviewActions = false,
}: MessagesGridProps) {
  const { data: currentUser } = useQuery(currentUserQueryOptions());
  const { data: users } = useQuery(usersQueryOptions());
  const { data: branches } = useQuery(branchesQueryOptions());
  const { data: departments } = useQuery(departmentsQueryOptions());
  const { data: messageStates } = useQuery(messageStatesQueryOptions());
  const [selectedAuditMessage, setSelectedAuditMessage] = useState<MessageRow | null>(null);
  const [selectedRawMessage, setSelectedRawMessage] = useState<MessageRow | null>(null);
  const [selectedReviewAction, setSelectedReviewAction] =
    useState<SelectedReviewAction | null>(null);
  const auditTriggerRef = useRef<HTMLElement | null>(null);
  const rawTriggerRef = useRef<HTMLElement | null>(null);
  const reviewTriggerRef = useRef<HTMLElement | null>(null);
  const dataGridRef = useRef<DataGridRef<MessageRow, MessageRow['id']>>(null);
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
    setSelectedAuditMessage(null);
    requestAnimationFrame(() => auditTriggerRef.current?.focus());
  }

  function closeRawMessage() {
    setSelectedRawMessage(null);
    requestAnimationFrame(() => rawTriggerRef.current?.focus());
  }

  function closeReviewAction() {
    setSelectedReviewAction(null);
    requestAnimationFrame(() => reviewTriggerRef.current?.focus());
  }

  function openReviewAction(message: MessageRow, decision: ReviewDecision) {
    reviewTriggerRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setSelectedReviewAction({ decision, message });
  }

  function canShowReviewAction(message: MessageRow | undefined, decision: ReviewDecision) {
    if (!currentUser || !message) {
      return false;
    }

    return canReviewMessage(message.state, decision, currentUser.permissions);
  }

  useEffect(() => {
    if (!selectedAuditMessage) {
      return undefined;
    }

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeAudit();
      }
    };

    document.addEventListener('keydown', closeOnEscape);
    return () => document.removeEventListener('keydown', closeOnEscape);
  }, [selectedAuditMessage]);

  useEffect(() => {
    if (!selectedAuditMessage) {
      return undefined;
    }

    const appRoot = document.getElementById('root');
    appRoot?.setAttribute('inert', '');
    return () => appRoot?.removeAttribute('inert');
  }, [selectedAuditMessage]);

  return (
    <>
      <div className="app-table-shell">
        <DataGrid
          ref={dataGridRef}
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
          <Column
            type="buttons"
            caption="Actions"
            width={enableReviewActions ? (showAudit ? 290 : 250) : showAudit ? 130 : 90}
            allowFiltering={false}
            allowSorting={false}
          >
            {enableReviewActions && (
              <GridButton
                text="Approve"
                hint="Approve message"
                visible={(event) =>
                  canShowReviewAction(event.row?.data as MessageRow | undefined, 'approve')
                }
                onClick={(event) => {
                  const message = event.row?.data as MessageRow | undefined;
                  if (message) {
                    openReviewAction(message, 'approve');
                  }
                }}
              />
            )}
            {enableReviewActions && (
              <GridButton
                text="Reject"
                hint="Reject message"
                visible={(event) =>
                  canShowReviewAction(event.row?.data as MessageRow | undefined, 'reject')
                }
                onClick={(event) => {
                  const message = event.row?.data as MessageRow | undefined;
                  if (message) {
                    openReviewAction(message, 'reject');
                  }
                }}
              />
            )}
            <GridButton
              text="Raw"
              hint="View raw message"
              onClick={(event) => {
                const message = event.row?.data as MessageRow | undefined;

                if (message) {
                  rawTriggerRef.current =
                    document.activeElement instanceof HTMLElement
                      ? document.activeElement
                      : null;
                  setSelectedRawMessage(message);
                }
              }}
            />
            {showAudit && (
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
                    setSelectedAuditMessage(message);
                  }
                }}
              />
            )}
          </Column>
        </DataGrid>
      </div>
      {selectedRawMessage && (
        <RawMessagePopup message={selectedRawMessage} onClose={closeRawMessage} />
      )}
      {selectedReviewAction && (
        <ReviewDecisionPopup
          decision={selectedReviewAction.decision}
          message={selectedReviewAction.message}
          onClose={closeReviewAction}
          onChanged={() => void dataGridRef.current?.instance().refresh()}
        />
      )}
      {selectedAuditMessage &&
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
                message={selectedAuditMessage}
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
