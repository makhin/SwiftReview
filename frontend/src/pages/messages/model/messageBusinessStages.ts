import type { DashboardSummaryDto, MessageState } from '../../../shared/api/generated/contracts.generated';

export type BusinessStage = 'viewOnly' | 'unassigned' | 'pendingFirstConfirmation' | 'pendingSecondConfirmation' | 'completed';

export function businessStageLabel(state: MessageState, fallback: string): string {
  switch (state) {
    case 'ViewOnly': return 'View only';
    case 'New': return 'Unassigned';
    case 'Assigned':
    case 'FirstReviewInProgress': return 'Pending 1st Confirmation';
    case 'WaitingForSecondReview':
    case 'SecondReviewInProgress': return 'Pending 2nd Confirmation';
    case 'Completed': return 'Completed';
    default: return fallback;
  }
}

export function businessStageFilter(stage: BusinessStage | null) {
  switch (stage) {
    case 'viewOnly': return ['state', '=', 'ViewOnly'];
    case 'unassigned': return ['state', '=', 'New'];
    case 'pendingFirstConfirmation': return [['state', '=', 'Assigned'], 'or', ['state', '=', 'FirstReviewInProgress']];
    case 'pendingSecondConfirmation': return [['state', '=', 'WaitingForSecondReview'], 'or', ['state', '=', 'SecondReviewInProgress']];
    case 'completed': return ['state', '=', 'Completed'];
    default: return null;
  }
}

export function businessStageCards(summary: DashboardSummaryDto | undefined) {
  return [
    { value: null, label: 'Total Messages', count: summary?.totalMessages ?? null },
    { value: 'viewOnly', label: 'View only', count: summary?.viewOnly ?? null },
    { value: 'unassigned', label: 'Unassigned', count: summary?.unassigned ?? null },
    { value: 'pendingFirstConfirmation', label: 'Pending 1st Confirmation', count: summary?.pendingFirstConfirmation ?? null,
      surface: 'var(--color-review-level-1-surface)', accent: 'var(--color-review-level-1)' },
    { value: 'pendingSecondConfirmation', label: 'Pending 2nd Confirmation', count: summary?.pendingSecondConfirmation ?? null,
      surface: 'var(--color-review-level-2-surface)', accent: 'var(--color-review-level-2)' },
    { value: 'completed', label: 'Completed', count: summary?.completed ?? null },
  ] satisfies Array<{ value: BusinessStage | null; label: string; count: number | null; surface?: string; accent?: string }>;
}
