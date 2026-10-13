// Access token chỉ giữ trong bộ nhớ (FE-03, checklist: không lưu trong localStorage/sessionStorage).
// Refresh token nằm trong cookie httpOnly do backend đặt, frontend không đọc.
let accessToken = null;
let expiresAtMs = null;
const listeners = new Set();
function emit() {
  listeners.forEach((l) => l());
}
export const session = {
  getToken: () => accessToken,
  getExpiresAtMs: () => expiresAtMs,
  setToken(token, expiresAt) {
    accessToken = token;
    expiresAtMs = Date.parse(expiresAt);
    emit();
  },
  clear() {
    accessToken = null;
    expiresAtMs = null;
    emit();
  },
  subscribe(listener) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
