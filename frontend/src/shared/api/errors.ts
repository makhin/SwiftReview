export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  [extension: string]: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(
    message: string,
    status: number,
    problem?: ProblemDetails,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export class ApiRequestError extends Error {
  readonly kind: 'network' | 'aborted' | 'invalid-response';
  readonly outcomeUnknown: boolean;
  readonly status?: number;

  constructor(
    message: string,
    kind: ApiRequestError['kind'],
    options: ErrorOptions & { outcomeUnknown?: boolean; status?: number } = {},
  ) {
    super(message, options);
    this.kind = kind;
    this.name = kind === 'aborted' ? 'AbortError' : 'ApiRequestError';
    this.outcomeUnknown = options.outcomeUnknown ?? false;
    this.status = options.status;
  }
}
