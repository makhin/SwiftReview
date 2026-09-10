import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import GridStateCards from './GridStateCards';

const items = [{ value: null, label: 'All', count: 12 }, { value: 'New', label: 'New', count: 12 }, { value: 'FutureState', label: 'Future state', count: 0 }];
function Controlled() {
  const [value, setValue] = useState<string | null>(null);
  return <GridStateCards items={items} value={value} onChange={setValue} />;
}
describe('GridStateCards', () => {
  it('selects one state and supports returning to All, including zero-count and unknown states', () => {
    render(<Controlled />);
    expect(screen.getByRole('radio', { name: 'All' })).toBeChecked();
    fireEvent.click(screen.getByRole('radio', { name: 'Future state' }));
    expect(screen.getByRole('radio', { name: 'Future state' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'All' })).not.toBeChecked();
    fireEvent.click(screen.getByRole('radio', { name: 'All' }));
    expect(screen.getByRole('radio', { name: 'All' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Future state' })).not.toBeChecked();
  });
  it('distinguishes unavailable counts from zero and keeps filters usable', () => {
    const change = vi.fn();
    render(<GridStateCards items={[{ value: null, label: 'All', count: null }]} value={null} onChange={change} />);
    expect(screen.getByLabelText('Count unavailable')).toHaveTextContent('—');
    expect(screen.getByRole('radio')).toBeEnabled();
  });
  it('isolates radio groups for separate grids', () => {
    render(<><Controlled /><Controlled /></>);
    const groups = screen.getAllByRole('radio', { name: 'All' });
    expect(groups[0].getAttribute('name')).not.toBe(groups[1].getAttribute('name'));
  });
});
