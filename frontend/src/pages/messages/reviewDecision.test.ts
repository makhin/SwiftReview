import { describe, expect, it } from 'vitest';

import { canReviewMessage, getReviewStep } from './reviewDecision';

describe('review decision availability', () => {
  it('maps waiting and active states to the current review step', () => {
    expect(getReviewStep('Assigned')).toEqual({ level: 1, needsStart: true });
    expect(getReviewStep('SecondReviewInProgress')).toEqual({
      level: 2,
      needsStart: false,
    });
    expect(getReviewStep('WaitingForThirdReview')).toEqual({
      level: 3,
      needsStart: true,
    });
    expect(getReviewStep('Completed')).toBeNull();
  });

  it('requires the level permission to approve', () => {
    const message = { state: 'Assigned' as const, currentAssigneeId: 1, activeReviewerId: null };
    expect(canReviewMessage(message, 1, ['review.level1'])).toBe(true);
    expect(canReviewMessage(message, 1, ['review.level2'])).toBe(false);
  });

  it('allows only the owner to approve an active review', () => {
    const message = { state: 'SecondReviewInProgress' as const, currentAssigneeId: 7, activeReviewerId: 7 };
    expect(canReviewMessage(message, 7, ['review.level2'])).toBe(true);
    expect(canReviewMessage(message, 8, ['review.level2'])).toBe(false);
  });

  it('requires the current level permission for both decisions', () => {
    expect(
      canReviewMessage({ state: 'WaitingForSecondReview', currentAssigneeId: 1, activeReviewerId: null }, 1, [
        'review.level2',
      ]),
    ).toBe(true);
    expect(canReviewMessage(
      { state: 'WaitingForSecondReview', currentAssigneeId: 1, activeReviewerId: null },
      1,
      ['review.level1'],
    )).toBe(false);
    expect(canReviewMessage(
      { state: 'SecondReviewInProgress', currentAssigneeId: 8, activeReviewerId: 7 },
      8,
      ['review.level1'],
    )).toBe(false);
  });
});
