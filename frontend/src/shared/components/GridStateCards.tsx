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
  const name = useId();
  return <fieldset className="grid-state-cards">
    <legend>{label}</legend>
    <div className="grid-state-cards__strip">
      {items.map((item) => <label key={item.value ?? 'all'} className="grid-state-card"
        style={{ '--card-surface': item.surface, '--card-accent': item.accent } as CSSProperties}>
        <input type="radio" name={name} value={item.value ?? ''} checked={value === item.value}
          onChange={() => onChange(item.value)} aria-label={item.label} aria-describedby={`${name}-${item.value ?? 'all'}-count`} />
        <span className="grid-state-card__body">
          <span className="grid-state-card__label">{item.label}</span>
          <span className="grid-state-card__count" id={`${name}-${item.value ?? 'all'}-count`} aria-label={item.count === null ? 'Count unavailable' : `${item.count} messages`}>
            {item.count === null ? '—' : item.count.toLocaleString()}
          </span>
        </span>
      </label>)}
    </div>
  </fieldset>;
}
