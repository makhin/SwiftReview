import GridRefreshButton from '../../shared/components/GridRefreshButton';
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
import { canAssignMessages, canManageWorkflows, canViewAudit, permissionsForScope } from '../../shared/auth/permissions';
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
import UndoReviewButton from './UndoReviewButton';
import ChangeWorkflowPopup from './ChangeWorkflowPopup';
import { canReviewMessage, getReviewStep } from './reviewDecision';
import './messages-grid.css';

type MessagesGridProps = {
  dataSource: CustomStore<MessageRow, MessageRow['id']>;
  enableReviewActions?: boolean;
  enableUndoActions?: boolean;
  enableWorkflowActions?: boolean;
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
  enableUndoActions = false,
  enableWorkflowActions = false,
}: MessagesGridProps) {
  const { data: currentUser } = useQuery(currentUserQueryOptions());
  const { data: users } = useQuery(usersQueryOptions());
  const { data: branches } = useQuery(branchesQueryOptions());
  const { data: departments } = useQuery(departmentsQueryOptions());
  const { data: messageStates } = useQuery(messageStatesQueryOptions());
  const [selectedAuditMessage, setSelectedAuditMessage] = useState<MessageRow | null>(null);
  const [readOnly, setReadOnly] = useState(false);
  const [selectedReviewMessage, setSelectedReviewMessage] = useState<MessageRow | null>(null);
  const [selectedAssignmentMessage, setSelectedAssignmentMessage] = useState<MessageRow | null>(null);
  const [selectedWorkflowMessage, setSelectedWorkflowMessage] = useState<MessageRow | null>(null);
  const auditTriggerRef = useRef<HTMLElement | null>(null);
  const reviewTriggerRef = useRef<HTMLElement | null>(null);
  const assignmentTriggerRef = useRef<HTMLElement | null>(null);
  const dataGridRef = useRef<DataGridRef<MessageRow, MessageRow['id']>>(null);
  const prefersReducedMotion = usePrefersReducedMotion();
  const showAudit = currentUser ? canViewAudit(currentUser.permissions, currentUser.isGlobalAdministrator) : false;
  const showAssignment = currentUser ? canAssignMessages(currentUser.permissions, currentUser.isGlobalAdministrator) : false;
  const showUndo = enableUndoActions && currentUser?.isGlobalAdministrator === true;
  const showWorkflow = enableWorkflowActions && currentUser != null && canManageWorkflows(currentUser.permissions, currentUser.isGlobalAdministrator);
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
    if (!message || !canAssignMessages(permissionsForScope(currentUser, message.branchId, message.departmentId), currentUser?.isGlobalAdministrator)) return false;
    const assignableState = message.state === 'New' || message.state === 'Assigned' ||
      message.state === 'WaitingForSecondReview' || message.state === 'WaitingForThirdReview';
    return assignableState && (message.currentAssigneeId != null) === assigned;
  }

  function openReviewAction(message: MessageRow, preview = false) {
    setReadOnly(preview);
    reviewTriggerRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setSelectedReviewMessage(message);
  }

  function canShowReviewAction(message: MessageRow | undefined) {
    if (!currentUser || !message) {
      return false;
    }

    return (enableReviewActions || currentUser.isGlobalAdministrator) && canReviewMessage(message, currentUser.userId, permissionsForScope(currentUser, message.branchId, message.departmentId), currentUser.isGlobalAdministrator);
  }

  function ownReviewLevel(message: MessageRow) {
    if (!currentUser || !canShowReviewAction(message)) return null;
    const step = getReviewStep(message.state);
    const owner = step?.needsStart ? message.currentAssigneeId : message.activeReviewerId;
    return String(owner) === String(currentUser.userId) ? step?.level : null;
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
      <div className="app-toolbar">
        <GridRefreshButton refresh={() => dataGridRef.current?.instance().refresh()} />
      </div>
      <p className="message-review-legend">
        Review stage:
        {[1, 2, 3].map((level) => <span key={level} style={{ borderInlineStart: `4px solid var(--color-review-level-${level})` }}>Level {level}</span>)}
      </p>
      <div className="app-table-shell">
        <DataGrid
          ref={dataGridRef}
          dataSource={dataSource}
          width="100%"
          remoteOperations
          onRowPrepared={(event) => {
            if (event.rowType !== 'data' || !event.data) return;
            const level = getReviewStep(event.data.state)?.level;
            event.rowElement.classList.toggle('message-assigned-to-you', currentUser != null && event.data.currentAssigneeId != null && String(event.data.currentAssigneeId) === String(currentUser.userId));
            for (const candidate of [1, 2, 3]) event.rowElement.classList.toggle(`message-review-level-${candidate}`, level === candidate);
          }}
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
                  assignedToYou={currentUser != null && message.currentAssigneeId != null && String(message.currentAssigneeId) === String(currentUser.userId)}
                  readyForReview={ownReviewLevel(message) != null && getReviewStep(message.state)?.needsStart === true}
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
            width={200 + (showAudit ? 40 : 0) + (showAssignment ? 100 : 0) + (showUndo ? 85 : 0) + (showWorkflow ? 155 : 0)}
            allowFiltering={false}
            allowSorting={false}
            cellRender={(cell) => {
              const message = cell.data as MessageRow;
              return (
                <div className="message-actions">
                  {showWorkflow && canManageWorkflows(permissionsForScope(currentUser, message.branchId, message.departmentId), currentUser?.isGlobalAdministrator) &&
                    <span
                      title={!message.canChangeWorkflow ? 'Workflow cannot be changed while a review is active, approved or rejected. All review attempts must be cancelled or undone.' : undefined}
                      aria-label={!message.canChangeWorkflow ? 'Workflow cannot be changed while a review is active, approved or rejected. All review attempts must be cancelled or undone.' : undefined}
                      tabIndex={!message.canChangeWorkflow ? 0 : undefined}
                    >
                      <Button text="Change workflow" stylingMode="outlined" disabled={!message.canChangeWorkflow}
                        onClick={() => { if (message.canChangeWorkflow) setSelectedWorkflowMessage(message); }} />
                    </span>}
                  {showUndo && <UndoReviewButton key={String(message.undoReviewId ?? 'none')} message={message}
                    onChanged={() => void dataGridRef.current?.instance().refresh()} />}
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
                    text="View"
                    icon="doc"
                    hint="View message"
                    stylingMode="outlined"
                    onClick={() => openReviewAction(message, true)}
                  />
                  {canShowReviewAction(message) && <Button text="Review" hint="Review message" stylingMode="outlined" onClick={() => openReviewAction(message)} />}
                  {canViewAudit(permissionsForScope(currentUser, message.branchId, message.departmentId), currentUser?.isGlobalAdministrator) && (
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
          key={String(selectedReviewMessage.id)}
          message={selectedReviewMessage}
          canApprove={!readOnly && canShowReviewAction(selectedReviewMessage)}
          canReject={!readOnly && canShowReviewAction(selectedReviewMessage)}
          onClose={closeReviewAction}
          onChanged={() => void dataGridRef.current?.instance().refresh()}
        />
      )}
      {selectedWorkflowMessage && <ChangeWorkflowPopup key={String(selectedWorkflowMessage.id)} message={selectedWorkflowMessage}
        onClose={() => setSelectedWorkflowMessage(null)} onChanged={() => void dataGridRef.current?.instance().refresh()} />}
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
