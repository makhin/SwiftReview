import DataGrid, {
  Column,
  FilterRow,
  HeaderFilter,
  Lookup,
  Pager,
  Paging,
} from 'devextreme-react/data-grid';
import type { DataGridRef } from 'devextreme-react/data-grid';
import Button from 'devextreme-react/button';
import Drawer from 'devextreme-react/drawer';
import type CustomStore from 'devextreme/data/custom_store';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import { canAssignMessages, canViewAudit } from '../../shared/auth/permissions';
import {
  branchesQueryOptions,
  departmentsQueryOptions,
  messageStatesQueryOptions,
  usersQueryOptions,
} from '../../shared/api/referenceDataQueries';
import AuditTrailDrawer from './AuditTrailDrawer';
import AssignmentPopup from './AssignmentPopup';
import MessageStage from './MessageStage';
import type { MessageRow } from './messagesApi';
import ReviewDecisionPopup from './ReviewDecisionPopup';
import { canReviewMessage, type ReviewDecision } from './reviewDecision';
import './messages-grid.css';

type MessagesGridProps = {
  dataSource: CustomStore<MessageRow, MessageRow['id']>;
  enableReviewActions?: boolean;
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
  const [selectedReviewMessage, setSelectedReviewMessage] = useState<MessageRow | null>(null);
  const [selectedAssignmentMessage, setSelectedAssignmentMessage] = useState<MessageRow | null>(null);
  const auditTriggerRef = useRef<HTMLElement | null>(null);
  const reviewTriggerRef = useRef<HTMLElement | null>(null);
  const assignmentTriggerRef = useRef<HTMLElement | null>(null);
  const dataGridRef = useRef<DataGridRef<MessageRow, MessageRow['id']>>(null);
  const prefersReducedMotion = usePrefersReducedMotion();
  const showAudit = currentUser ? canViewAudit(currentUser.permissions) : false;
  const showAssignment = currentUser ? canAssignMessages(currentUser.permissions) : false;
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

  function closeReviewAction() {
    setSelectedReviewMessage(null);
    requestAnimationFrame(() => reviewTriggerRef.current?.focus());
  }

  function closeAssignment() {
    setSelectedAssignmentMessage(null);
    requestAnimationFrame(() => assignmentTriggerRef.current?.focus());
  }

  function openAssignment(message: MessageRow) {
    assignmentTriggerRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setSelectedAssignmentMessage(message);
  }

  function openAudit(message: MessageRow) {
    auditTriggerRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setSelectedAuditMessage(message);
  }

  function canShowAssignment(message: MessageRow | undefined, assigned: boolean) {
    if (!showAssignment || !message) return false;
    const assignableState = message.state === 'New' || message.state === 'Assigned' ||
      message.state === 'WaitingForSecondReview' || message.state === 'WaitingForThirdReview';
    return assignableState && (message.currentAssigneeId != null) === assigned;
  }

  function openReviewAction(message: MessageRow) {
    reviewTriggerRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setSelectedReviewMessage(message);
  }

  function canShowReviewAction(message: MessageRow | undefined, decision: ReviewDecision) {
    if (!currentUser || !message) {
      return false;
    }

    return canReviewMessage(message, decision, currentUser.userId, currentUser.permissions);
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
          width="100%"
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
          <Column dataField="messageType" caption="Message type" width={110} />
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
          <Column
            dataField="state"
            caption="Stage"
            minWidth={180}
            cellRender={(cell) => {
              const message = cell.data as MessageRow;
              const label = messageStates?.find((item) => item.code === message.state)?.label ??
                message.state;
              return (
                <MessageStage
                  state={message.state}
                  label={label}
                  requiredLevels={message.requiredReviewLevels ?? []}
                  hasAssignee={message.currentAssigneeId != null}
                />
              );
            }}
          >
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
            caption="Actions"
            width={100 + (showAudit ? 40 : 0) + (showAssignment ? 100 : 0)}
            allowFiltering={false}
            allowSorting={false}
            cellRender={(cell) => {
              const message = cell.data as MessageRow;
              return (
                <div className="message-actions">
                  {canShowAssignment(message, false) && (
                    <Button
                      text="Assign"
                      hint="Assign message"
                      stylingMode="outlined"
                      onClick={() => openAssignment(message)}
                    />
                  )}
                  {canShowAssignment(message, true) && (
                    <Button
                      text="Reassign"
                      hint="Reassign message"
                      stylingMode="outlined"
                      onClick={() => openAssignment(message)}
                    />
                  )}
                  <Button
                    text="Review"
                    icon="doc"
                    hint="Review message"
                    stylingMode="outlined"
                    onClick={() => openReviewAction(message)}
                  />
                  {showAudit && (
                    <Button
                      icon="search"
                      hint="View audit trail"
                      stylingMode="outlined"
                      elementAttr={{
                        class: 'message-actions__icon-button',
                        'aria-label': 'View audit trail',
                      }}
                      onClick={() => openAudit(message)}
                    />
                  )}
                </div>
              );
            }}
          />
        </DataGrid>
      </div>
      {selectedReviewMessage && (
        <ReviewDecisionPopup
          message={selectedReviewMessage}
          canApprove={enableReviewActions && canShowReviewAction(selectedReviewMessage, 'approve')}
          canReject={enableReviewActions && canShowReviewAction(selectedReviewMessage, 'reject')}
          onClose={closeReviewAction}
          onChanged={() => void dataGridRef.current?.instance().refresh()}
        />
      )}
      {selectedAssignmentMessage && (
        <AssignmentPopup
          message={selectedAssignmentMessage}
          onClose={closeAssignment}
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
