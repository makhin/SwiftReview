import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

vi.mock('devextreme-react/load-indicator', () => ({ default: () => null }));

afterEach(cleanup);
