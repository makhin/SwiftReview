import { RadioGroup, Select } from '@smbc/ui';
import { useId, type CSSProperties } from 'react';

export type GridStateCard = {
  value: string | null;
  label: string;
  count: number | null;
  surface?: string;
  accent?: string;
};

type Props = {
  items: GridStateCard[];
  value: string | null;
  onChange: (value: string | null) => void;
  label?: string;
};

export default function GridStateCards({
  items,
  value,
  onChange,
  label = 'Filter by state',
}: Props) {
  const id = useId();
  const selectableItems = items.map((item, selectionKey) => ({
    ...item,
    selectionKey,
  }));
  const selectedKey =
    selectableItems.find((item) => item.value === value)?.selectionKey ?? null;
  return (
    <fieldset className="grid-state-cards">
      <legend id={`${id}-label`}>{label}</legend>
      <div className="md:hidden">
        <Select
          options={selectableItems.map((item) => ({
            ...item,
            text: `${item.label} (${item.count ?? '—'})`,
          }))}
          optionValue="selectionKey"
          optionLabel="text"
          value={selectedKey}
          onChange={(key) => {
            const item = selectableItems[key as number];
            if (item) onChange(item.value);
          }}
          devExtremeProps={{ inputAttr: { 'aria-label': label } }}
        />
      </div>
      <div className="hidden md:block">
        <RadioGroup
          options={selectableItems}
          optionValue="selectionKey"
          optionLabel="label"
          value={selectedKey}
          orientation="horizontal"
          onChange={(event) => {
            const item = selectableItems[event as number];
            if (item) onChange(item.value);
          }}
          devExtremeProps={{
            className: 'grid-state-cards__control',
            name: id,
            elementAttr: { 'aria-labelledby': `${id}-label` },
            itemRender: (item: GridStateCard & { selectionKey: number }) => (
              <span
                className={`grid-state-card__body${item.selectionKey === selectedKey ? ' grid-state-card__body--selected' : ''}`}
                style={
                  {
                    '--card-surface': item.surface,
                    '--card-accent': item.accent,
                  } as CSSProperties
                }
              >
                <span className="grid-state-card__label">{item.label}</span>
                <span
                  className="grid-state-card__count"
                  aria-label={
                    item.count === null
                      ? 'Count unavailable'
                      : `${item.count} messages`
                  }
                >
                  {item.count === null ? '—' : item.count.toLocaleString()}
                </span>
              </span>
            ),
          }}
        />
      </div>
    </fieldset>
  );
}
