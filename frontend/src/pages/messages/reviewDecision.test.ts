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
    const message = { state: 'Assigned' as const, activeReviewerId: null };
    expect(canReviewMessage(message, 'approve', 1, ['review.level1'])).toBe(true);
    expect(canReviewMessage(message, 'approve', 1, ['review.level2'])).toBe(false);
  });

  it('allows only the owner to approve an active review', () => {
    const message = { state: 'SecondReviewInProgress' as const, activeReviewerId: 7 };
    expect(canReviewMessage(message, 'approve', 7, ['review.level2'])).toBe(true);
    expect(canReviewMessage(message, 'approve', 8, ['review.level2'])).toBe(false);
  });

  it('requires start permission only when rejection must start the review', () => {
    expect(
      canReviewMessage({ state: 'WaitingForSecondReview', activeReviewerId: null }, 'reject', 1, [
        'review.level2',
        'review.reject',
      ]),
    ).toBe(true);
    expect(canReviewMessage(
      { state: 'WaitingForSecondReview', activeReviewerId: null },
      'reject',
      1,
      ['review.reject'],
    )).toBe(false);
    expect(canReviewMessage(
      { state: 'SecondReviewInProgress', activeReviewerId: 7 },
      'reject',
      8,
      ['review.reject'],
    )).toBe(true);
  });
});
