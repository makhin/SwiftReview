import {
  Field,
  Card,
  DataGrid,
  Select,
  MultiSelect,
  Button,
  Tabs,
  TextInput,
  ConfirmDialog,
} from '@smbc/ui';

import { useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';

import {
  DataGridColumn as Column,
  DataGridPager as Pager,
  DataGridPaging as Paging,
  type DataGridRef,
} from '@smbc/ui/data-grid';

import { useNotify } from '../../shared/notifications/notifications';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import type {
  AccessCatalogDto,
  RoleDetailsDto,
  ScopedRoleAssignmentDto,
  UserAccessDetailsDto,
} from '../../shared/api/generated/contracts.generated';
import PageLoading from '../../shared/components/feedback/PageLoading';
import PageError from '../../shared/components/feedback/PageError';
import GridRefreshButton from '../../shared/components/GridRefreshButton';
import { createAdminUsersStore, type AdminUser } from './administrationApi';
import {
  accessCatalogQueryOptions,
  userAccessQueryOptions,
  useUpdateUserAccess,
  useUpdateRolePermissions,
} from './administrationQueries';

const administrationTabs = [
  { id: 'users' as const, text: 'Users' },
  { id: 'roles' as const, text: 'Roles' },
];

export default function AdministrationPage() {
  const user = useQuery(currentUserQueryOptions());
  if (user.isPending)
    return <PageLoading message="Checking administrator access…" />;
  if (user.error || !user.data?.isGlobalAdministrator)
    return (
      <PageError
        title="Access denied"
        message="This page is available only to global administrators."
        actionLabel="Retry"
        onAction={() => void user.refetch()}
      />
    );
  return <Administration />;
}

function Administration() {
  const pendingConfirmation = useRef<((confirmed: boolean) => void) | null>(
    null,
  );
  const [confirmOpen, setConfirmOpen] = useState(false);
  function finishConfirmation(confirmed: boolean) {
    const resolve = pendingConfirmation.current;
    pendingConfirmation.current = null;
    setConfirmOpen(false);
    resolve?.(confirmed);
  }
  const [tab, setTab] = useState<'users' | 'roles'>('users');
  const [selectedUser, setSelectedUser] = useState<AdminUser | null>(null);
  const [selectedRole, setSelectedRole] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [dirty, setDirty] = useState(false);
  const gridRef = useRef<DataGridRef<AdminUser, number>>(null);
  const catalog = useQuery(accessCatalogQueryOptions());
  const details = useQuery(userAccessQueryOptions(selectedUser?.id));
  const store = useMemo(
    () => createAdminUsersStore(appliedSearch),
    [appliedSearch],
  );
  const role = catalog.data?.roles.find((r) => Number(r.id) === selectedRole);
  async function leaveEditor() {
    if (pendingConfirmation.current) return false;
    if (dirty) {
      const confirmed = await new Promise<boolean>((resolve) => {
        pendingConfirmation.current = resolve;
        setConfirmOpen(true);
      });
      if (!confirmed) return false;
    }
    setDirty(false);
    return true;
  }
  async function selectUser(nextUser: AdminUser) {
    if (selectedUser?.id !== nextUser.id && (await leaveEditor()))
      setSelectedUser(nextUser);
  }
  async function selectRole(roleId: number) {
    if (roleId !== selectedRole && (await leaveEditor())) setSelectedRole(roleId);
  }
  async function closeUserEditor() {
    if (await leaveEditor()) setSelectedUser(null);
  }
  return (
    <main className="w-full max-w-[1600px] mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6 administration">
      <header className="flex flex-col items-stretch justify-between gap-4 md:flex-row md:items-start">
        <div>
          <h1 className="text-2xl font-semibold leading-tight">
            Users & Access
          </h1>
          <p className="mt-2 text-fg-muted">
            Permissions come only from roles, within each branch and department.
          </p>
        </div>
      </header>
      <Tabs
        items={administrationTabs}
        selectedIndex={tab === 'users' ? 0 : 1}
        onChange={(index) => {
          const nextTab = administrationTabs[index];
          if (nextTab) setTab(nextTab.id);
        }}
        devExtremeProps={{
          className: 'w-full max-w-[360px] my-4',
          keyExpr: 'id',
          elementAttr: { 'aria-label': 'Administration sections' },
          onSelectionChanging: (event) => {
            if (event.addedItems[0]?.id !== tab)
              event.cancel = leaveEditor().then((canLeave) => !canLeave);
          },
        }}
      />
      {catalog.isPending ? (
        <PageLoading message="Loading access catalog…" />
      ) : catalog.error ? (
        <PageError
          title="Unable to load access catalog"
          message={catalog.error.message}
          onAction={() => void catalog.refetch()}
          actionLabel="Retry"
        />
      ) : (
        catalog.data && (
          <>
            {tab === 'users' ? (
              <>
                <form
                  className="flex flex-col items-stretch gap-3 my-4 md:flex-row md:items-end"
                  onSubmit={(e) => {
                    e.preventDefault();
                    setAppliedSearch(search);
                  }}
                >
                  <Field
                    label="Find user"
                    id="admin-user-search"
                    className="grid gap-1 min-w-0 w-full md:min-w-[260px] md:flex-[0_1_360px]"
                  >
                    <TextInput
                      value={search}
                      placeholder="e.g. Amelia or amelia.hart…"
                      onChange={(event) => setSearch(event)}
                      devExtremeProps={{
                        height: 36,
                        maxLength: 100,
                        valueChangeEvent: 'input',
                        inputAttr: {
                          id: 'admin-user-search',
                          name: 'user-search',
                          autoComplete: 'off',
                          spellCheck: 'false',
                        },
                      }}
                    />
                  </Field>
                  <div className="admin-search-actions flex items-center gap-2 w-full md:w-auto">
                    <Button
                      submit
                      variant="primary"
                      children="Search"
                      devExtremeProps={{ height: 36 }}
                    />
                    <GridRefreshButton
                      refresh={() => gridRef.current?.instance().refresh()}
                    />
                  </div>
                </form>
                <DataGrid
                  ref={gridRef}
                  dataSource={store}
                  devExtremeProps={{
                    remoteOperations: { paging: true, sorting: true },
                    showBorders: false,
                    columnAutoWidth: true,
                    elementAttr: { 'aria-label': 'Users' },
                    noDataText: 'No users found',
                  }}
                >
                  <Paging defaultPageSize={20} />
                  <Pager visible showInfo />
                  <Column dataField="displayName" caption="Name" />
                  <Column dataField="userName" caption="Username" />
                  <Column
                    caption="Access"
                    allowSorting={false}
                    cellRender={({ data }: { data: AdminUser }) => (
                      <Button
                        onClick={() => void selectUser(data)}
                        variant="secondary"
                        children="Edit access"
                      />
                    )}
                  />
                </DataGrid>
                {selectedUser &&
                  (details.isPending ? (
                    <PageLoading message="Loading user access…" />
                  ) : details.error ? (
                    <PageError
                      title="Unable to load user access"
                      message={details.error.message}
                      onAction={() => void details.refetch()}
                      actionLabel="Retry"
                    />
                  ) : (
                    details.data && (
                      <UserEditor
                        key={selectedUser.id}
                        user={details.data}
                        catalog={catalog.data}
                        onDirty={setDirty}
                        onClose={() => void closeUserEditor()}
                      />
                    )
                  ))}
              </>
            ) : (
              <>
                <Select
                  options={catalog.data.roles ?? []}
                  optionValue="id"
                  optionLabel="name"
                  value={selectedRole}
                  placeholder="Select a role"
                  onChange={(e) => void selectRole(e as number)}
                  devExtremeProps={{ inputAttr: { 'aria-label': 'Role' } }}
                />
                {role && (
                  <RoleEditor
                    key={role.id}
                    role={role}
                    catalog={catalog.data}
                    onDirty={setDirty}
                  />
                )}
              </>
            )}
          </>
        )
      )}
      <ConfirmDialog
        open={confirmOpen}
        title="Unsaved changes"
        message="Discard unsaved access changes?"
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        onConfirm={() => finishConfirmation(true)}
        onCancel={() => finishConfirmation(false)}
      />
    </main>
  );
}

function UserEditor({
  user,
  catalog,
  onDirty,
  onClose,
}: {
  user: UserAccessDetailsDto;
  catalog: AccessCatalogDto;
  onDirty: (dirty: boolean) => void;
  onClose: () => void;
}) {
  const notify = useNotify();
  const [assignments, setAssignments] = useState<ScopedRoleAssignmentDto[]>(
    user.assignments,
  );
  const mutation = useUpdateUserAccess(Number(user.userId), {
    onSuccess: () => {
      onDirty(false);
      notify('Changes saved.', 'success', 4000);
    },
    onError: (error) => notify(error.message, 'error', 4000),
  });
  function save() {
    mutation.mutate({ assignments });
  }
  function change(next: ScopedRoleAssignmentDto[]) {
    if (mutation.isLocked()) return;
    setAssignments(next);
    onDirty(true);
    mutation.reset();
  }
  function update(index: number, patch: Partial<ScopedRoleAssignmentDto>) {
    change(assignments.map((a, i) => (i === index ? { ...a, ...patch } : a)));
  }
  return (
    <Card
      className="p-6 mt-6 [&_fieldset]:border-0 [&_fieldset]:p-0 [&_fieldset]:min-w-0"
      role="region"
      aria-label="User access editor"
    >
      <h2 className="text-xl font-semibold">{user.displayName}</h2>
      <p>{user.userName}</p>
      <fieldset disabled={mutation.isPending}>
        {assignments.map((assignment, index) => (
          <div
            className="grid grid-cols-1 md:grid-cols-[1fr_1fr_2fr_auto] gap-3 py-4 border-b border-border"
            key={index}
          >
            <Select
              options={catalog.branches ?? []}
              optionValue="id"
              optionLabel="name"
              value={assignment.branchId || null}
              disabled={mutation.isPending}
              placeholder="Branch"
              onChange={(e) => update(index, { branchId: e as number })}
              devExtremeProps={{
                inputAttr: { 'aria-label': `Branch ${index + 1}` },
              }}
            />
            <Select
              options={catalog.departments ?? []}
              optionValue="id"
              optionLabel="name"
              value={assignment.departmentId || null}
              disabled={mutation.isPending}
              placeholder="Department"
              onChange={(e) => update(index, { departmentId: e as number })}
              devExtremeProps={{
                inputAttr: { 'aria-label': `Department ${index + 1}` },
              }}
            />
            <MultiSelect
              options={catalog.roles ?? []}
              optionValue="id"
              optionLabel="name"
              value={assignment.roleIds}
              selectionControls
              disabled={mutation.isPending}
              onChange={(e) => update(index, { roleIds: e as number[] })}
              devExtremeProps={{
                inputAttr: { 'aria-label': `Roles ${index + 1}` },
              }}
            />
            <Button
              disabled={mutation.isPending}
              onClick={() => change(assignments.filter((_, i) => i !== index))}
              variant="secondary"
              children="Remove scope"
            />
            <p className="col-span-full m-0 wrap-anywhere">
              Effective permissions:{' '}
              {[
                ...new Set(
                  catalog.roles
                    .filter((r) => assignment.roleIds.includes(r.id))
                    .flatMap((r) => r.permissions),
                ),
              ]
                .sort()
                .join(', ') || 'None'}
            </p>
          </div>
        ))}
        {!assignments.length && (
          <p>No business access. Add a scope to assign roles.</p>
        )}
        <div className="flex flex-wrap items-center gap-3 my-4">
          <Button
            disabled={mutation.isPending}
            onClick={() =>
              change([
                ...assignments,
                { branchId: 0, departmentId: 0, roleIds: [] },
              ])
            }
            variant="secondary"
            children="Add scope"
          />
          <Button
            disabled={mutation.isPending}
            onClick={save}
            variant="primary"
            children={mutation.isPending ? 'Saving…' : 'Save access'}
          />
          <Button
            disabled={mutation.isPending}
            onClick={() => {
              if (!mutation.isLocked()) onClose();
            }}
            variant="secondary"
            children="Close"
          />
        </div>
      </fieldset>
      <SaveError error={mutation.error} />
    </Card>
  );
}

function RoleEditor({
  role,
  catalog,
  onDirty,
}: {
  role: RoleDetailsDto;
  catalog: AccessCatalogDto;
  onDirty: (dirty: boolean) => void;
}) {
  const notify = useNotify();
  const [permissions, setPermissions] = useState(role.permissions);
  const mutation = useUpdateRolePermissions(Number(role.id), {
    onSuccess: () => {
      onDirty(false);
      notify('Changes saved.', 'success', 4000);
    },
    onError: (error) => notify(error.message, 'error', 4000),
  });
  function save() {
    mutation.mutate({ permissions });
  }
  return (
    <Card
      className="p-6 mt-6 [&_fieldset]:border-0 [&_fieldset]:p-0 [&_fieldset]:min-w-0"
      role="region"
      aria-label="Role permissions editor"
    >
      <h2 className="text-xl font-semibold">{role.name}</h2>
      <p>Changes affect every user assigned this role, in all of its scopes.</p>
      <fieldset disabled={mutation.isPending}>
        <MultiSelect
          options={catalog.permissions ?? []}
          value={permissions}
          selectionControls
          disabled={mutation.isPending}
          onChange={(e) => {
            if (mutation.isLocked()) return;
            setPermissions(e as string[]);
            onDirty(true);
            mutation.reset();
          }}
          devExtremeProps={{ inputAttr: { 'aria-label': 'Role permissions' } }}
        />
        <div className="flex flex-wrap items-center gap-3 my-4">
          <Button
            disabled={mutation.isPending}
            onClick={save}
            variant="primary"
            children={mutation.isPending ? 'Saving…' : 'Save permissions'}
          />
          <Button
            disabled={mutation.isPending}
            onClick={() => {
              if (mutation.isLocked()) return;
              setPermissions(role.permissions);
              onDirty(false);
              mutation.reset();
            }}
            variant="secondary"
            children="Reset"
          />
        </div>
      </fieldset>
      <SaveError error={mutation.error} />
    </Card>
  );
}

function SaveError({ error }: { error: Error | null }) {
  return error ? <p role="alert">{error.message}</p> : null;
}
