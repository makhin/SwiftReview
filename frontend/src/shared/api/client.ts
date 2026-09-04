interface GetOptions {
  signal?: AbortSignal;
}

interface ApiResponse<T> {
  data?: T;
  response: Response;
}

const debugUserStorageKey = 'orp.debugUser';

function getDebugUser() {
  if (!import.meta.env.DEV) {
    return undefined;
  }

  const urlUser = new URLSearchParams(window.location.search).get('user')?.trim();

  if (urlUser) {
    window.sessionStorage.setItem(debugUserStorageKey, urlUser);
    return urlUser;
  }

  const storedUser = window.sessionStorage.getItem(debugUserStorageKey)?.trim();
  const configuredUser = import.meta.env.VITE_DEBUG_USER?.trim();
  return storedUser || configuredUser || 'supervisor';
}

export function apiFetch(input: RequestInfo | URL, init: RequestInit = {}) {
  const debugUser = getDebugUser();

  if (!debugUser) {
    return fetch(input, init);
  }

  const headers = new Headers(init.headers);
  headers.set('X-Debug-User', debugUser);
  return fetch(input, { ...init, headers });
}

async function get<T>(path: string, options: GetOptions = {}): Promise<ApiResponse<T>> {
  const response = await apiFetch(path, { signal: options.signal });
  const data = response.ok && response.status !== 204
    ? (await response.json()) as T
    : undefined;

  return { data, response };
}

export const apiClient = { GET: get };
