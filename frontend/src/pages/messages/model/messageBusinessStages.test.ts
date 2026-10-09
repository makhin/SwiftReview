import { describe, expect, it } from 'vitest';

import { businessStageCards, businessStageFilter, businessStageLabel } from './messageBusinessStages';

describe('business message stages', () => {
  it('uses business names for the first two confirmations and keeps other states identifiable', () => {
    expect(businessStageLabel('New', 'New')).toBe('Unassigned');
    expect(businessStageLabel('Assigned', 'Assigned')).toBe('Pending 1st Confirmation');
    expect(businessStageLabel('FirstReviewInProgress', 'First review in progress')).toBe('Pending 1st Confirmation');
    expect(businessStageLabel('WaitingForSecondReview', 'Waiting for second review')).toBe('Pending 2nd Confirmation');
    expect(businessStageLabel('SecondReviewInProgress', 'Second review in progress')).toBe('Pending 2nd Confirmation');
    expect(businessStageLabel('Completed', 'Completed')).toBe('Completed');
    expect(businessStageLabel('Rejected', 'Rejected')).toBe('Rejected');
    expect(businessStageLabel('WaitingForThirdReview', 'Waiting for third review')).toBe('Waiting for third review');
  });

  it('filters both technical states in each pending group', () => {
    expect(businessStageFilter('pendingFirstConfirmation')).toEqual([['state', '=', 'Assigned'], 'or', ['state', '=', 'FirstReviewInProgress']]);
    expect(businessStageFilter('pendingSecondConfirmation')).toEqual([['state', '=', 'WaitingForSecondReview'], 'or', ['state', '=', 'SecondReviewInProgress']]);
    expect(businessStageFilter(null)).toBeNull();
  });

  it('uses the five server counts without deriving total from the displayed groups', () => {
    expect(businessStageCards({ totalMessages: 10, unassigned: 1, pendingFirstConfirmation: 2, pendingSecondConfirmation: 3, completed: 2 })
      .map(({ count }) => count)).toEqual([10, 1, 2, 3, 2]);
  });
});
