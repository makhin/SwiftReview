import { useInfiniteQuery } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';

import type {
  AuditEventDto,
  AuditEventType,
  MessageStateReferenceDto,
  UserSummaryDto,
} from '../../shared/api/generated/contracts.generated';
import PageError from '../../shared/components/feedback/PageError';
import PageLoading from '../../shared/components/feedback/PageLoading';
import { messageAuditQueryOptions } from './auditQueries';
import type { MessageRow } from './messagesApi';

const eventLabels: Record<AuditEventType, string> = {
  MessageRegistered: 'Message registered',
  MessageAssigned: 'Message assigned',
  MessageReassigned: 'Message reassigned',
  ReviewStarted: 'Review started',
  ReviewApproved: 'Review approved',
  MessageCompleted: 'Message completed',
  ReviewRejected: 'Review rejected',
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
    <div className="audit-event__detail">
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
      : messageStates?.find((item) => item.code === state)?.label ?? state;
  const userLabel = (userId: number | string) =>
    users?.find((user) => String(user.id) === String(userId))?.displayName ??
    `User #${userId}`;
  const { details } = event;

  return (
    <li className="audit-event">
      <div className="audit-event__marker" aria-hidden="true" />
      <article>
        <div className="audit-event__heading">
          <strong>{eventLabels[event.eventType]}</strong>
          <time dateTime={event.timestamp}>
            {new Intl.DateTimeFormat(undefined, {
              dateStyle: 'medium',
              timeStyle: 'short',
            }).format(new Date(event.timestamp))}
          </time>
        </div>
        <p className="audit-event__actor">
          {event.actor?.displayName || event.actor?.userName || 'System'}
        </p>
        <dl className="audit-event__details">
          {(event.oldState !== null || event.newState !== null) && (
            <Detail
              label="State"
              value={`${stateLabel(event.oldState)} → ${stateLabel(event.newState)}`}
            />
          )}
          {details.workflowDefinitionId != null && (
            <Detail label="Workflow" value={String(details.workflowDefinitionId)} />
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
          {details.comment && <Detail label="Comment" value={details.comment} />}
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
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const auditQuery = useInfiniteQuery(messageAuditQueryOptions(message.id));
  const events = auditQuery.data?.pages.flatMap((page) => page.items) ?? [];

  useEffect(() => {
    closeButtonRef.current?.focus();

    const keepFocusInPanel = (event: FocusEvent) => {
      if (!panelRef.current?.contains(event.target as Node)) {
        closeButtonRef.current?.focus();
      }
    };

    document.addEventListener('focusin', keepFocusInPanel);
    return () => document.removeEventListener('focusin', keepFocusInPanel);
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
      className="audit-drawer-panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby="audit-drawer-title"
      onKeyDown={trapFocus}
    >
      <header className="audit-drawer-panel__header">
        <div>
          <h2 id="audit-drawer-title">Audit trail</h2>
          <p>{message.externalId}</p>
        </div>
        <button
          ref={closeButtonRef}
          className="audit-drawer-panel__close"
          type="button"
          aria-label="Close audit trail"
          onClick={onClose}
        >
          ×
        </button>
      </header>

      <div className="audit-drawer-panel__body">
        {auditQuery.isPending && <PageLoading message="Loading audit trail…" />}

        {auditQuery.isError && events.length === 0 && (
          <PageError
            title="Unable to load audit trail"
            message="Check your connection and try again."
            actionLabel="Retry"
            onAction={() => void auditQuery.refetch()}
          />
        )}

        {!auditQuery.isPending && !auditQuery.isError && events.length === 0 && (
          <p className="audit-drawer-panel__empty">No audit events found.</p>
        )}

        {events.length > 0 && (
          <ol className="audit-trail">
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
          <div className="audit-drawer-panel__load-error" role="alert">
            <span>Unable to load more events.</span>
            <button type="button" onClick={() => void auditQuery.fetchNextPage()}>
              Retry
            </button>
          </div>
        )}

        {auditQuery.hasNextPage && !auditQuery.isFetchNextPageError && (
          <button
            className="audit-drawer-panel__load-more"
            type="button"
            disabled={auditQuery.isFetchingNextPage}
            onClick={() => void auditQuery.fetchNextPage()}
          >
            {auditQuery.isFetchingNextPage ? 'Loading…' : 'Load more'}
          </button>
        )}
      </div>
    </aside>
  );
}
