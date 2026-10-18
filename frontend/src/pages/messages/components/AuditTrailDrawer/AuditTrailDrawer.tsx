import { useInfiniteQuery } from '@tanstack/react-query';
import { Button, type ButtonRef } from '@smbc/ui';

import { useEffect, useRef } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';

import type {
  AuditEventDto,
  AuditEventType,
  MessageStateReferenceDto,
  UserSummaryDto,
} from '../../../../shared/api/generated/contracts.generated';
import PageError from '../../../../shared/components/feedback/PageError';
import PageLoading from '../../../../shared/components/feedback/PageLoading';
import { messageAuditQueryOptions } from '../../api/auditQueries';
import type { MessageRow } from '../../api/messagesApi';

const eventLabels: Record<AuditEventType, string> = {
  MessageRegistered: 'Message registered',
  MessageAssigned: 'Message assigned',
  MessageReassigned: 'Message reassigned',
  MessageUnassigned: 'Message unassigned',
  ReviewStarted: 'Review started',
  ReviewApproved: 'Review approved',
  MessageCompleted: 'Message completed',
  ReviewRejected: 'Review rejected',
  ReviewCancelled: 'Review cancelled',
  MessageWorkflowChanged: 'Workflow changed',
  ConfirmationUndone: 'Confirmation undone',
};

const focusableSelector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

type AuditTrailDrawerProps = {
  message: MessageRow;
  users?: UserSummaryDto[];
  messageStates?: MessageStateReferenceDto[];
  onClose: () => void;
};

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-1 gap-0 md:grid-cols-[120px_minmax(0,1fr)] md:gap-2 [&_dt]:text-fg-muted [&_dd]:min-w-0 [&_dd]:wrap-anywhere">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function AuditEvent({
  event,
  users,
  messageStates,
}: {
  event: AuditEventDto;
  users?: UserSummaryDto[];
  messageStates?: MessageStateReferenceDto[];
}) {
  const stateLabel = (state: string | null) =>
    state === null
      ? '—'
      : (messageStates?.find((item) => item.code === state)?.label ?? state);
  const userLabel = (userId: number | string) =>
    users?.find((user) => String(user.id) === String(userId))?.displayName ??
    `User #${userId}`;
  const { details } = event;

  return (
    <li className="audit-event relative grid grid-cols-[var(--space-4)_minmax(0,1fr)] gap-3 pb-5">
      <div
        className="audit-event__marker z-1 size-4 mt-1 bg-primary border-[length:var(--border-width-emphasis)] border-surface rounded-circle shadow-[0_0_0_1px_var(--color-action-primary)]"
        aria-hidden="true"
      />
      <article>
        <div className="flex flex-col items-start justify-between gap-0 md:flex-row md:items-baseline md:gap-3 [&_time]:shrink-0 [&_time]:text-fg-muted [&_time]:text-sm">
          <strong>{eventLabels[event.eventType]}</strong>
          <time dateTime={event.timestamp}>
            {new Intl.DateTimeFormat(undefined, {
              dateStyle: 'medium',
              timeStyle: 'short',
            }).format(new Date(event.timestamp))}
          </time>
        </div>
        <p className="mt-1 text-fg-muted text-sm">
          {event.actor?.displayName || event.actor?.userName || 'System'}
        </p>
        <dl className="grid gap-1 mt-2">
          {(event.oldState !== null || event.newState !== null) && (
            <Detail
              label="State"
              value={`${stateLabel(event.oldState)} → ${stateLabel(event.newState)}`}
            />
          )}
          {details.workflowDefinitionId != null && (
            <Detail
              label="Workflow"
              value={String(details.workflowDefinitionId)}
            />
          )}
          {details.previousWorkflowDefinitionId != null && (
            <Detail
              label="Previous workflow"
              value={String(details.previousWorkflowDefinitionId)}
            />
          )}
          {details.previousAssigneeId != null && (
            <Detail
              label="Previous assignee"
              value={userLabel(details.previousAssigneeId)}
            />
          )}
          {details.assigneeId != null && (
            <Detail label="Assignee" value={userLabel(details.assigneeId)} />
          )}
          {details.reviewLevel != null && (
            <Detail label="Review level" value={String(details.reviewLevel)} />
          )}
          {details.comment && (
            <Detail label="Comment" value={details.comment} />
          )}
        </dl>
      </article>
    </li>
  );
}

