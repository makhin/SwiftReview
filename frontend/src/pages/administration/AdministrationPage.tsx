import PageHeader from '../../shared/components/PageHeader';
import PageLayout from '../../shared/components/PageLayout';
import {
  Field,
  DataGrid,
  Select,
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

import UserAccessEditor from './UserAccessEditor';
import RolePermissionsEditor from './RolePermissionsEditor';
import { type EditorHandle } from './useEditorDraft';
import { useEditorNavigation } from './useEditorNavigation';
import { currentUserQueryOptions } from '../../shared/api/currentUserQueries';
import PageLoading from '../../shared/components/feedback/PageLoading';
import PageError from '../../shared/components/feedback/PageError';
import GridRefreshButton from '../../shared/components/GridRefreshButton';
import { createAdminUsersStore, type AdminUser } from './administrationApi';
import {
  accessCatalogQueryOptions,
  userAccessQueryOptions,
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
  const editorRef = useRef<EditorHandle>(null);
  const { confirmOpen, finishConfirmation, leaveEditor } =
    useEditorNavigation(editorRef);
  const [tab, setTab] = useState<'users' | 'roles'>('users');
  const [selectedUser, setSelectedUser] = useState<AdminUser | null>(null);
  const [selectedRole, setSelectedRole] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const gridRef = useRef<DataGridRef<AdminUser, number>>(null);
  const catalog = useQuery(accessCatalogQueryOptions());
  const details = useQuery(userAccessQueryOptions(selectedUser?.id));
  const store = useMemo(
    () => createAdminUsersStore(appliedSearch),
    [appliedSearch],
  );
  const role = catalog.data?.roles.find((r) => Number(r.id) === selectedRole);
  async function selectUser(nextUser: AdminUser) {
    if (selectedUser?.id !== nextUser.id && (await leaveEditor()))
      setSelectedUser(nextUser);
  }
  async function selectRole(roleId: number) {
    if (roleId !== selectedRole && (await leaveEditor()))
      setSelectedRole(roleId);
  }
  async function closeUserEditor() {
    if (await leaveEditor()) setSelectedUser(null);
  }
  return (
    <PageLayout className="administration">
      <PageHeader
        title="Users & Access"
        description="Permissions come only from roles, within each branch and department."
      />
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
      ) : catalog.error && !catalog.data ? (
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
                  ) : details.error && !details.data ? (
                    <PageError
                      title="Unable to load user access"
                      message={details.error.message}
                      onAction={() => void details.refetch()}
                      actionLabel="Retry"
                    />
                  ) : (
                    details.data && (
                      <UserAccessEditor
                        key={selectedUser.id}
                        user={details.data}
                        catalog={catalog.data}
                        ref={editorRef}
                        onClose={() => void closeUserEditor()}
                      />
                    )
                  ))}
              </>
            ) : (
              <>
                <Field label="Role" id="admin-role">
                  <Select
                    options={catalog.data.roles ?? []}
                    optionValue="id"
                    optionLabel="name"
                    value={selectedRole}
                    placeholder="Select a role"
                    onChange={(e) => void selectRole(e as number)}
                    devExtremeProps={{
                      inputAttr: { id: 'admin-role', 'aria-label': 'Role' },
                    }}
                  />
                </Field>
                {role && (
                  <RolePermissionsEditor
                    key={role.id}
                    role={role}
                    catalog={catalog.data}
                    ref={editorRef}
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
    </PageLayout>
  );
}
