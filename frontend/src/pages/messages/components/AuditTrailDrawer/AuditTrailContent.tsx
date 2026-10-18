import { useInfiniteQuery } from '@tanstack/react-query';
import { Button } from '@smbc/ui';

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

type AuditTrailContentProps = {
  message: MessageRow;
  users?: UserSummaryDto[];
  messageStates?: MessageStateReferenceDto[];
  onUndo?: () => void;
  undoDisabled?: boolean;
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
  onUndo,
  undoDisabled,
}: {
  onUndo?: () => void;
  undoDisabled?: boolean;
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
        {onUndo && (
          <Button variant="secondary" disabled={undoDisabled} onClick={onUndo}>
            Undo approval
          </Button>
        )}
      </article>
    </li>
  );
}

export default function AuditTrailContent({
  message,
  users,
  messageStates,
  onUndo,
  undoDisabled,
}: AuditTrailContentProps) {
  const auditQuery = useInfiniteQuery(messageAuditQueryOptions(message.id));
  const events = auditQuery.data?.pages.flatMap((page) => page.items) ?? [];

  const undoEvent = events.find(
    (event) =>
      event.eventType === 'ReviewApproved' &&
      message.undoReviewId != null &&
      String(event.details.reviewId) === String(message.undoReviewId),
  );

  return (
    <div aria-label="Audit trail">
      {onUndo && !undoEvent && (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
          <strong>Latest approval</strong>
          <Button variant="secondary" disabled={undoDisabled} onClick={onUndo}>
            Undo approval
          </Button>
        </div>
      )}
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
              onUndo={event === undoEvent ? onUndo : undefined}
              undoDisabled={undoDisabled}
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
  );
}
