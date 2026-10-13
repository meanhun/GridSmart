// Chuyển ProblemDetails của backend thành lỗi có mã nghiệp vụ (FE-02, FE-05).
export class ApiError extends Error {
  status;
  code;
  retryAfterSeconds;
  constructor(status, code, message, retryAfterSeconds) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}
function parseRetryAfter(value) {
  if (!value) return undefined;
  const seconds = Number(value);
  if (Number.isFinite(seconds)) return seconds;
  const date = Date.parse(value);
  if (Number.isNaN(date)) return undefined;
  return Math.max(0, Math.ceil((date - Date.now()) / 1000));
}
export function toApiError(res, body) {
  const problem = body ?? {};
  return new ApiError(
    res.status,
    problem.code,
    problem.detail ?? problem.title ?? res.statusText,
    parseRetryAfter(res.headers.get('Retry-After')),
  );
}
export function isApiError(err) {
  return err instanceof ApiError;
}
