import { useCallback, useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Tabs } from '@smbc/ui';
import ActionDialog, {
  DialogActions,
} from '../../../../shared/components/ActionDialog';
import PageError from '../../../../shared/components/feedback/PageError';
import { useModalFocus } from '../../../../shared/hooks/useModalFocus';
import { useMediaQuery } from '../../../../shared/hooks/useMediaQuery';
import PageLoading from '../../../../shared/components/feedback/PageLoading';
import type { RefreshData } from '../../../../shared/api/refreshAfterMutation';
import type {
  MessageStateReferenceDto,
  UserSummaryDto,
} from '../../../../shared/api/generated/contracts.generated';
import { messageQueryOptions } from '../../api/messageQueries';
import { messageDataSource } from '../../api/messageDataSource';
import type { MessageRow } from '../../api/messagesApi';
import AuditTrailContent from '../AuditTrailDrawer/AuditTrailContent';
import MessagePdfContent from './MessagePdfContent';
import AssignmentPopup from './AssignmentPopup';
import ChangeWorkflowPopup from './ChangeWorkflowPopup';
import ConfirmMessagePopup from './ConfirmMessagePopup';
import ReviewDecisionPopup from './ReviewDecisionPopup';
import UndoReviewPopup from './UndoReviewPopup';

type Tab = 'message' | 'pdf' | 'history';
type Action = 'assignment' | 'workflow' | 'confirm' | 'review' | 'undo';

// Stable items keep DevExtreme from rebuilding the focused tab on selection.
const messageTabs: { id: Tab; text: string }[] = [
  { id: 'message', text: 'Message' },
  { id: 'pdf', text: 'PDF' },
];
const messageTabsWithHistory: { id: Tab; text: string }[] = [
  ...messageTabs,
  { id: 'history', text: 'History' },
];

