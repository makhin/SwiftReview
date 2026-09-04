import { QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { componentProps } = vi.hoisted(() => ({
  componentProps: vi.fn(),
}));

vi.mock('../../shared/api/referenceDataApi', () => ({
  getBranches: vi.fn(() => new Promise(() => undefined)),
  getDepartments: vi.fn(() => new Promise(() => undefined)),
  getMessageStates: vi.fn(() => new Promise(() => undefined)),
  getMessageTypes: vi.fn(() => new Promise(() => undefined)),
  getUsers: vi.fn(() => new Promise(() => undefined)),
  getWorkflows: vi.fn(() => new Promise(() => undefined)),
}));

vi.mock('devextreme-react/data-grid', () => {
  const childComponent = (name: string) =>
    (props: PropsWithChildren<Record<string, unknown>>) => {
      componentProps(name, props);
      return (
        <span data-testid={name}>
          {String(props.caption ?? name)}
          {props.children}
        </span>
      );
    };

  return {
    default: ({ children, ...props }: PropsWithChildren<Record<string, unknown>>) => {
      componentProps('DataGrid', props);
      return <div aria-label="Messages">{children}</div>;
    },
    Column: childComponent('Column'),
    FilterRow: childComponent('FilterRow'),
    HeaderFilter: childComponent('HeaderFilter'),
    Pager: childComponent('Pager'),
    Paging: childComponent('Paging'),
    Lookup: childComponent('Lookup'),
  };
});

import { referenceDataKeys } from '../../shared/api/referenceDataQueries';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import MessagesPage from './MessagesPage';

function renderPage(withReferenceData = true) {
  const queryClient = createTestQueryClient();

  if (withReferenceData) {
    queryClient.setQueryData(referenceDataKeys.users, [
      { id: 1, displayName: 'Alex Morgan' },
    ]);
    queryClient.setQueryData(referenceDataKeys.branches, [{ id: 10, name: 'Warsaw' }]);
    queryClient.setQueryData(referenceDataKeys.departments, [
      { id: 20, name: 'Operations' },
    ]);
    queryClient.setQueryData(referenceDataKeys.messageStates, [
      { code: 'WaitingForSecondReview', label: 'Waiting for second review' },
    ]);
  }

  return render(
    <QueryClientProvider client={queryClient}>
      <MessagesPage />
    </QueryClientProvider>,
  );
}

describe('MessagesPage', () => {
  beforeEach(() => {
    componentProps.mockClear();
  });

  it('configures the remote messages grid', () => {
    renderPage();

    expect(screen.getByRole('heading', { name: 'Messages' })).toBeInTheDocument();
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();
    expect(screen.getAllByTestId('Column')).toHaveLength(10);

    const dataGridProps = componentProps.mock.calls.find(([name]) => name === 'DataGrid')?.[1];
    expect(dataGridProps).toMatchObject({
      remoteOperations: true,
      rowAlternationEnabled: true,
      noDataText: 'No messages found',
    });

    const pagingProps = componentProps.mock.calls.find(([name]) => name === 'Paging')?.[1];
    expect(pagingProps).toMatchObject({ defaultPageSize: 20 });

    const captions = componentProps.mock.calls
      .filter(([name]) => name === 'Column')
      .map(([, props]) => props.caption);
    expect(captions).toEqual([
      'External ID',
      'Message type',
      'Branch',
      'Department',
      'State',
      'Received',
      'Account',
      'CCY',
      'Amount',
      'Assignee',
    ]);

    const lookupColumns = componentProps.mock.calls
      .filter(
        ([name, props]) =>
          name === 'Column' &&
          ['branchId', 'departmentId', 'state', 'currentAssigneeId'].includes(
            props.dataField,
          ),
      )
      .map(([, props]) => props);
    expect(lookupColumns).toHaveLength(4);
    expect(lookupColumns).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ dataField: 'branchId', allowSorting: false }),
        expect.objectContaining({ dataField: 'departmentId', allowSorting: false }),
        expect.objectContaining({ dataField: 'state' }),
        expect.objectContaining({ dataField: 'currentAssigneeId', allowSorting: false }),
      ]),
    );

    const lookups = componentProps.mock.calls
      .filter(([name]) => name === 'Lookup')
      .map(([, props]) => props);
    expect(lookups).toEqual([
      {
        dataSource: [{ id: 10, name: 'Warsaw' }],
        valueExpr: 'id',
        displayExpr: 'name',
      },
      {
        dataSource: [{ id: 20, name: 'Operations' }],
        valueExpr: 'id',
        displayExpr: 'name',
      },
      {
        dataSource: [
          { code: 'WaitingForSecondReview', label: 'Waiting for second review' },
        ],
        valueExpr: 'code',
        displayExpr: 'label',
      },
      {
        dataSource: [{ id: 1, displayName: 'Alex Morgan' }],
        valueExpr: 'id',
        displayExpr: 'displayName',
      },
    ]);
  });

  it('keeps numeric columns available while reference data is unavailable', () => {
    renderPage(false);

    expect(screen.getAllByTestId('Column')).toHaveLength(10);
    expect(screen.queryAllByTestId('Lookup')).toHaveLength(0);
  });
});
