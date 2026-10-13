import { describe, expect, it } from 'vitest';
import { publicStep, statusLabel } from './incident-status';
describe('publicStep', () => {
  it('ánh xạ trạng thái backend sang ba bước công khai', () => {
    expect(publicStep('NEW')).toBe(0);
    expect(publicStep('ASSIGNED')).toBe(1);
    expect(publicStep('IN_PROGRESS')).toBe(1);
    expect(publicStep('RESOLVED')).toBe(2);
  });
  it('CANCELLED và mã lạ không thuộc thanh tiến trình', () => {
    expect(publicStep('CANCELLED')).toBeNull();
    expect(publicStep('UNKNOWN_CODE')).toBeNull();
  });
});
describe('statusLabel', () => {
  it('nhãn tiếng Việt đúng với FE-09', () => {
    expect(statusLabel('NEW')).toBe('Đã tiếp nhận');
    expect(statusLabel('in_progress')).toBe('Đang xử lý');
    expect(statusLabel('RESOLVED')).toBe('Đã hoàn thành');
    expect(statusLabel('CANCELLED')).toBe('Đã hủy');
    expect(statusLabel('???')).toBe('Không xác định');
  });
});
