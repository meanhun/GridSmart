// Bộ fetch dùng chung cho client sinh bởi orval (FE-02).
// - Gắn access token từ bộ nhớ.
// - Gặp 401: refresh một lần (nhiều request cùng lúc chia sẻ một lần refresh), rồi thử lại đúng một lần.
// - Lỗi ProblemDetails được chuyển thành ApiError có `code`.
import { toApiError } from '../errors';
import { session } from '../session';
const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';
// Các endpoint này không bao giờ thử refresh lại, tránh vòng lặp.
const NO_RETRY_PATHS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/register'];
let refreshInFlight = null;
/** Đổi refresh cookie lấy access token mới. Trả false khi phiên đã hết hạn. */
export function refreshSession() {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      try {
        const res = await fetch(`${BASE_URL}/api/auth/refresh`, { method: 'POST', credentials: 'include' });
        if (!res.ok) {
          session.clear();
          return false;
        }
        const body = await res.json();
        session.setToken(body.accessToken, body.expiresAt);
        return true;
      } catch {
        session.clear();
        return false;
      } finally {
        refreshInFlight = null;
      }
    })();
  }
  return refreshInFlight;
}
function send(url, options) {
  const headers = new Headers(options.headers);
  const token = session.getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  // FormData tự đặt Content-Type kèm boundary; JSON thì đặt rõ.
  if (options.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  return fetch(`${BASE_URL}${url}`, { ...options, headers, credentials: 'include' });
}
// Kiểu trả về khớp với type sinh bởi orval: { data, status, headers }.
export async function customInstance(url, options = {}) {
  let res = await send(url, options);
  const retryable = !NO_RETRY_PATHS.some((p) => url.startsWith(p));
  if (res.status === 401 && retryable && (await refreshSession())) {
    res = await send(url, options);
  }
  const text = res.status === 204 ? '' : await res.text();
  // Proxy hoặc máy chủ lỗi có thể trả HTML hoặc chuỗi rỗng, không được làm vỡ việc đọc JSON.
  let body = undefined;
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = undefined;
    }
  }
  if (!res.ok) throw toApiError(res, body);
  return { data: body, status: res.status, headers: res.headers };
}
export default customInstance;
