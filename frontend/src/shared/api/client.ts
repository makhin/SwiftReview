interface GetOptions {
  signal?: AbortSignal;
}

interface ApiResponse<T> {
  data?: T;
  response: Response;
}

async function get<T>(path: string, options: GetOptions = {}): Promise<ApiResponse<T>> {
  const response = await fetch(path, { signal: options.signal });
  const data = response.ok && response.status !== 204
    ? (await response.json()) as T
    : undefined;

  return { data, response };
}

export const apiClient = { GET: get };
