import type { MessageState } from '../../shared/api/generated/contracts.generated';
import { getReviewStep } from './reviewDecision';

export function messageStateCardColours(state: string) {
  const level = getReviewStep(state as MessageState)?.level;
  return level ? { surface: `var(--color-review-level-${level}-surface)`, accent: `var(--color-review-level-${level})` } : {};
}
