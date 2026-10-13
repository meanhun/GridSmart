// Nhãn hiển thị cho cổng điều hành: trạng thái, mức ưu tiên, lý do chờ duyệt, màu ô lưới.
// Mã lấy từ backend (IncidentText, enum ReviewReason, vw_grid_status). Không dùng chuỗi kỹ thuật trên giao diện.
export const STATUS_LABEL = {
  NEW: 'Mới',
  ASSIGNED: 'Đã giao',
  IN_PROGRESS: 'Đang xử lý',
  RESOLVED: 'Đã xử lý',
  CANCELLED: 'Đã hủy',
};
export const OPEN_STATUSES = ['NEW', 'ASSIGNED', 'IN_PROGRESS'];
export function isOpenStatus(status) {
  return OPEN_STATUSES.includes(status.toUpperCase());
}
export function statusText(status) {
  return STATUS_LABEL[status.toUpperCase()] ?? status;
}
/** Priority: 1 thấp, 2 trung bình, 3 khẩn (backend enum Priority). */
export const PRIORITY_OPTIONS = [
  { value: 1, label: 'Thấp' },
  { value: 2, label: 'Trung bình' },
  { value: 3, label: 'Khẩn' },
];
export function priorityText(priority) {
  const n = Number(priority);
  return PRIORITY_OPTIONS.find((p) => p.value === n)?.label ?? '—';
}
export const REVIEW_REASON_LABEL = {
  AI_LOW_CONFIDENCE: 'AI không nhận diện chắc chắn',
  NO_GRID: 'Ngoài ô lưới',
  NO_OFFICER: 'Ô lưới chưa có cán bộ',
  RETURNED: 'Cán bộ trả lại',
  PIPELINE_ERROR: 'Lỗi xử lý nền',
};
export const REVIEW_REASON_ORDER = ['AI_LOW_CONFIDENCE', 'NO_GRID', 'NO_OFFICER', 'RETURNED', 'PIPELINE_ERROR'];
export function reviewReasonText(reason) {
  if (!reason) return 'Khác';
  return REVIEW_REASON_LABEL[reason.toUpperCase()] ?? 'Khác';
}
/** Màu ô lưới: vw_grid_status trả RED, YELLOW hoặc GREEN. Luôn đi kèm chữ (FE-26). */
export const GRID_COLOR = {
  RED: { fill: '#b91c1c', label: 'Quá hạn' },
  YELLOW: { fill: '#f59e0b', label: 'Đang có việc' },
  GREEN: { fill: '#21A691', label: 'Bình thường' },
};
export function gridColor(statusColor) {
  return GRID_COLOR[(statusColor ?? '').toUpperCase()] ?? { fill: '#27403E', label: 'Không rõ' };
}
