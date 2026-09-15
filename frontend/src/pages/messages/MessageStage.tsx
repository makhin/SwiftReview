import Stepper from 'devextreme-react/stepper';

import type { MessageState, MessageStateReferenceDto } from '../../shared/api/generated/contracts.generated';
import './message-stage.css';

const ordinals = ['1st', '2nd', '3rd'];

function selectedAction(metadata: MessageStateReferenceDto, hasAssignee: boolean, requiredLevels: number[]) {
  if (metadata.phase === 'Completed' || metadata.phase === 'Rejected') {
    return requiredLevels.length * 2 - 1;
  }
  const position = metadata.reviewLevel == null ? -1 : requiredLevels.indexOf(Number(metadata.reviewLevel));
  return position < 0 ? -1 : position * 2 + Number(metadata.phase === 'Reviewing' || hasAssignee);
}

type MessageStageProps = {
  state: MessageState;
  metadata?: MessageStateReferenceDto;
  requiredLevels: number[];
  hasAssignee: boolean;
};

export default function MessageStage({ state, metadata, requiredLevels, hasAssignee }: MessageStageProps) {
  const label = metadata?.label ?? state;
  const description = (hasAssignee ? metadata?.assignedDescription : null) ?? metadata?.description ?? label;
  const reviewSteps = requiredLevels.flatMap((level) => [
    { hint: `${ordinals[level - 1]} assignment` },
    { hint: `${ordinals[level - 1]} review` },
  ]);
  const actionCount = reviewSteps.length;
  const finalState = metadata?.phase === 'Completed'
    ? ' message-stage--completed'
    : metadata?.phase === 'Rejected'
      ? ' message-stage--rejected'
      : '';

  return (
    <div
      className={`message-stage${finalState}`}
      aria-label={`Stage: ${label}`}
      aria-description={description}
      title={description}
    >
      <span className="message-stage__label">{label}</span>
      {metadata && actionCount > 0 && <Stepper
        className="message-stage__stepper"
        items={reviewSteps}
        selectedIndex={selectedAction(metadata, hasAssignee, requiredLevels)}
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
    </div>
  );
}
