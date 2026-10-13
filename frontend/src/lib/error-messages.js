// Ánh xạ mã lỗi backend sang câu tiếng Việt (FE-05, mục 8.3 của tài liệu backend).
import { isApiError } from '@/api/errors';
import { vi } from '@/locales/vi';
export function messageForCode(code, fallback) {
  if (code && code in vi.errors) return vi.errors[code];
  return fallback ?? vi.errors.UNKNOWN;
}
/** Câu thông báo cho một lỗi bất kỳ. Không bao giờ trả chuỗi kỹ thuật. */
/** Mã HTTP do proxy hoặc máy chủ trả khi backend không tới được (chưa chạy, sai cổng). */
const GATEWAY_STATUSES = [0, 502, 503, 504];
export function errorMessage(err) {
  if (isApiError(err)) {
    if (GATEWAY_STATUSES.includes(err.status)) return vi.errors.NETWORK;
    if (err.code === 'RATE_LIMITED' && err.retryAfterSeconds) {
      return `${vi.errors.RATE_LIMITED} Thử lại sau ${formatWait(err.retryAfterSeconds)}.`;
    }
    return messageForCode(err.code);
  }
  if (err instanceof TypeError) return vi.errors.NETWORK;
  return vi.errors.UNKNOWN;
}
export function formatWait(seconds) {
  if (seconds < 60) return `${seconds} giây`;
  return `${Math.ceil(seconds / 60)} phút`;
}
