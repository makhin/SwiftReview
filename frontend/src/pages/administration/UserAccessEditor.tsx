import { Button, Card, Field, Select, MultiSelect } from '@smbc/ui';
import { useImperativeHandle, type Ref } from 'react';
import type {
  ScopedRoleAssignmentDto,
  AccessCatalogDto,
  UserAccessDetailsDto,
} from '../../shared/api/generated/contracts.generated';
import { useNotify } from '../../shared/notifications/notifications';
import { useUpdateUserAccess } from './administrationQueries';
import {
  useEditorDraft,
  scopeSignature,
  type EditorHandle,
} from './useEditorDraft';

export default function UserAccessEditor({
  user,
  catalog,
  ref,
  onClose,
}: {
  user: UserAccessDetailsDto;
  catalog: AccessCatalogDto;
  ref: Ref<EditorHandle>;
  onClose: () => void;
}) {
  const notify = useNotify();
  const draft = useEditorDraft(user.assignments, scopeSignature);
  const assignments = draft.value;
  const setAssignments = draft.setValue;
  const mutation = useUpdateUserAccess(Number(user.userId), {
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
    if (draft.dirty && !mutation.isLocked()) mutation.mutate({ assignments });
  }
  function change(next: ScopedRoleAssignmentDto[]) {
    if (mutation.isLocked()) return;
    setAssignments(next);
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
            <Field label={`Branch ${index + 1}`} id={`scope-branches-${index}`}>
              <Select
                options={catalog.branches ?? []}
                optionValue="id"
                optionLabel="name"
                value={assignment.branchId || null}
                disabled={mutation.isPending}
                placeholder="Branch"
                onChange={(e) => update(index, { branchId: e as number })}
                devExtremeProps={{
                  inputAttr: {
                    id: `scope-branches-${index}`,
                    'aria-label': `Branch ${index + 1}`,
                  },
                }}
              />
            </Field>
            <Field
              label={`Department ${index + 1}`}
              id={`scope-departments-${index}`}
            >
              <Select
                options={catalog.departments ?? []}
                optionValue="id"
                optionLabel="name"
                value={assignment.departmentId || null}
                disabled={mutation.isPending}
                placeholder="Department"
                onChange={(e) => update(index, { departmentId: e as number })}
                devExtremeProps={{
                  inputAttr: {
                    id: `scope-departments-${index}`,
                    'aria-label': `Department ${index + 1}`,
                  },
                }}
              />
            </Field>
            <Field label={`Roles ${index + 1}`} id={`scope-roles-${index}`}>
              <MultiSelect
                options={catalog.roles ?? []}
                optionValue="id"
                optionLabel="name"
                value={assignment.roleIds}
                selectionControls
                disabled={mutation.isPending}
                onChange={(e) => update(index, { roleIds: e as number[] })}
                devExtremeProps={{
                  inputAttr: {
                    id: `scope-roles-${index}`,
                    'aria-label': `Roles ${index + 1}`,
                  },
                }}
              />
            </Field>
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
            disabled={mutation.isPending || !draft.dirty}
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
