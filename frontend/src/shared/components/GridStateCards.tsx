import RadioGroup from 'devextreme-react/radio-group';
import { useId, type CSSProperties } from 'react';
import './grid-state-cards.css';

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

export default function GridStateCards({ items, value, onChange, label = 'Filter by state' }: Props) {
  const id = useId();
  const selectableItems = items.map((item, selectionKey) => ({ ...item, selectionKey }));
  const selectedKey = selectableItems.find((item) => item.value === value)?.selectionKey ?? null;
  return <fieldset className="grid-state-cards">
    <legend id={`${id}-label`}>{label}</legend>
    <RadioGroup className="grid-state-cards__control" items={selectableItems} valueExpr="selectionKey" displayExpr="label"
      value={selectedKey} name={id} layout="horizontal" elementAttr={{ 'aria-labelledby': `${id}-label` }}
      onValueChanged={(event) => { const item = selectableItems[event.value as number]; if (item) onChange(item.value); }}
      itemRender={(item: GridStateCard & { selectionKey: number }) =>
        <span className={`grid-state-card__body${item.selectionKey === selectedKey ? ' grid-state-card__body--selected' : ''}`}
          style={{ '--card-surface': item.surface, '--card-accent': item.accent } as CSSProperties}>
          <span className="grid-state-card__label">{item.label}</span>
          <span className="grid-state-card__count" aria-label={item.count === null ? 'Count unavailable' : `${item.count} messages`}>
            {item.count === null ? '—' : item.count.toLocaleString()}
          </span>
        </span>} />
  </fieldset>;
}
