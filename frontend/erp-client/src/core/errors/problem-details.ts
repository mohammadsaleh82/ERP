export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly code: string;
  public readonly problemDetails?: ProblemDetails;

  constructor(status: number, message: string, code: string = 'Api.Error', problemDetails?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.problemDetails = problemDetails;
  }
}
