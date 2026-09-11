import { QueryClientProvider } from '@tanstack/react-query';
import { useImperativeHandle, type PropsWithChildren, type Ref } from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { componentProps, refreshGrid, getCurrentUser, rowOverrides, stateCounts, gridFilter, gridPageIndex } = vi.hoisted(() => ({
  componentProps: vi.fn(),
  stateCounts: vi.fn().mockResolvedValue([{ state: 'New', count: 15 }, { state: 'Assigned', count: 5 }]),
  gridFilter: vi.fn(),
  gridPageIndex: vi.fn(),
  refreshGrid: vi.fn(),
  getCurrentUser: vi.fn(),
  rowOverrides: {} as Record<string, unknown>,
}));

vi.mock('./messagesApi', async (original) => ({ ...await original<typeof import('./messagesApi')>(), getMessageStateCounts: stateCounts }));

vi.mock('../../shared/api/referenceDataApi', () => ({
  getBranches: vi.fn(() => new Promise(() => undefined)),
  getDepartments: vi.fn(() => new Promise(() => undefined)),
  getMessageStates: vi.fn(() => new Promise(() => undefined)),
  getMessageTypes: vi.fn(() => new Promise(() => undefined)),
  getUsers: vi.fn(() => new Promise(() => undefined)),
  getWorkflows: vi.fn(() => new Promise(() => undefined)),
}));
vi.mock('../../shared/api/currentUserApi', () => ({
  getCurrentUser,
}));