export default function MessageViewPopup({
  message: initialMessage,
  onClose,
  onChanged,
  canReview,
  canConfirm,
  allowWorkflow,
  allowUndo,
  users,
  messageStates,
}: {
  message: MessageRow;
  onClose: () => void;
  onChanged: RefreshData;
  canReview: (message: MessageRow) => boolean;
  canConfirm: (message: MessageRow) => boolean;
  allowWorkflow: boolean;
  allowUndo: boolean;
  users?: UserSummaryDto[];
  messageStates?: MessageStateReferenceDto[];
}) {
  const prefersReducedMotion = useMediaQuery(
    '(prefers-reduced-motion: reduce)',
  );
  const [message, setMessage] = useState(initialMessage);
  const [tab, setTab] = useState<Tab>('message');
  const [action, setAction] = useState<Action | null>(null);
  const [reload, setReload] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState(false);
  const details = useQuery(messageQueryOptions(message.id));
  const panelRef = useRef<HTMLElement>(null);
  useModalFocus(panelRef, action === null, onClose, ['#root']);
  const attachContent = useCallback((content: HTMLDivElement | null) => {
    panelRef.current =
      content?.closest<HTMLElement>('[role="dialog"]') ?? content;
  }, []);

  // Load the full row through CustomStore, including permissions and workflow,
  // even if a mutation moved this message outside the grid's current filter/page.
  useEffect(() => {
    if (reload === 0) return;
    let active = true;
    void Promise.resolve(messageDataSource.byKey(initialMessage.id)).then(
      (updated) => {
        if (!active) return;
        setMessage(updated);
        setRefreshing(false);
      },
      () => {
        if (!active) return;
        setRefreshError(true);
        setRefreshing(false);
      },
    );
    return () => {
      active = false;
    };
  }, [initialMessage.id, reload]);

  function returnToMessage() {
    setAction(null);
    setRefreshing(true);
    setRefreshError(false);
    setReload((value) => value + 1);
  }

  const actionProps = { message, onClose: returnToMessage, onChanged };
  if (action === 'assignment') return <AssignmentPopup {...actionProps} />;
  if (action === 'workflow') return <ChangeWorkflowPopup {...actionProps} />;
  if (action === 'undo') return <UndoReviewPopup {...actionProps} />;
  if (action === 'confirm') return <ConfirmMessagePopup {...actionProps} />;
  if (action === 'review')
    return (
      <ReviewDecisionPopup
        {...actionProps}
        canApprove={canReview(message)}
        canReject={canReview(message) && message.rejectAllowed === true}
      />
    );

  const ready = !refreshing && !refreshError;
  const primary = canConfirm(message)
    ? 'confirm'
    : canReview(message)
      ? 'review'
      : message.canAssign
        ? 'assignment'
        : null;
  const assignmentLabel =
    message.currentAssigneeId == null ? 'Assign' : 'Reassign';
  const canUndo = allowUndo && message.undoReviewId != null;
  const hasHistory = message.canViewAudit || canUndo;
  const tabs = hasHistory ? messageTabsWithHistory : messageTabs;
  const activeTab = tab === 'history' && !hasHistory ? 'message' : tab;

  return (
    <ActionDialog
      title="View message"
      contentRef={attachContent}
      onClose={onClose}
      devExtremeProps={{
        wrapperAttr: { class: 'message-view-popup raw-message-popup' },
        ...(prefersReducedMotion
          ? { animation: { show: { duration: 0 }, hide: { duration: 0 } } }
          : {}),
        maxWidth: 1000,
        height: '85vh',
      }}
    >
      <div className="min-w-0">
        <h2 className="text-lg font-semibold wrap-anywhere">
          {message.externalId}
        </h2>
        <dl className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-3 text-sm">
          <div>
            <dt className="text-fg-muted">Status</dt>
            <dd>
              {message.stateName ??
                messageStates?.find((state) => state.code === message.state)
                  ?.label ??
                message.state}
            </dd>
          </div>
          <div>
            <dt className="text-fg-muted">Assignee</dt>
            <dd className="flex flex-wrap items-center gap-2">
              <span>
                {message.currentAssigneeName ??
                  users?.find(
                    (user) =>
                      String(user.id) === String(message.currentAssigneeId),
                  )?.displayName ??
                  (message.currentAssigneeId == null
                    ? 'Unassigned'
                    : `User #${message.currentAssigneeId}`)}
              </span>
              {message.canAssign && primary !== 'assignment' && (
                <Button
                  variant="secondary"
                  disabled={!ready}
                  onClick={() => setAction('assignment')}
                >
                  {assignmentLabel}
                </Button>
              )}
            </dd>
          </div>
          <div>
            <dt className="text-fg-muted">Workflow</dt>
            <dd className="flex flex-wrap items-center gap-2">
              <span>
                {message.workflowDefinitionName ?? message.workflowDefinitionId}
              </span>
              {allowWorkflow && message.canChangeWorkflow && (
                <Button
                  variant="secondary"
                  disabled={!ready}
                  onClick={() => setAction('workflow')}
                >
                  Change workflow
                </Button>
              )}
            </dd>
          </div>
        </dl>
      </div>
      {refreshing && <PageLoading message="Refreshing message…" />}
      {refreshError && (
        <PageError
          title="Unable to refresh message"
          message="Reload the message before taking another action."
          actionLabel="Retry"
          onAction={returnToMessage}
        />
      )}
      <Tabs
        items={tabs}
        selectedIndex={tabs.findIndex((item) => item.id === activeTab)}
        onChange={(index) => {
          if (tabs[index]) setTab(tabs[index].id);
        }}
        devExtremeProps={{
          keyExpr: 'id',
          elementAttr: { 'aria-label': 'Message sections' },
        }}
      />
      <section
        className="message-view-popup__content"
        aria-label={`${tabs.find((item) => item.id === activeTab)?.text} content`}
      >
        {activeTab === 'message' && (
          <>
            <p className="mb-3 text-sm text-fg-muted">
              {message.messageType} · {message.direction ?? 'Unknown direction'}{' '}
              · {message.sender} → {message.receiver}
            </p>
            {details.isPending && (
              <PageLoading message="Loading raw message…" />
            )}
            {details.isError && (
              <PageError
                title="Unable to load raw message"
                message="Check your connection and try again."
                actionLabel="Retry"
                onAction={() => void details.refetch()}
              />
            )}
            {details.isSuccess &&
              (details.data.body ? (
                <pre
                  className="raw-message-popup__body"
                  aria-label="Raw message content"
                >
                  {details.data.body}
                </pre>
              ) : (
                <p>No raw message content available.</p>
              ))}
          </>
        )}
        {activeTab === 'pdf' && <MessagePdfContent message={message} />}
        {activeTab === 'history' &&
          (message.canViewAudit ? (
            <AuditTrailContent
              message={message}
              users={users}
              messageStates={messageStates}
              onUndo={canUndo ? () => setAction('undo') : undefined}
              undoDisabled={!ready}
            />
          ) : (
            <div>
              <p>You do not have permission to view the audit history.</p>
              <p>The latest approval can be undone.</p>
              <Button
                variant="secondary"
                disabled={!ready}
                onClick={() => setAction('undo')}
              >
                Undo approval
              </Button>
            </div>
          ))}
      </section>
      <DialogActions>
        {primary && (
          <Button
            variant="primary"
            disabled={!ready}
            onClick={() => setAction(primary)}
          >
            {primary === 'confirm'
              ? 'Confirm'
              : primary === 'review'
                ? 'Start review'
                : assignmentLabel}
          </Button>
        )}
        {primary === 'confirm' && canReview(message) && (
          <Button
            variant="secondary"
            disabled={!ready}
            onClick={() => setAction('review')}
          >
            Start review
          </Button>
        )}
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      </DialogActions>
    </ActionDialog>
  );
}
