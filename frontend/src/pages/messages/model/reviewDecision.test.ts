import { describe, expect, it } from 'vitest';
import { canReviewMessage, getReviewStep } from './reviewDecision';

describe('review decision availability', () => {
  it.each(['Assigned', 'SecondReviewInProgress', 'WaitingForThirdReview'] as const)(
    'uses server capabilities for %s, including workflows allowing another authorized actor', (state) => {
      expect(canReviewMessage({ state, canReview: true })).toBe(true);
      expect(canReviewMessage({ state, canReview: false })).toBe(false);
    },
  );
  it.each(['New', 'Completed', 'Rejected', 'ViewOnly'] as const)('does not review %s', (state) => {
    expect(canReviewMessage({ state, canReview: true })).toBe(false);
  });
  it('maps waiting and active states to their review level', () => {
    expect(getReviewStep('Assigned')).toEqual({ level: 1, needsStart: true });
    expect(getReviewStep('SecondReviewInProgress')).toEqual({ level: 2, needsStart: false });
    expect(getReviewStep('WaitingForThirdReview')).toEqual({ level: 3, needsStart: true });
    expect(getReviewStep('ViewOnly')).toBeNull();
  });
});
