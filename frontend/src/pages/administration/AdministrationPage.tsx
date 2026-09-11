import { useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import DataGrid, { Column, Pager, Paging } from 'devextreme-react/data-grid';
import type { DataGridRef } from 'devextreme-react/data-grid';
import SelectBox from 'devextreme-react/select-box';
import TagBox from 'devextreme-react/tag-box';
import Button from 'devextreme-react/button';
import Tabs from 'devextreme-react/tabs';
import TextBox from 'devextreme-react/text-box';
import { confirm as confirmDialog } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import type { AccessCatalogDto, RoleDetailsDto, ScopedRoleAssignmentDto, UserAccessDetailsDto } from '../../shared/api/generated/contracts.generated';
import PageLoading from '../../shared/components/feedback/PageLoading';
import PageError from '../../shared/components/feedback/PageError';
import GridRefreshButton from '../../shared/components/GridRefreshButton';
import { createAdminUsersStore, getAccessCatalog, getUserAccess, updateRolePermissions, updateUserAccess, type AdminUser } from './administrationApi';
import './administration.css';

const administrationTabs = [
  { id: 'users' as const, text: 'Users' },
  { id: 'roles' as const, text: 'Roles' },
];

export default function AdministrationPage() {
  const user = useQuery(currentUserQueryOptions());
  if (user.isPending) return <PageLoading message="Checking administrator access…" />;
  if (user.error || !user.data?.isGlobalAdministrator) return <PageError title="Access denied" message="This page is available only to global administrators." actionLabel="Retry" onAction={() => void user.refetch()} />;
  return <Administration />;
}

function Administration() {
  const [tab, setTab] = useState<'users' | 'roles'>('users');
  const [selectedUser, setSelectedUser] = useState<AdminUser | null>(null);
  const [selectedRole, setSelectedRole] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [dirty, setDirty] = useState(false);
  const gridRef = useRef<DataGridRef<AdminUser, number>>(null);
  const catalog = useQuery({ queryKey: ['admin', 'catalog'], queryFn: ({ signal }) => getAccessCatalog(signal) });
  const details = useQuery({ queryKey: ['admin', 'user', selectedUser?.id], queryFn: ({ signal }) => getUserAccess(selectedUser!.id, signal), enabled: selectedUser !== null });
  const store = useMemo(() => createAdminUsersStore(appliedSearch), [appliedSearch]);
  const role = catalog.data?.roles.find((r) => Number(r.id) === selectedRole);
  async function leaveEditor() {
    if (dirty && !await confirmDialog('Discard unsaved access changes?', 'Unsaved changes')) return false;
    setDirty(false);
    return true;
  }
  async function selectUser(nextUser: AdminUser) {
    if (selectedUser?.id !== nextUser.id && await leaveEditor()) setSelectedUser(nextUser);
  }
  async function selectRole(roleId: number) {
    if (await leaveEditor()) setSelectedRole(roleId);
  }
  async function closeUserEditor() {
    if (await leaveEditor()) setSelectedUser(null);
  }
  return <main className="app-content app-page administration">
    <header className="app-page-header"><div><h1 className="app-page-title">Users & Access</h1>
      <p className="app-page-subtitle">Permissions come only from roles, within each branch and department.</p></div></header>
    <Tabs className="administration__tabs" items={administrationTabs} keyExpr="id" selectedIndex={tab === 'users' ? 0 : 1}
      elementAttr={{ 'aria-label': 'Administration sections' }}
      onSelectionChanging={(event) => { event.cancel = leaveEditor().then((canLeave) => !canLeave); }}
      onSelectedIndexChange={(index) => { const nextTab = administrationTabs[index]; if (nextTab) setTab(nextTab.id); }} />
    {catalog.isPending ? <PageLoading message="Loading access catalog…" /> : catalog.error ?
      <PageError title="Unable to load access catalog" message={catalog.error.message} onAction={() => void catalog.refetch()} actionLabel="Retry" /> : catalog.data && <>
      {tab === 'users' ? <>
        <form className="admin-search-toolbar" onSubmit={(e) => { e.preventDefault(); setAppliedSearch(search); }}>
          <div className="admin-search-field">
            <label className="app-label" htmlFor="admin-user-search">Find user</label>
            <TextBox value={search} height={36} maxLength={100} valueChangeEvent="input" placeholder="e.g. Amelia or amelia.hart…"
              inputAttr={{ id: 'admin-user-search', name: 'user-search', autoComplete: 'off', spellCheck: 'false' }}
              onValueChanged={(event) => setSearch(event.value)} />
          </div>
          <div className="admin-search-actions">
            <Button text="Search" type="default" height={36} useSubmitBehavior />
            <GridRefreshButton refresh={() => gridRef.current?.instance().refresh()} />
          </div>
        </form>
        <DataGrid ref={gridRef} dataSource={store} remoteOperations={{ paging: true, sorting: true }} showBorders={false}
          columnAutoWidth elementAttr={{ 'aria-label': 'Users' }} noDataText="No users found">
          <Paging defaultPageSize={20} /><Pager visible showInfo />
          <Column dataField="displayName" caption="Name" /><Column dataField="userName" caption="Username" />
          <Column caption="Access" allowSorting={false} cellRender={({ data }: { data: AdminUser }) =>
            <Button text="Edit access" onClick={() => void selectUser(data)} />} />
        </DataGrid>
        {selectedUser && (details.isPending ? <PageLoading message="Loading user access…" /> : details.error ?
          <PageError title="Unable to load user access" message={details.error.message} onAction={() => void details.refetch()} actionLabel="Retry" /> : details.data &&
          <UserEditor key={selectedUser.id} user={details.data} catalog={catalog.data} onDirty={setDirty} onClose={() => void closeUserEditor()} />)}
      </> : <>
        <SelectBox items={catalog.data.roles} valueExpr="id" displayExpr="name" value={selectedRole}
          inputAttr={{ 'aria-label': 'Role' }} placeholder="Select a role"
          onValueChanged={(e) => void selectRole(e.value as number)} />
        {role && <RoleEditor key={role.id} role={role} catalog={catalog.data} onDirty={setDirty} />}
      </>}
    </>}
  </main>;
}

function UserEditor({ user, catalog, onDirty, onClose }: {
  user: UserAccessDetailsDto; catalog: AccessCatalogDto; onDirty: (dirty: boolean) => void; onClose: () => void;
}) {
  const [assignments, setAssignments] = useState<ScopedRoleAssignmentDto[]>(user.assignments);
  const queryClient = useQueryClient();
  const mutation = useMutation({ mutationFn: () => updateUserAccess(Number(user.userId), { assignments }), onSuccess: async () => {
    onDirty(false); notify('Changes saved.', 'success', 4000); await queryClient.invalidateQueries();
  } });
  function change(next: ScopedRoleAssignmentDto[]) { setAssignments(next); onDirty(true); mutation.reset(); }
  function update(index: number, patch: Partial<ScopedRoleAssignmentDto>) { change(assignments.map((a, i) => i === index ? { ...a, ...patch } : a)); }
  return <section className="app-card admin-editor" aria-label="User access editor">
    <h2>{user.displayName}</h2><p>{user.userName}</p>
    <fieldset disabled={mutation.isPending}>
      {assignments.map((assignment, index) => <div className="admin-scope" key={index}>
        <SelectBox items={catalog.branches} valueExpr="id" displayExpr="name" value={assignment.branchId || null}
          inputAttr={{ 'aria-label': `Branch ${index + 1}` }} placeholder="Branch" onValueChanged={(e) => update(index, { branchId: e.value as number })} />
        <SelectBox items={catalog.departments} valueExpr="id" displayExpr="name" value={assignment.departmentId || null}
          inputAttr={{ 'aria-label': `Department ${index + 1}` }} placeholder="Department" onValueChanged={(e) => update(index, { departmentId: e.value as number })} />
        <TagBox items={catalog.roles} valueExpr="id" displayExpr="name" value={assignment.roleIds} showSelectionControls
          inputAttr={{ 'aria-label': `Roles ${index + 1}` }} onValueChanged={(e) => update(index, { roleIds: e.value as number[] })} />
        <Button text="Remove scope" onClick={() => change(assignments.filter((_, i) => i !== index))} />
        <p className="admin-permissions">Effective permissions: {[...new Set(catalog.roles.filter((r) => assignment.roleIds.includes(r.id)).flatMap((r) => r.permissions))].sort().join(', ') || 'None'}</p>
      </div>)}
      {!assignments.length && <p>No business access. Add a scope to assign roles.</p>}
      <div className="admin-toolbar"><Button text="Add scope" onClick={() => change([...assignments, { branchId: 0, departmentId: 0, roleIds: [] }])} />
        <Button text={mutation.isPending ? 'Saving…' : 'Save access'} type="default" onClick={() => mutation.mutate()} />
        <Button text="Close" onClick={onClose} /></div>
    </fieldset>
    <SaveError error={mutation.error} />
  </section>;
}

function RoleEditor({ role, catalog, onDirty }: { role: RoleDetailsDto; catalog: AccessCatalogDto; onDirty: (dirty: boolean) => void }) {
  const [permissions, setPermissions] = useState(role.permissions);
  const queryClient = useQueryClient();
  const mutation = useMutation({ mutationFn: () => updateRolePermissions(Number(role.id), { permissions }), onSuccess: async () => {
    onDirty(false); notify('Changes saved.', 'success', 4000); await queryClient.invalidateQueries();
  } });
  return <section className="app-card admin-editor" aria-label="Role permissions editor"><h2>{role.name}</h2>
    <p>Changes affect every user assigned this role, in all of its scopes.</p>
    <fieldset disabled={mutation.isPending}>
      <TagBox items={catalog.permissions} value={permissions} showSelectionControls inputAttr={{ 'aria-label': 'Role permissions' }}
        onValueChanged={(e) => { setPermissions(e.value as string[]); onDirty(true); mutation.reset(); }} />
      <div className="admin-toolbar"><Button text={mutation.isPending ? 'Saving…' : 'Save permissions'} type="default" onClick={() => mutation.mutate()} />
        <Button text="Reset" onClick={() => { setPermissions(role.permissions); onDirty(false); mutation.reset(); }} /></div>
    </fieldset><SaveError error={mutation.error} />
  </section>;
}

function SaveError({ error }: { error: Error | null }) {
  return error ? <p role="alert">{error.message}</p> : null;
}
