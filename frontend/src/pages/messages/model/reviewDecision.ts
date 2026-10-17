import type { MessageRow } from '../api/messagesApi';

export type ReviewDecision = 'approve' | 'reject';

export type ReviewStep = {
  level: number;
  needsStart: boolean;
};

export function getReviewStep(state: MessageRow['state']): ReviewStep | null {
  switch (state) {
    case 'Assigned':
      return { level: 1, needsStart: true };
    case 'FirstReviewInProgress':
      return { level: 1, needsStart: false };
    case 'WaitingForSecondReview':
      return { level: 2, needsStart: true };
    case 'SecondReviewInProgress':
      return { level: 2, needsStart: false };
    case 'WaitingForThirdReview':
      return { level: 3, needsStart: true };
    case 'ThirdReviewInProgress':
      return { level: 3, needsStart: false };
    default:
      return null;
  }
}

export function canReviewMessage(message: Pick<MessageRow, 'state' | 'canReview'>) {
  return getReviewStep(message.state) != null && message.canReview === true;
}