vi.mock('devextreme-react/data-grid', () => {
  const rowData = {
    id: 42,
    canReview: true,
    externalId: 'MSG-0042',
    messageType: 'MT103',
    branchId: 10,
    departmentId: 20,
    state: 'New',
    receivedAt: '2026-09-05T08:00:00Z',
    currentAssigneeId: null,
    activeReviewId: null,
    activeReviewLevel: null,
    activeReviewerId: null,
  };
  const childComponent = (name: string) =>
    (props: PropsWithChildren<Record<string, unknown>>) => {
      componentProps(name, props);
      return (
        <span data-testid={name}>
          {String(props.caption ?? name)}
          {props.children}
          {name === 'Column' && props.caption === 'Actions' &&
            (props.cellRender as (cell: { data: typeof rowData }) => React.ReactNode)({
              data: { ...rowData, ...rowOverrides },
            })}
        </span>
      );
    };

  return {
    default: function MockDataGrid({ children, ref, ...props }: PropsWithChildren<{
      ref?: Ref<{ instance: () => { refresh: typeof refreshGrid; filter: typeof gridFilter; pageIndex: typeof gridPageIndex; beginUpdate: () => void; endUpdate: () => void } }>;
    }>) {
      useImperativeHandle(ref, () => ({ instance: () => ({ refresh: refreshGrid, filter: gridFilter, pageIndex: gridPageIndex, beginUpdate: () => undefined, endUpdate: () => undefined }) }));
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
vi.mock('devextreme-react/button', () => ({
  default: ({ text, onClick, elementAttr, ...props }: Record<string, unknown>) => {
    componentProps('Button', { text, onClick, ...props });
    const attributes = elementAttr as { 'aria-label'?: string } | undefined;
    return (
      <button
        type="button"
        aria-label={attributes?.['aria-label']}
        onClick={() => (onClick as () => void)()}
      >
        {text == null ? null : String(text)}
      </button>
    );
  },
}));
vi.mock('devextreme-react/radio-group', () => ({
  default: ({ items, value, name, elementAttr, onValueChanged, itemRender }: {
    items: { selectionKey: number; label: string }[];
    value: number | null;
    name: string;
    elementAttr: { 'aria-labelledby': string };
    onValueChanged: (event: { value: number }) => void;
    itemRender: (item: { selectionKey: number; label: string }) => React.ReactNode;
  }) => <div role="radiogroup" aria-labelledby={elementAttr['aria-labelledby']}>{items.map((item) =>
    <label key={item.selectionKey}><input type="radio" name={name} aria-label={item.label}
      checked={value === item.selectionKey} onChange={() => onValueChanged({ value: item.selectionKey })} />
      {itemRender(item)}</label>)}</div>,
}));
vi.mock('devextreme-react/drawer', () => ({
  default: ({
    children,
    render: renderPanel,
    ...props
  }: PropsWithChildren<Record<string, unknown>>) => {
    componentProps('Drawer', props);
    return (
      <div>
        {children}
        {props.opened && (renderPanel as () => React.ReactNode)()}
      </div>
    );
  },
}));
vi.mock('./AuditTrailDrawer', () => ({
  default: ({ message, onClose }: { message: { externalId: string }; onClose: () => void }) => (
    <aside aria-label="Audit trail">
      {message.externalId}
      <button type="button" onClick={onClose}>Close</button>
    </aside>
  ),
}));
vi.mock('./AssignmentPopup', () => ({
  default: ({ message, onClose, onChanged }: {
    message: { externalId: string };
    onClose: () => void;
    onChanged: () => void;
  }) => (
    <aside aria-label="Assignment dialog">
      {message.externalId}
      <button type="button" onClick={onChanged}>Save assignment</button>
      <button type="button" onClick={onClose}>Close assignment</button>
    </aside>
  ),
}));

vi.mock('./ReviewDecisionPopup', () => ({
  default: ({ message, canApprove, canReject, onClose, onChanged }: {
    message: { externalId: string };
    canApprove: boolean;
    canReject: boolean;
    onClose: () => void;
    onChanged: () => void;
  }) => (
    <aside aria-label="Review dialog">
      {message.externalId}
      <button type="button" disabled={!canApprove}>Approve</button>
      <button type="button" disabled={!canReject}>Reject</button>
      <button type="button" onClick={onChanged}>Save review</button>
      <button type="button" onClick={onClose}>Close review</button>
    </aside>
  ),
}));

import MessagesGrid from './MessagesGrid';
import { messageDataSource } from './messageDataSource';
import { referenceDataKeys } from '../../shared/api/referenceDataQueries';
import { createTestQueryClient } from '../../test/createTestQueryClient';
import MessagesPage from './MessagesPage';

function renderPage(
  withReferenceData = true,
  permissions = ['message.view', 'message.assign'],
  withCurrentUser = true,
  isGlobalAdministrator = false,
) {
  const queryClient = createTestQueryClient();
  if (withCurrentUser) {
    queryClient.setQueryData(['current-user'], {
      userId: 1,
      userName: 'alex.morgan',
      permissions,
      isGlobalAdministrator,
      scopes: isGlobalAdministrator ? [] : [{ branchId: 10, departmentId: 20, roleIds: [1], permissions }],
      branches: [10],
      departments: [20],
    });
  }

  if (withReferenceData) {
    queryClient.setQueryData(referenceDataKeys.users, [
      {
        id: 1,
        userName: 'alex.morgan',
        displayName: 'Alex Morgan',
        branchIds: [10],
        departmentIds: [20],
      },
      {
        id: 2,
        userName: 'sam.lee',
        displayName: 'Sam Lee',
        branchIds: [10],
        departmentIds: [20, 30],
      },
      {
        id: 3,
        userName: 'pat.taylor',
        displayName: 'Pat Taylor',
        branchIds: [10],
        departmentIds: [],
      },
    ]);
    queryClient.setQueryData(referenceDataKeys.branches, [{ id: 10, name: 'Warsaw' }]);
    queryClient.setQueryData(referenceDataKeys.departments, [
      { id: 20, name: 'Operations' },
      { id: 30, name: 'Compliance' },
    ]);
    queryClient.setQueryData(referenceDataKeys.messageStates, [
      { code: 'WaitingForSecondReview', label: 'Waiting for second review' },
    ]);
  }

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/messages']}>
        <Routes>
          <Route path="/messages" element={<MessagesPage />} />
          <Route path="/messages/assigned" element={<main>Assigned messages page</main>} />
          <Route path="/me" element={<main>User profile</main>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('MessagesPage', () => {
  it.each([['Assigned', 1], ['WaitingForSecondReview', 2], ['ThirdReviewInProgress', 3]] as const)(
    'uses the shared stage colours in the administrator assignment grid: %s', (state, level) => {
      renderPage(true, [], true, true);
      expect(screen.getByText('Messages that need your attention are marked with a green line.')).toBeInTheDocument();
      const prepare = componentProps.mock.calls.filter(([name]) => name === 'DataGrid').at(-1)![1].onRowPrepared;
      const rowElement = document.createElement('tr');
      prepare({ rowType: 'data', rowElement, data: { state, currentAssigneeId: 7 } });
      expect(rowElement).toHaveClass(`message-review-level-${level}`);
      expect(rowElement).not.toHaveClass('message-assigned-to-you');
      prepare({ rowType: 'data', rowElement, data: { state, currentAssigneeId: 1 } });
      expect(rowElement).toHaveClass(`message-review-level-${level}`, 'message-assigned-to-you');
    },
  );
  it('shows Undo to a global administrator in the assignment grid', () => {
    renderPage(true, [], true, true);
    expect(screen.getByRole('button', { name: 'Undo' })).toBeInTheDocument();
  });

  it('does not expose Undo to an ordinary user even with review.undo permission', () => {
    renderPage(true, ['message.view', 'message.assign', 'review.undo']);
    expect(screen.queryByRole('button', { name: 'Undo' })).not.toBeInTheDocument();
  });

  it('allows an administrator without permissions to review another users message from this page', () => {
    Object.assign(rowOverrides, { state: 'SecondReviewInProgress', currentAssigneeId: 7, activeReviewerId: 7, branchId: 99, departmentId: 99 });
    renderPage(true, [], true, true);
    expect(screen.getByRole('button', { name: 'View audit trail' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Review' }));
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
  });

  it('allows an administrator to assign in a scope without roles', () => {
    Object.assign(rowOverrides, { branchId: 99, departmentId: 99 });
    renderPage(true, [], true, true);
    expect(screen.getByRole('button', { name: 'Assign' })).toBeInTheDocument();
  });
  beforeEach(() => {
    componentProps.mockClear();
    refreshGrid.mockReset();
    getCurrentUser.mockReset().mockImplementation(() => new Promise(() => undefined));
    for (const key of Object.keys(rowOverrides)) delete rowOverrides[key];
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('waits for the current user before displaying the messages grid', () => {
    renderPage(true, undefined, false);
    expect(screen.getByText('Loading messages…')).toBeInTheDocument();
    expect(screen.queryByLabelText('Messages')).not.toBeInTheDocument();
  });

  it('refreshes the existing messages grid on request', async () => {
    renderPage();
    expect(screen.getByRole('heading', { level: 1, name: 'Messages' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeInTheDocument());
    expect(refreshGrid).toHaveBeenCalledOnce();
  });

  it('retries a failed current-user request before granting access', async () => {
    getCurrentUser.mockRejectedValueOnce(new Error('Network unavailable'));
    renderPage(true, undefined, false);
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to verify access');
    expect(screen.queryByLabelText('Messages')).not.toBeInTheDocument();

    getCurrentUser.mockResolvedValue({
      userId: 1,
      permissions: ['message.view', 'message.assign'],
    });
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByLabelText('Messages')).toBeInTheDocument();
    expect(getCurrentUser).toHaveBeenCalledTimes(3);
  });

  it('opens a combined review, refreshes after saving and restores focus when closed', async () => {
    Object.assign(rowOverrides, { state: 'Assigned', currentAssigneeId: 1 });
    const queryClient = createTestQueryClient();
    queryClient.setQueryData(['current-user'], {
      userId: 1,
      permissions: ['message.view', 'review.level1'],
      scopes: [{ branchId: 10, departmentId: 20, roleIds: [1], permissions: ['message.view', 'review.level1'] }],
    });
    render(
      <QueryClientProvider client={queryClient}>
        <MessagesGrid dataSource={messageDataSource} enableReviewActions />
      </QueryClientProvider>,
    );
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
    const trigger = screen.getByRole('button', { name: 'Review' });
    trigger.focus();
    fireEvent.click(trigger);
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Save review' }));
    expect(refreshGrid).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Close review' }));
    expect(screen.queryByLabelText('Review dialog')).not.toBeInTheDocument();
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('refreshes after assignment and restores focus when closed', async () => {
    renderPage(true, ['message.view', 'message.assign']);
    const trigger = screen.getByRole('button', { name: 'Assign' });
    trigger.focus();
    fireEvent.click(trigger);
    fireEvent.click(screen.getByRole('button', { name: 'Save assignment' }));
    expect(refreshGrid).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Close assignment' }));
    expect(screen.queryByLabelText('Assignment dialog')).not.toBeInTheDocument();
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('configures the remote messages grid', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Messages' })).toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveClass('app-page--wide');
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();
    expect(screen.getAllByTestId('Column')).toHaveLength(8);

    const dataGridProps = componentProps.mock.calls.find(([name]) => name === 'DataGrid')?.[1];
    expect(dataGridProps).toMatchObject({
      remoteOperations: true,
      rowAlternationEnabled: true,
      noDataText: 'No messages found',
      width: '100%',
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
      'Stage',
      'Received',
      'Assignee',
      'Actions',
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
        dataSource: [
          { id: 20, name: 'Operations' },
          { id: 30, name: 'Compliance' },
        ],
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
        dataSource: [
          {
            id: 1,
            userName: 'alex.morgan',
            displayName: 'Alex Morgan',
            displayLabel: 'Alex Morgan — Operations',
            branchIds: [10],
            departmentIds: [20],
          },
          {
            id: 2,
            userName: 'sam.lee',
            displayName: 'Sam Lee',
            displayLabel: 'Sam Lee — Operations, Compliance',
            branchIds: [10],
            departmentIds: [20, 30],
          },
          {
            id: 3,
            userName: 'pat.taylor',
            displayName: 'Pat Taylor',
            displayLabel: 'Pat Taylor — No departments',
            branchIds: [10],
            departmentIds: [],
          },
        ],
        valueExpr: 'id',
        displayExpr: 'displayLabel',
      },
    ]);
  });

  it('keeps numeric columns available while reference data is unavailable', () => {
    renderPage(false);

    expect(screen.getAllByTestId('Column')).toHaveLength(8);
    expect(screen.queryAllByTestId('Lookup')).toHaveLength(0);
  });

  it('opens View from the shared actions column with decisions disabled', () => {
    renderPage();

    const rawButton = componentProps.mock.calls
      .filter(([name]) => name === 'Button')
      .map(([, props]) => props)
      .find((props) => props.hint === 'View message');
    expect(rawButton).toMatchObject({ icon: 'doc', stylingMode: 'outlined' });

    fireEvent.click(screen.getByRole('button', { name: 'View' }));

    expect(screen.getByLabelText('Review dialog')).toHaveTextContent('MSG-0042');
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Close review' }));
    expect(screen.queryByLabelText('Review dialog')).not.toBeInTheDocument();
  });

  it('shows manual assignment only with permission', () => {
    rowOverrides.departmentId = 30;
    const withoutPermission = renderPage();
    expect(screen.queryByRole('button', { name: 'Assign' })).not.toBeInTheDocument();
    withoutPermission.unmount();
    delete rowOverrides.departmentId;

    renderPage(true, ['message.view', 'message.assign']);
    fireEvent.click(screen.getByRole('button', { name: 'Assign' }));
    expect(screen.getByLabelText('Assignment dialog')).toHaveTextContent('MSG-0042');
    expect(screen.queryByRole('button', { name: 'Reassign' })).not.toBeInTheDocument();
  });

  it('shows the audit action only with permission and opens the right-side drawer', () => {
    const view = renderPage(true, ['message.view', 'message.assign', 'audit.view']);
    view.container.id = 'root';

    expect(screen.getAllByTestId('Column')).toHaveLength(8);
    expect(screen.getByRole('button', { name: 'View' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'View audit trail' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'View audit trail' }));

    expect(screen.getByLabelText('Audit trail')).toHaveTextContent('MSG-0042');
    expect(view.container).toHaveAttribute('inert');
    const drawerProps = componentProps.mock.calls
      .filter(([name]) => name === 'Drawer')
      .at(-1)?.[1];
    expect(drawerProps).toMatchObject({
      opened: true,
      openedStateMode: 'overlap',
      position: 'right',
      animationEnabled: true,
      shading: true,
      closeOnOutsideClick: true,
    });

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByLabelText('Audit trail')).not.toBeInTheDocument();
    expect(view.container).not.toHaveAttribute('inert');
  });

  it('disables audit drawer animation when reduced motion is requested', () => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({
      matches: true,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })));
    renderPage(true, ['message.view', 'message.assign', 'audit.view']);

    fireEvent.click(screen.getByRole('button', { name: 'View audit trail' }));

    const drawerProps = componentProps.mock.calls
      .filter(([name]) => name === 'Drawer')
      .at(-1)?.[1];
    expect(drawerProps).toMatchObject({ animationEnabled: false });
  });

  it('redirects reviewers without assign permission to the review queue', () => {
    renderPage(true, ['message.view', 'review.level1']);

    expect(screen.getByText('Assigned messages page')).toBeInTheDocument();
    expect(screen.queryByLabelText('Messages')).not.toBeInTheDocument();
  });

  it('redirects a view-only user to the queue without rendering the assignment grid', () => {
    renderPage(true, ['message.view']);
    expect(screen.getByText('Assigned messages page')).toBeInTheDocument();
    expect(screen.queryByLabelText('Messages')).not.toBeInTheDocument();
    expect(componentProps.mock.calls.some(([name]) => name === 'DataGrid')).toBe(false);
  });

  it('uses department IDs until department metadata is available', () => {
    const queryClient = createTestQueryClient();
    queryClient.setQueryData(['current-user'], {
      userId: 1,
      userName: 'alex.morgan',
      permissions: ['message.view', 'message.assign'],
      branches: [10],
      departments: [20],
    });
    queryClient.setQueryData(referenceDataKeys.users, [
      {
        id: 1,
        userName: 'alex.morgan',
        displayName: 'Alex Morgan',
        branchIds: [10],
        departmentIds: [20],
      },
    ]);

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/messages']}>
          <MessagesPage />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const assigneeLookup = componentProps.mock.calls
      .filter(([name]) => name === 'Lookup')
      .map(([, props]) => props)
      .find((props) => props.displayExpr === 'displayLabel');
    expect(assigneeLookup?.dataSource).toEqual([
      expect.objectContaining({ displayLabel: 'Alex Morgan — 20' }),
    ]);
  });
});


describe('workflow action', () => {
  it('explains why review history prevents changing workflow', () => {
    renderPage(true, ['message.view', 'workflow.manage']);
    expect(screen.getByText('Change workflow')).toBeInTheDocument();
    expect(screen.getByTitle(/Workflow cannot be changed/)).toHaveAttribute('tabindex', '0');
    expect(componentProps).toHaveBeenCalledWith('Button', expect.objectContaining({ text: 'Change workflow', disabled: true }));
    expect(screen.queryByRole('button', { name: 'Assign' })).not.toBeInTheDocument();
  });

  it('enables changing workflow when the server allows it', () => {
    rowOverrides.canChangeWorkflow = true;
    renderPage(true, [], true, true);
    expect(componentProps).toHaveBeenCalledWith('Button', expect.objectContaining({ text: 'Change workflow', disabled: false }));
    expect(screen.queryByTitle(/Workflow cannot be changed/)).not.toBeInTheDocument();
  });
});

 describe('queue row actions and colours', () => {
   beforeEach(() => { componentProps.mockClear(); for (const key of Object.keys(rowOverrides)) delete rowOverrides[key]; });
   function queue(permissions = ['message.view', 'review.level1', 'review.level2', 'review.level3'], isGlobalAdministrator = false) {
     const client = createTestQueryClient();
     client.setQueryData(['current-user'], { userId: 1, permissions, isGlobalAdministrator, scopes: [{ branchId: 10, departmentId: 20, permissions }] });
     return render(<QueryClientProvider client={client}><MessagesGrid dataSource={messageDataSource} enableReviewActions /></QueryClientProvider>);
   }
   it.each([['Assigned', 1], ['SecondReviewInProgress', 2], ['WaitingForThirdReview', 3]] as const)('highlights an actionable %s and clears recycled rows', (state, level) => {
     Object.assign(rowOverrides, { state, currentAssigneeId: 1, activeReviewerId: 1, canReview: true });
     queue();
     expect(screen.getByRole('button', { name: 'Review' })).toBeInTheDocument();
     const onRowPrepared = componentProps.mock.calls.filter(([name]) => name === 'DataGrid').at(-1)![1].onRowPrepared;
     const rowElement = document.createElement('tr');
     const data = { ...rowOverrides, branchId: 10, departmentId: 20 };
     onRowPrepared({ rowType: 'data', rowElement, data });
     expect(rowElement).toHaveClass(`message-review-level-${level}`);
     onRowPrepared({ rowType: 'data', rowElement, data: { ...data, state: 'Completed', currentAssigneeId: null, canReview: false } });
     expect(rowElement.className).toBe('');
     fireEvent.click(screen.getByRole('button', { name: 'View' }));
     expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
   });
   it.each([
     { state: 'Completed', currentAssigneeId: null, canReview: false },
     { state: 'Assigned', currentAssigneeId: 7, canReview: false },
     { state: 'Assigned', currentAssigneeId: 1, canReview: false },
     { state: 'Assigned', currentAssigneeId: 1, canReview: true, departmentId: 99 },
   ])('hides Review when the row is not actionable: %j', (row) => {
     Object.assign(rowOverrides, row); queue();
     expect(screen.queryByRole('button', { name: 'Review' })).not.toBeInTheDocument();
     expect(screen.getByRole('button', { name: 'View' })).toBeInTheDocument();
   });
   it.each([7, null])('does not personally highlight an administrator-accessible row owned by %s', (owner) => {
     Object.assign(rowOverrides, { state: 'Assigned', currentAssigneeId: owner, activeReviewerId: null, canReview: true });
     queue([], true);
     expect(screen.getByRole('button', { name: 'Review' })).toBeInTheDocument();
     const rowElement = document.createElement('tr');
     const data = { ...rowOverrides, branchId: 10, departmentId: 20 };
     componentProps.mock.calls.filter(([name]) => name === 'DataGrid').at(-1)![1].onRowPrepared({ rowType: 'data', rowElement, data });
     expect(rowElement).toHaveClass('message-review-level-1');
     expect(rowElement).not.toHaveClass('message-assigned-to-you');
   });

   it.each([['Assigned', 1], ['WaitingForSecondReview', 2], ['ThirdReviewInProgress', 3]] as const)('colours %s even without review rights and separately marks personal assignments', (state, level) => {
     queue(['message.view']);
     const rowElement = document.createElement('tr');
     const data = { state, currentAssigneeId: 7, canReview: false };
     const prepare = componentProps.mock.calls.filter(([name]) => name === 'DataGrid').at(-1)![1].onRowPrepared;
     prepare({ rowType: 'data', rowElement, data });
     expect(rowElement).toHaveClass(`message-review-level-${level}`);
     expect(rowElement).not.toHaveClass('message-assigned-to-you');
     prepare({ rowType: 'data', rowElement, data: { ...data, currentAssigneeId: '1' } });
     expect(rowElement).toHaveClass(`message-review-level-${level}`, 'message-assigned-to-you');
     prepare({ rowType: 'data', rowElement, data });
     expect(rowElement).not.toHaveClass('message-assigned-to-you');
   });

 });

describe('message state cards integration', () => {
  beforeEach(() => { gridFilter.mockClear(); gridPageIndex.mockClear(); refreshGrid.mockClear(); stateCounts.mockClear(); });
  it('loads server counts, filters by the selected state and clears only the card filter with All', async () => {
    renderPage();
    expect(screen.getByRole('radio', { name: 'All' })).toBeChecked();
    await waitFor(() => expect(screen.getByLabelText('20 messages')).toBeInTheDocument());
    fireEvent.click(screen.getByRole('radio', { name: 'New' }));
    expect(gridPageIndex).toHaveBeenLastCalledWith(0);
    expect(gridFilter).toHaveBeenLastCalledWith(['state', '=', 'New']);
    expect(screen.getByRole('radio', { name: 'New' })).toBeChecked();
    fireEvent.click(screen.getByRole('radio', { name: 'All' }));
    expect(gridFilter).toHaveBeenLastCalledWith(null);
  });
  it('refreshes the counts with the grid without clearing the selected card', async () => {
    renderPage();
    await waitFor(() => expect(stateCounts).toHaveBeenCalled());
    fireEvent.click(await screen.findByRole('radio', { name: 'Assigned' }));
    const before = stateCounts.mock.calls.length;
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => expect(stateCounts.mock.calls.length).toBeGreaterThan(before));
    expect(screen.getByRole('radio', { name: 'Assigned' })).toBeChecked();
    expect(refreshGrid).toHaveBeenCalledOnce();
  });
});

it('adds a state returned by the server even when reference labels are still cached', async () => {
  stateCounts.mockResolvedValueOnce([{ state: 'FutureState', count: 4 }]);
  renderPage();
  const card = await screen.findByRole('radio', { name: 'Future State' });
  expect(screen.getAllByLabelText('4 messages')).toHaveLength(2);
  fireEvent.click(card);
  expect(gridFilter).toHaveBeenLastCalledWith(['state', '=', 'FutureState']);
});
