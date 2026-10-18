import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  permissionSignature,
  scopeSignature,
  useEditorDraft,
} from './useEditorDraft';

describe('editor draft', () => {
  it('clears dirty after reverting and resets to the latest saved baseline', () => {
    const { result } = renderHook(() =>
      useEditorDraft(['read'], permissionSignature),
    );
    act(() => result.current.setValue(['write']));
    expect(result.current.dirty).toBe(true);
    act(() => result.current.setValue(['read']));
    expect(result.current.dirty).toBe(false);
    act(() => result.current.setValue(['write']));
    act(() => result.current.saved());
    act(() => result.current.setValue(['read']));
    act(() => result.current.reset());
    expect(result.current.value).toEqual(['write']);
    expect(result.current.dirty).toBe(false);
  });
  it('preserves local edits across background server updates', () => {
    const { result, rerender } = renderHook(
      ({ initial }) => useEditorDraft(initial, permissionSignature),
      { initialProps: { initial: ['read'] } },
    );
    act(() => result.current.setValue(['write']));
    rerender({ initial: ['read', 'audit'] });
    expect(result.current.value).toEqual(['write']);
    expect(result.current.dirty).toBe(true);
  });
  it('compares permissions and scopes without treating order as an edit', () => {
    expect(permissionSignature(['read', 'audit'])).toBe(
      permissionSignature(['audit', 'read']),
    );
    const first = { branchId: 1, departmentId: 2, roleIds: [2, 1] };
    const second = { branchId: 2, departmentId: 1, roleIds: [3] };
    expect(scopeSignature([first, second])).toBe(
      scopeSignature([second, { ...first, roleIds: [1, 2] }]),
    );
    expect(first.roleIds).toEqual([2, 1]);
  });
});
