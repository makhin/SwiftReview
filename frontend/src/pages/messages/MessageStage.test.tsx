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

import type { MessageState } from '../../shared/api/generated/contracts.generated';
import MessageStage from './MessageStage';

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
        label="Current stage"
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
    render(<MessageStage state={state} label={state} requiredLevels={[1, 2, 3]} hasAssignee={false} />);

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
        label="Third review"
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
    render(<MessageStage state="New" label="New" requiredLevels={[]} hasAssignee={false} />);
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.queryByTestId('stepper')).not.toBeInTheDocument();
  });
});

 it.each([['Assigned', 1], ['WaitingForSecondReview', 2], ['WaitingForThirdReview', 3]] as const)('labels readiness at %s without relying on colour', (state, level) => {
   const view = render(<MessageStage state={state} label={state} requiredLevels={[1, 2, 3]} hasAssignee readyForReview />);
   expect(screen.getByText(`Ready for review · Level ${level}`)).toBeInTheDocument();
   view.rerender(<MessageStage state={state} label={state} requiredLevels={[1, 2, 3]} hasAssignee={false} />);
   expect(screen.queryByText(/Ready for review/)).not.toBeInTheDocument();
   expect(screen.queryByText(/Awaiting assignment/)).not.toBeInTheDocument();
   view.rerender(<MessageStage state={state} label={state} requiredLevels={[1, 2, 3]} hasAssignee />);
   expect(screen.queryByText(/Ready for review/)).not.toBeInTheDocument();
 });

it('marks personal assignment independently of review permission or readiness', () => {
  const view = render(<MessageStage state="Assigned" label="Assigned" requiredLevels={[1]} hasAssignee assignedToYou />);
  expect(screen.getByText('Assigned to you')).toBeInTheDocument();
  expect(screen.queryByText(/Ready for review/)).not.toBeInTheDocument();
  view.rerender(<MessageStage state="Assigned" label="Assigned" requiredLevels={[1]} hasAssignee />);
  expect(screen.queryByText('Assigned to you')).not.toBeInTheDocument();
});
