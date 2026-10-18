import { useState } from 'react';
import type { ScopedRoleAssignmentDto } from '../../shared/api/generated/contracts.generated';

export type EditorHandle = { isDirty: () => boolean; isSaving: () => boolean };
export const permissionSignature = (permissions: readonly string[]) =>
  JSON.stringify([...permissions].sort());
export const scopeSignature = (scopes: readonly ScopedRoleAssignmentDto[]) =>
  JSON.stringify(
    scopes
      .map((scope) => ({
        branchId: scope.branchId,
        departmentId: scope.departmentId,
        roleIds: [...scope.roleIds].sort((a, b) => a - b),
      }))
      .sort(
        (a, b) => a.branchId - b.branchId || a.departmentId - b.departmentId,
      ),
  );

// Each keyed editor owns its draft and baseline, including after background refetches.
export function useEditorDraft<T>(initial: T, signature: (value: T) => string) {
  const [value, setValue] = useState(initial);
  const [baseline, setBaseline] = useState(initial);
  return {
    value,
    setValue,
    dirty: signature(value) !== signature(baseline),
    saved: () => setBaseline(value),
    reset: () => setValue(baseline),
  };
}
