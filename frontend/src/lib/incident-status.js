// Trạng thái hiển thị cho công dân (FE-09): Đã tiếp nhận → Đang xử lý → Đã hoàn thành, hoặc Đã hủy.
// Nhãn lấy từ mã trạng thái backend, không dùng statusText của server.
export const PUBLIC_STEPS = ['Đã tiếp nhận', 'Đang xử lý', 'Đã hoàn thành'];
export function publicStep(status) {
  switch (status.toUpperCase()) {
    case 'NEW':
      return 0;
    case 'ASSIGNED':
    case 'IN_PROGRESS':
      return 1;
    case 'RESOLVED':
      return 2;
    default:
      return null;
  }
}
export function statusLabel(status) {
  switch (status.toUpperCase()) {
    case 'CANCELLED':
      return 'Đã hủy';
    default: {
      const step = publicStep(status);
      return step === null ? 'Không xác định' : PUBLIC_STEPS[step];
    }
  }
}
