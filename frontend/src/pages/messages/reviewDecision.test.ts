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
    expect(canReviewMessage('Assigned', 'approve', ['review.level1'])).toBe(true);
    expect(canReviewMessage('Assigned', 'approve', ['review.level2'])).toBe(false);
  });

  it('requires start permission only when rejection must start the review', () => {
    expect(
      canReviewMessage('WaitingForSecondReview', 'reject', [
        'review.level2',
        'review.reject',
      ]),
    ).toBe(true);
    expect(canReviewMessage('WaitingForSecondReview', 'reject', ['review.reject'])).toBe(
      false,
    );
    expect(canReviewMessage('SecondReviewInProgress', 'reject', ['review.reject'])).toBe(
      true,
    );
  });
});