export default function AuditTrailDrawer({
  message,
  users,
  messageStates,
  onClose,
}: AuditTrailDrawerProps) {
  const panelRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<ButtonRef>(null);
  const auditQuery = useInfiniteQuery(messageAuditQueryOptions(message.id));
  const events = auditQuery.data?.pages.flatMap((page) => page.items) ?? [];
  const focusCloseButton = () => closeButtonRef.current?.focus();

  useEffect(() => {
    focusCloseButton();
    // Drawer positions its overlay after the panel mounts; focus once it is visible.
    const focusFrame = requestAnimationFrame(focusCloseButton);

    const keepFocusInPanel = (event: FocusEvent) => {
      if (!panelRef.current?.contains(event.target as Node)) {
        focusCloseButton();
      }
    };

    document.addEventListener('focusin', keepFocusInPanel);
    return () => {
      cancelAnimationFrame(focusFrame);
      document.removeEventListener('focusin', keepFocusInPanel);
    };
  }, []);

  function trapFocus(event: ReactKeyboardEvent<HTMLElement>) {
    if (event.key !== 'Tab' || !panelRef.current) {
      return;
    }

    const focusableElements = Array.from(
      panelRef.current.querySelectorAll<HTMLElement>(focusableSelector),
    );
    const firstElement = focusableElements[0];
    const lastElement = focusableElements.at(-1);

    if (!firstElement || !lastElement) {
      return;
    }

    if (event.shiftKey && document.activeElement === firstElement) {
      event.preventDefault();
      lastElement.focus();
    } else if (!event.shiftKey && document.activeElement === lastElement) {
      event.preventDefault();
      firstElement.focus();
    }
  }

  return (
    <aside
      ref={panelRef}
      className="flex flex-col w-screen md:w-[440px] max-w-screen h-full bg-surface shadow-popup-elevation"
      role="dialog"
      aria-modal="true"
      aria-labelledby="audit-drawer-title"
      onKeyDown={trapFocus}
    >
      <header className="flex items-start justify-between gap-4 px-5 py-4 border-b border-border [&_p]:mt-1 [&_p]:text-fg-muted">
        <div>
          <h2 className="text-xl font-semibold" id="audit-drawer-title">
            Audit trail
          </h2>
          <p>{message.externalId}</p>
        </div>
        <Button
          ariaLabel="Close audit trail"
          ref={closeButtonRef}
          icon="close"
          width={36}
          onClick={onClose}
          variant="tertiary"
          devExtremeProps={{
            className: 'shrink-0 text-fg-muted',
            height: 36,
            elementAttr: { 'aria-label': 'Close audit trail' },
          }}
        />
      </header>

      <div className="flex-1 min-h-0 p-5 overflow-y-auto overscroll-contain">
        {auditQuery.isPending && <PageLoading message="Loading audit trail…" />}

        {auditQuery.isError && events.length === 0 && (
          <PageError
            title="Unable to load audit trail"
            message="Check your connection and try again."
            actionLabel="Retry"
            onAction={() => void auditQuery.refetch()}
          />
        )}

        {!auditQuery.isPending &&
          !auditQuery.isError &&
          events.length === 0 && (
            <p className="text-fg-muted">No audit events found.</p>
          )}

        {events.length > 0 && (
          <ol className="list-none p-0 m-0">
            {events.map((event) => (
              <AuditEvent
                key={String(event.id)}
                event={event}
                users={users}
                messageStates={messageStates}
              />
            ))}
          </ol>
        )}

        {auditQuery.isFetchNextPageError && (
          <div
            className="flex items-center justify-between gap-3 p-3 text-danger bg-danger-faint border-l-[length:var(--border-width-emphasis)] border-danger"
            role="alert"
          >
            <span>Unable to load more events.</span>
            <Button
              onClick={() => void auditQuery.fetchNextPage()}
              variant="secondary"
              children="Retry"
            />
          </div>
        )}

        {auditQuery.hasNextPage && !auditQuery.isFetchNextPageError && (
          <Button
            width="100%"
            disabled={auditQuery.isFetchingNextPage}
            onClick={() => void auditQuery.fetchNextPage()}
            variant="secondary"
            children={auditQuery.isFetchingNextPage ? 'Loading…' : 'Load more'}
            devExtremeProps={{ className: 'audit-drawer-panel__load-more' }}
          />
        )}
      </div>
    </aside>
  );
}
