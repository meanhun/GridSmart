// Tải file nhị phân (xuất Excel/CSV). customInstance chỉ đọc JSON nên dùng fetch có gắn token phiên.
import { refreshSession } from '@/api/mutator/customInstance';
import { session } from '@/api/session';
const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';
async function fetchWithAuth(url) {
  const send = () =>
    fetch(`${BASE_URL}${url}`, {
      credentials: 'include',
      headers: session.getToken() ? { Authorization: `Bearer ${session.getToken()}` } : {},
    });
  let res = await send();
  if (res.status === 401 && (await refreshSession())) res = await send();
  return res;
}
/** Tải file từ API và kích hoạt hộp thoại lưu của trình duyệt. */
export async function downloadFile(url, fallbackName) {
  const res = await fetchWithAuth(url);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const blob = await res.blob();
  const disposition = res.headers.get('Content-Disposition') ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}
