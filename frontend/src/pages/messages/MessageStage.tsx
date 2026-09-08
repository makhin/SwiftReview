import Stepper from 'devextreme-react/stepper';

import type { MessageState } from '../../shared/api/generated/contracts.generated';
import './message-stage.css';

const ordinals = ['1st', '2nd', '3rd'];

function selectedAction(state: MessageState, hasAssignee: boolean, requiredLevels: number[]) {
  let level = 1;
  let review = hasAssignee;
  switch (state) {
    case 'FirstReviewInProgress':
      review = true;
      break;
    case 'WaitingForSecondReview':
      level = 2;
      break;
    case 'SecondReviewInProgress':
      level = 2;
      review = true;
      break;
    case 'WaitingForThirdReview':
      level = 3;
      break;
    case 'ThirdReviewInProgress':
      level = 3;
      review = true;
      break;
    case 'Completed':
    case 'Rejected':
      return requiredLevels.length * 2 - 1;
  }
  const position = requiredLevels.indexOf(level);
  return position < 0 ? -1 : position * 2 + Number(review);
}

type MessageStageProps = {
  state: MessageState;
  label: string;
  requiredLevels: number[];
  hasAssignee: boolean;
};

export default function MessageStage({ state, label, requiredLevels, hasAssignee }: MessageStageProps) {
  const reviewSteps = requiredLevels.flatMap((level) => [
    { hint: `${ordinals[level - 1]} assignment` },
    { hint: `${ordinals[level - 1]} review` },
  ]);
  const actionCount = reviewSteps.length;
  const finalState = state === 'Completed'
    ? ' message-stage--completed'
    : state === 'Rejected'
      ? ' message-stage--rejected'
      : '';

  return (
    <div className={`message-stage${finalState}`} aria-label={`Stage: ${label}`}>
      {actionCount > 0 && <Stepper
        className="message-stage__stepper"
        items={reviewSteps}
        selectedIndex={selectedAction(state, hasAssignee, requiredLevels)}
        width={actionCount * 20 - 4}
        orientation="horizontal"
        activeStateEnabled={false}
        focusStateEnabled={false}
        hoverStateEnabled={false}
        elementAttr={{ 'aria-hidden': 'true' }}
        onSelectionChanging={(event) => {
          event.cancel = true;
        }}
      />}
      <span className="message-stage__label">{label}</span>
    </div>
  );
}
