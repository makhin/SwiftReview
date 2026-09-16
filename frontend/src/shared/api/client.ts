import { ApiError, ApiRequestError, type ProblemDetails } from './errors';

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  signal?: AbortSignal;
  headers?: HeadersInit;
  errorMessage: string;
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
  return storedUser || configuredUser || 'admin';
}

function parseProblem(value: unknown): ProblemDetails | undefined {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return undefined;
  const problem: ProblemDetails = { ...value };
  for (const key of ['type', 'title', 'detail', 'instance'] as const) {
    if (typeof problem[key] !== 'string') delete problem[key];
  }
  if (typeof problem.status !== 'number') delete problem.status;
  return problem;
}

function isAbortError(cause: unknown): boolean {
  return typeof cause === 'object' && cause !== null && 'name' in cause && cause.name === 'AbortError';
}

export function apiRequest(path: string, options: RequestOptions & { responseType: 'none' }): Promise<void>;
export function apiRequest<T>(path: string, options: RequestOptions & { responseType?: 'json' }): Promise<T>;
export async function apiRequest<T>(
  path: string,
  options: RequestOptions & { responseType?: 'json' | 'none' },
): Promise<T | void> {
  const { method = 'GET', signal, errorMessage } = options;
  let sent = false;
  const requestError = (kind: ApiRequestError['kind'], cause?: unknown, status?: number) => {
    const outcomeUnknown = sent && method !== 'GET';
    const message = outcomeUnknown
      ? `${errorMessage}. The result is unknown. Refresh the data and check the current state before trying again.`
      : kind === 'aborted' ? 'Request cancelled.'
        : kind === 'invalid-response'
          ? `${errorMessage}. The server returned an invalid or empty response.`
          : `${errorMessage}.`;
    return new ApiRequestError(message, kind, { cause, status, outcomeUnknown });
  };
  if (signal?.aborted) throw requestError('aborted', signal.reason);

  const headers = new Headers(options.headers);
  headers.set('Accept', 'application/json, application/problem+json');
  const body = options.body === undefined ? undefined : JSON.stringify(options.body);
  if (body !== undefined) headers.set('Content-Type', 'application/json');
  const debugUser = getDebugUser();
  if (debugUser) headers.set('X-Debug-User', debugUser);

  let response: Response;
  try {
    sent = true;
    response = await fetch(path, { method, headers, body, signal });
  } catch (cause) {
    throw requestError(signal?.aborted || isAbortError(cause)
      ? 'aborted' : 'network', cause);
  }

  if (!response.ok) {
    // A broken error body must not hide an already received HTTP status.
    const problem = await response.json().then(parseProblem).catch(() => undefined);
    throw new ApiError(
      problem?.detail?.trim() || problem?.title?.trim() || `${errorMessage} (${response.status}).`,
      response.status,
      problem,
    );
  }
  if (options.responseType === 'none') return;

  let text: string;
  try {
    text = await response.text();
  } catch (cause) {
    throw requestError(signal?.aborted || isAbortError(cause)
      ? 'aborted' : 'network', cause, response.status);
  }
  try {
    return JSON.parse(text) as T;
  } catch (cause) {
    throw requestError('invalid-response', cause, response.status);
  }
}
