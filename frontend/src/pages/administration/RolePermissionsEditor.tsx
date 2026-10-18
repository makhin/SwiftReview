import { Button, Card, Field, MultiSelect } from '@smbc/ui';
import { useImperativeHandle, type Ref } from 'react';
import type {
  AccessCatalogDto,
  RoleDetailsDto,
} from '../../shared/api/generated/contracts.generated';
import { useNotify } from '../../shared/notifications/notifications';
import { useUpdateRolePermissions } from './administrationQueries';
import {
  useEditorDraft,
  permissionSignature,
  type EditorHandle,
} from './useEditorDraft';

export default function RolePermissionsEditor({
  role,
  catalog,
  ref,
}: {
  role: RoleDetailsDto;
  catalog: AccessCatalogDto;
  ref: Ref<EditorHandle>;
}) {
  const notify = useNotify();
  const draft = useEditorDraft(role.permissions, permissionSignature);
  const permissions = draft.value;
  const setPermissions = draft.setValue;
  const mutation = useUpdateRolePermissions(Number(role.id), {
    onSuccess: () => {
      draft.saved();
      notify('Changes saved.', 'success', 4000);
    },
    onError: (error) => notify(error.message, 'error', 4000),
  });
  useImperativeHandle(ref, () => ({
    isDirty: () => draft.dirty,
    isSaving: mutation.isLocked,
  }));
  function save() {
    if (draft.dirty && !mutation.isLocked()) mutation.mutate({ permissions });
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
        <Field label="Role permissions" id="role-permissions">
          <MultiSelect
            options={catalog.permissions ?? []}
            value={permissions}
            selectionControls
            disabled={mutation.isPending}
            onChange={(e) => {
              if (mutation.isLocked()) return;
              setPermissions(e as string[]);
              mutation.reset();
            }}
            devExtremeProps={{
              inputAttr: {
                id: 'role-permissions',
                'aria-label': 'Role permissions',
              },
            }}
          />
        </Field>
        <div className="flex flex-wrap items-center gap-3 my-4">
          <Button
            disabled={mutation.isPending || !draft.dirty}
            onClick={save}
            variant="primary"
            children={mutation.isPending ? 'Saving…' : 'Save permissions'}
          />
          <Button
            disabled={mutation.isPending}
            onClick={() => {
              if (mutation.isLocked()) return;
              draft.reset();
              mutation.reset();
            }}
            variant="secondary"
            children="Reset"
          />
        </div>
      </fieldset>
      <p role="status" className="text-sm text-fg-muted">
        {mutation.isPending
          ? 'Saving changes…'
          : draft.dirty
            ? 'Unsaved changes'
            : 'All changes saved'}
      </p>
      {mutation.error && <p role="alert">{mutation.error.message}</p>}
    </Card>
  );
}
