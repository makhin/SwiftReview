import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('devextreme-react/stepper', () => ({
  default: ({ selectedIndex, className, items }: {
    selectedIndex: number;
    className: string;
    items: Array<{ hint: string }>;
  }) => (
    <div
      data-testid="stepper"
      data-selected-index={selectedIndex}
      data-step-count={items.length}
      className={className}
    >
      {items.map((item) => <span key={item.hint}>{item.hint}</span>)}
    </div>
  ),
}));

import type { MessageState, MessageStateReferenceDto } from '../../shared/api/generated/contracts.generated';
import MessageStage from './MessageStage';

const definitions: Record<MessageState, MessageStateReferenceDto> = Object.fromEntries([
  ['New', 1, 'Waiting'],
  ['Assigned', 1, 'Assigned'],
  ['FirstReviewInProgress', 1, 'Reviewing'],
  ['WaitingForSecondReview', 2, 'Waiting'],
  ['SecondReviewInProgress', 2, 'Reviewing'],
  ['WaitingForThirdReview', 3, 'Waiting'],
  ['ThirdReviewInProgress', 3, 'Reviewing'],
  ['Completed', null, 'Completed'],
  ['Rejected', null, 'Rejected'],
].map(([code, reviewLevel, phase]) => [code, {
  code, label: code, reviewLevel, phase, description: 'Description from backend', assignedDescription: null,
}])) as Record<MessageState, MessageStateReferenceDto>;

describe('MessageStage', () => {
  it.each([
    ['New', false, 0],
    ['Assigned', true, 1],
    ['Assigned', false, 0],
    ['FirstReviewInProgress', true, 1],
    ['WaitingForSecondReview', false, 2],
    ['WaitingForSecondReview', true, 3],
    ['SecondReviewInProgress', true, 3],
    ['WaitingForThirdReview', false, 4],
    ['WaitingForThirdReview', true, 5],
    ['ThirdReviewInProgress', true, 5],
  ] satisfies Array<[MessageState, boolean, number]>)('maps %s with assignee %s to action %i', (
    state,
    hasAssignee,
    selectedIndex,
  ) => {
    render(
      <MessageStage
        state={state}
        metadata={{ ...definitions[state], label: "Current stage" }}
        requiredLevels={[1, 2, 3]}
        hasAssignee={hasAssignee}
      />,
    );

    expect(screen.getByTestId('stepper')).toHaveAttribute(
      'data-selected-index',
      String(selectedIndex),
    );
    expect(screen.getByLabelText('Stage: Current stage')).toBeInTheDocument();
  });

  it.each([
    ['Completed', 'message-stage--completed'],
    ['Rejected', 'message-stage--rejected'],
  ] satisfies Array<[MessageState, string]>)('marks %s as a final stage', (state, className) => {
    render(<MessageStage state={state} metadata={definitions[state]} requiredLevels={[1, 2, 3]} hasAssignee={false} />);

    expect(screen.getByLabelText(`Stage: ${state}`)).toHaveClass(className);
  });

  it.each([
    ['WaitingForThirdReview', false, 2],
    ['WaitingForThirdReview', true, 3],
    ['ThirdReviewInProgress', true, 3],
  ] satisfies Array<[MessageState, boolean, number]>)('maps skipped-level workflow %s with assignee %s', (
    state, hasAssignee, selectedIndex,
  ) => {
    render(
      <MessageStage
        state={state}
        metadata={{ ...definitions[state], label: "Third review" }}
        requiredLevels={[1, 3]}
        hasAssignee={hasAssignee}
      />,
    );

    expect(screen.getByTestId('stepper')).toHaveAttribute('data-step-count', '4');
    expect(screen.getByTestId('stepper')).toHaveAttribute('data-selected-index', String(selectedIndex));
    expect(screen.getByTestId('stepper')).toHaveTextContent('1st assignment1st review3rd assignment3rd review');
    expect(screen.queryByText('2nd assignment')).not.toBeInTheDocument();
  });

  it('shows only the label when workflow metadata is missing', () => {
    render(<MessageStage state="New" metadata={definitions.New} requiredLevels={[]} hasAssignee={false} />);
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.queryByTestId('stepper')).not.toBeInTheDocument();
  });
  it.each([false, true])('uses backend tooltip text with assignee %s', (hasAssignee) => {
    render(<MessageStage state="WaitingForSecondReview" metadata={{
      ...definitions.WaitingForSecondReview,
      description: 'Waiting for second review assignment',
      assignedDescription: 'Assigned for second review',
    }} requiredLevels={[1, 2]} hasAssignee={hasAssignee} />);
    const tooltip = hasAssignee ? 'Assigned for second review' : 'Waiting for second review assignment';
    expect(screen.getByTitle(tooltip)).toHaveAttribute('aria-description', tooltip);
  });

  it('uses metadata for progress and descriptions instead of interpreting the state code', () => {
    render(<MessageStage state="New" metadata={{
      ...definitions.SecondReviewInProgress, label: 'Backend label', description: 'Second review in progress',
    }} requiredLevels={[1, 2, 3]} hasAssignee={false} />);
    expect(screen.getByTestId('stepper')).toHaveAttribute('data-selected-index', '3');
    expect(screen.getByTitle('Second review in progress')).toHaveTextContent('Backend label');
  });

  it('shows the state code without progress while the dictionary is unavailable', () => {
    render(<MessageStage state="Assigned" requiredLevels={[1, 2]} hasAssignee />);
    expect(screen.getByText('Assigned')).toBeInTheDocument();
    expect(screen.queryByTestId('stepper')).not.toBeInTheDocument();
  });

  it('accepts a review level encoded as a JSON string', () => {
    render(<MessageStage state="SecondReviewInProgress" metadata={{
      ...definitions.SecondReviewInProgress, reviewLevel: '2',
    }} requiredLevels={[1, 2, 3]} hasAssignee />);
    expect(screen.getByTestId('stepper')).toHaveAttribute('data-selected-index', '3');
  });

});
