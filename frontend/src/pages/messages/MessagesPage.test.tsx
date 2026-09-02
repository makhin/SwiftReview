import type { PropsWithChildren } from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { componentProps } = vi.hoisted(() => ({ componentProps: vi.fn() }));

vi.mock('devextreme-react/data-grid', () => {
  const childComponent = (name: string) => (props: Record<string, unknown>) => {
    componentProps(name, props);
    return <span data-testid={name}>{String(props.caption ?? name)}</span>;
  };

  return {
    default: ({ children, ...props }: PropsWithChildren<Record<string, unknown>>) => {
      componentProps('DataGrid', props);
      return <div aria-label="Messages">{children}</div>;
    },
    Column: childComponent('Column'),
    FilterRow: childComponent('FilterRow'),
    HeaderFilter: childComponent('HeaderFilter'),
    Pager: childComponent('Pager'),
    Paging: childComponent('Paging'),
  };
});

import MessagesPage from './MessagesPage';

describe('MessagesPage', () => {
  it('configures the remote messages grid', () => {
    render(<MessagesPage />);

    expect(screen.getByRole('heading', { name: 'Messages' })).toBeInTheDocument();
    expect(screen.getByLabelText('Messages')).toBeInTheDocument();
    expect(screen.getAllByTestId('Column')).toHaveLength(10);

    const dataGridProps = componentProps.mock.calls.find(([name]) => name === 'DataGrid')?.[1];
    expect(dataGridProps).toMatchObject({
      remoteOperations: true,
      rowAlternationEnabled: true,
      noDataText: 'No messages found',
    });

    const pagingProps = componentProps.mock.calls.find(([name]) => name === 'Paging')?.[1];
    expect(pagingProps).toMatchObject({ defaultPageSize: 20 });

    const captions = componentProps.mock.calls
      .filter(([name]) => name === 'Column')
      .map(([, props]) => props.caption);
    expect(captions).toEqual([
      'External ID',
      'Message type',
      'Branch',
      'Department',
      'State',
      'Received',
      'Account',
      'CCY',
      'Amount',
      'Assignee',
    ]);
  });
});
