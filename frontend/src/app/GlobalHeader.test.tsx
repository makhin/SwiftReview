import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import GlobalHeader from './GlobalHeader';

describe('GlobalHeader', () => {
  it('opens and closes the navigation menu', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <GlobalHeader />
      </MemoryRouter>,
    );

    const menuButton = screen.getByRole('button', { name: 'Open navigation' });

    expect(menuButton).toHaveAttribute('aria-expanded', 'false');

    await user.click(menuButton);

    expect(screen.getByRole('button', { name: 'Close navigation' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );

    await user.click(screen.getByRole('link', { name: 'Messages' }));

    expect(screen.getByRole('button', { name: 'Open navigation' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });
});
