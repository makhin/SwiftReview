import createClient from 'openapi-fetch';

import type { paths } from './schema';

type ApiPaths = Omit<paths, '/api/messages/grid'>;

export const apiClient = createClient<ApiPaths>();
