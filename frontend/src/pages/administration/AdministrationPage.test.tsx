import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useImperativeHandle, type ReactNode, type Ref } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestQueryClient } from '../../test/createTestQueryClient';

const mocks = vi.hoisted(() => ({
  getCurrentUser: vi.fn(), getAccessCatalog: vi.fn(), getUserAccess: vi.fn(),
  updateUserAccess: vi.fn(), updateRolePermissions: vi.fn(), createAdminUsersStore: vi.fn(),
  refreshGrid: vi.fn(), confirmDialog: vi.fn(), notify: vi.fn(),
}));
vi.mock('../../shared/api/currentUserApi', () => ({ getCurrentUser: mocks.getCurrentUser }));
vi.mock('./administrationApi', () => mocks);
vi.mock('devextreme-react/data-grid', () => ({
  default: function MockDataGrid({ children, ref }: { children: ReactNode; ref?: Ref<unknown> }) {
    useImperativeHandle(ref, () => ({ instance: () => ({ refresh: mocks.refreshGrid }) }));
    return <div>{children}</div>;
  },
  Column: ({ cellRender }: { cellRender?: (cell: { data: { id: number; userName: string; displayName: string } }) => ReactNode }) =>
    cellRender?.({ data: { id: 1, userName: 'reviewer', displayName: 'Reviewer' } }),
  Pager: () => null, Paging: () => null,
}));
vi.mock('devextreme-react/button', () => ({
  default: ({ text, onClick, useSubmitBehavior }: { text: string; onClick?: () => void; useSubmitBehavior?: boolean }) =>
    <button type={useSubmitBehavior ? 'submit' : 'button'} onClick={onClick}>{text}</button>,
}));
vi.mock('devextreme/ui/dialog', () => ({ confirm: mocks.confirmDialog }));
vi.mock('devextreme/ui/notify', () => ({ default: mocks.notify }));
vi.mock('devextreme-react/tabs', () => ({
  default: ({ items, selectedIndex, elementAttr, onSelectionChanging, onSelectedIndexChange }: {
    items: { id: string; text: string }[];
    selectedIndex: number;
    elementAttr: { 'aria-label': string };
    onSelectionChanging: (event: { cancel: boolean | PromiseLike<boolean> }) => void;
    onSelectedIndexChange: (index: number) => void;
  }) => <div role="tablist" aria-label={elementAttr['aria-label']}>{items.map((item, index) =>
    <button key={item.id} type="button" role="tab" aria-selected={selectedIndex === index}
      onClick={() => { const event = { cancel: false as boolean | PromiseLike<boolean> }; onSelectionChanging(event);
        void Promise.resolve(event.cancel).then((cancelled) => { if (!cancelled) onSelectedIndexChange(index); }); }}>{item.text}</button>)}</div>,
}));
vi.mock('devextreme-react/text-box', () => ({
  default: ({ value, maxLength, inputAttr, placeholder, onValueChanged }: {
    value: string;
    maxLength: number;
    inputAttr: { id: string };
    placeholder: string;
    onValueChanged: (event: { value: string }) => void;
  }) => <input id={inputAttr.id} value={value} maxLength={maxLength} placeholder={placeholder}
    onChange={(event) => onValueChanged({ value: event.target.value })} />,
}));
type SelectProps = { items: (string | { id: number; name: string })[]; value: number | string | number[] | string[] | null;
  inputAttr: { 'aria-label': string }; onValueChanged: (event: { value: unknown }) => void };
vi.mock('devextreme-react/select-box', () => ({
  default: ({ items, value, inputAttr, onValueChanged }: SelectProps) => <select aria-label={inputAttr['aria-label']}
    value={value as number ?? ''} onChange={(e) => onValueChanged({ value: Number(e.target.value) })}>
    <option value="">Choose</option>{items.map((item) => typeof item === 'string' ? <option key={item}>{item}</option> :
      <option key={item.id} value={item.id}>{item.name}</option>)}</select>,
}));
vi.mock('devextreme-react/tag-box', () => ({
  default: ({ items, value, inputAttr, onValueChanged }: SelectProps) => <select multiple aria-label={inputAttr['aria-label']}
    value={(value as (string | number)[]).map(String)} onChange={(e) => onValueChanged({ value: Array.from(e.target.selectedOptions).map((option) =>
      typeof items[0] === 'string' ? option.value : Number(option.value)) })}>
    {items.map((item) => typeof item === 'string' ? <option key={item}>{item}</option> :
      <option key={item.id} value={item.id}>{item.name}</option>)}</select>,
}));

import AdministrationPage from './AdministrationPage';

