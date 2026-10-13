// Phản ánh đã gửi lưu trên máy (FE-08, FE-09). Mọi thao tác localStorage đều bọc try/catch
// vì trình duyệt có thể chặn hoặc hết bộ nhớ. Không lưu token hay thông tin nhạy cảm khác.
const KEY = 'gridsmart.myReports';
const MAX_ITEMS = 50;
export function readLocalReports() {
  try {
    const raw = window.localStorage.getItem(KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}
export function saveLocalReport(report) {
  try {
    const rest = readLocalReports().filter((r) => r.trackingCode !== report.trackingCode);
    window.localStorage.setItem(KEY, JSON.stringify([report, ...rest].slice(0, MAX_ITEMS)));
  } catch {
    // Bộ nhớ đầy hoặc bị chặn: bỏ qua, người dùng vẫn có mã tra cứu trên màn hình.
  }
}
export function findLocalReport(trackingCode) {
  const code = trackingCode.toUpperCase();
  return readLocalReports().find((r) => r.trackingCode.toUpperCase() === code);
}
