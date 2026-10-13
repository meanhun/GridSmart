// Giờ: API trả UTC, giao diện hiển thị giờ Việt Nam dạng HH:mm dd/MM/yyyy (quy ước chung).
export const APP_TIME_ZONE = 'Asia/Ho_Chi_Minh';
const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  timeZone: APP_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hourCycle: 'h23',
});
/** Chuỗi "HH:mm dd/MM/yyyy" theo giờ Việt Nam. */
export function formatDateTime(value) {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const parts = Object.fromEntries(dateTimeFormatter.formatToParts(date).map((p) => [p.type, p.value]));
  return `${parts.hour}:${parts.minute} ${parts.day}/${parts.month}/${parts.year}`;
}
/** Đếm ngược SLA: trả về phần trăm thời hạn còn lại (có thể âm khi quá hạn). */
export function slaRemainingRatio(createdAt, deadline, now = new Date()) {
  const start = Date.parse(createdAt);
  const end = Date.parse(deadline);
  const total = end - start;
  if (total <= 0) return 0;
  return (end - now.getTime()) / total;
}
/** Màu đồng hồ SLA theo mục FE-12: xanh trên 20 %, vàng dưới 20 %, đỏ khi quá hạn. */
export function slaTone(ratio) {
  if (ratio < 0) return 'overdue';
  if (ratio < 0.2) return 'warning';
  return 'ok';
}