const catalog = {
  roles: [{ id: 1, name: 'Reviewer', permissions: ['message.view', 'review.level1'] },
    { id: 2, name: 'Auditor', permissions: ['message.view', 'audit.view'] }],
  permissions: ['message.view', 'review.level1', 'audit.view'],
  branches: [{ id: 1, name: 'London' }, { id: 2, name: 'Dublin' }],
  departments: [{ id: 1, name: 'Operations' }, { id: 2, name: 'Compliance' }],
};
function renderPage() {
  return render(<QueryClientProvider client={createTestQueryClient()}><AdministrationPage /></QueryClientProvider>);
}

describe('AdministrationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getCurrentUser.mockResolvedValue({ isGlobalAdministrator: true });
    mocks.getAccessCatalog.mockResolvedValue(catalog);
    mocks.getUserAccess.mockResolvedValue({ userId: 1, userName: 'reviewer', displayName: 'Reviewer',
      assignments: [{ branchId: 1, departmentId: 1, roleIds: [1] }], scopes: [] });
    mocks.updateUserAccess.mockResolvedValue(undefined);
    mocks.updateRolePermissions.mockResolvedValue(undefined);
    mocks.confirmDialog.mockReset().mockResolvedValue(true);
    mocks.notify.mockReset();
  });

  it('does not load administrator data for ordinary users', async () => {
    mocks.getCurrentUser.mockResolvedValue({ isGlobalAdministrator: false });
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent('Access denied');
    expect(mocks.getAccessCatalog).not.toHaveBeenCalled();
    expect(mocks.createAdminUsersStore).not.toHaveBeenCalled();
  });

  it('shows scoped access and saves only role assignments, never global admin or individual permissions', async () => {
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit access' }));
    expect(await screen.findByLabelText('Branch 1')).toHaveValue('1');
    expect(screen.getByText('Effective permissions: message.view, review.level1')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Branch 1'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save access' }));
    await waitFor(() => expect(mocks.updateUserAccess).toHaveBeenCalledWith(1, {
      assignments: [{ branchId: 2, departmentId: 1, roleIds: [1] }],
    }));
    await waitFor(() => expect(mocks.notify).toHaveBeenCalledWith('Changes saved.', 'success', 4000));
    expect(screen.queryByText('Changes saved.')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Global administrator')).not.toBeInTheDocument();
  });

  it('refreshes users without losing unsaved access edits', async () => {
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit access' }));
    fireEvent.change(await screen.findByLabelText('Branch 1'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeInTheDocument());
    expect(mocks.refreshGrid).toHaveBeenCalledOnce();
    expect(screen.getByLabelText('Branch 1')).toHaveValue('2');
    expect(mocks.updateUserAccess).not.toHaveBeenCalled();
  });

  it('applies the user search through the DevExtreme editor form', async () => {
    renderPage();
    fireEvent.change(await screen.findByLabelText('Find user'), { target: { value: 'alex' } });
    fireEvent.click(screen.getByRole('button', { name: 'Search' }));
    await waitFor(() => expect(mocks.createAdminUsersStore).toHaveBeenLastCalledWith('alex'));
  });

  it('can remove business access entirely', async () => {
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit access' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Remove scope' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save access' }));
    await waitFor(() => expect(mocks.updateUserAccess).toHaveBeenCalledWith(1, { assignments: [] }));
  });

  it('keeps edits visible when the server refuses an active-review revocation', async () => {
    mocks.updateUserAccess.mockRejectedValue(new Error('An active review must be completed first.'));
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit access' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Remove scope' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save access' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('An active review');
    expect(screen.getByText('No business access. Add a scope to assign roles.')).toBeInTheDocument();
    expect(screen.queryByText('Changes saved.')).not.toBeInTheDocument();
    expect(mocks.notify).not.toHaveBeenCalled();
  });

  it('edits role permissions and warns about all affected assignments', async () => {
    renderPage();
    await screen.findByRole('button', { name: 'Edit access' });
    fireEvent.click(screen.getByRole('tab', { name: 'Roles' }));
    fireEvent.change(await screen.findByLabelText('Role'), { target: { value: '1' } });
    expect(await screen.findByText(/Changes affect every user/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Role permissions'), { target: { value: 'audit.view' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save permissions' }));
    await waitFor(() => expect(mocks.updateRolePermissions).toHaveBeenCalledWith(1, { permissions: ['audit.view'] }));
    expect(mocks.notify).toHaveBeenCalledWith('Changes saved.', 'success', 4000);
  });

  it('asks before discarding unsaved edits when switching tabs', async () => {
    mocks.confirmDialog.mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit access' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Remove scope' }));
    fireEvent.click(screen.getByRole('tab', { name: 'Roles' }));
    await waitFor(() => expect(mocks.confirmDialog).toHaveBeenCalledOnce());
    expect(screen.getByRole('tab', { name: 'Users' })).toHaveAttribute('aria-selected', 'true');
    fireEvent.click(screen.getByRole('tab', { name: 'Roles' }));
    expect(await screen.findByLabelText('Role')).toBeInTheDocument();
    expect(mocks.confirmDialog).toHaveBeenCalledTimes(2);
  });
});
