// Lightweight controls for behaviour tests; production browser tests exercise the installed packages.
import type { HTMLAttributes, PropsWithChildren } from 'react';
function Box(props: HTMLAttributes<HTMLDivElement>) {
  return <div {...props} />;
}
export const Card = Object.assign(Box, {
  Header: Box,
  Body: Box,
  Footer: Box,
  Title: Box,
});
export const TableShell = Box;
export const FilterPanel = Box;
export const EmptyState = Box;
export function Callout({
  tone,
  ...props
}: HTMLAttributes<HTMLDivElement> & { tone?: string }) {
  return <div {...props} data-tone={tone} />;
}
export function LoadingIndicator() {
  return null;
}
export function Toast() {
  return null;
}
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return open ? (
    <section role="dialog" aria-label={title}>
      <p>{message}</p>
      <button onClick={onConfirm}>{confirmLabel}</button>
      <button onClick={onCancel}>{cancelLabel}</button>
    </section>
  ) : null;
}
export function Field({
  children,
  label,
  id,
}: PropsWithChildren<{ label: string; id?: string }>) {
  return (
    <div>
      <label htmlFor={id}>{label}</label>
      {children}
    </div>
  );
}

export function Select({
  options,
  value,
  optionValue,
  optionLabel,
  onChange,
  devExtremeProps,
}: {
  options: Record<string, unknown>[];
  value: unknown;
  optionValue: string;
  optionLabel: string;
  onChange: (value: unknown) => void;
  devExtremeProps?: { inputAttr?: HTMLAttributes<HTMLSelectElement> };
}) {
  return (
    <select
      {...devExtremeProps?.inputAttr}
      value={String(value ?? '')}
      onChange={(event) =>
        onChange(
          options.find(
            (option) => String(option[optionValue]) === event.target.value,
          )?.[optionValue],
        )
      }
    >
      {options.map((option) => (
        <option
          key={String(option[optionValue])}
          value={String(option[optionValue])}
        >
          {String(option[optionLabel])}
        </option>
      ))}
    </select>
  );
}
