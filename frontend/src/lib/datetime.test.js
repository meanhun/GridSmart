import { describe, expect, it } from 'vitest';
import { formatDateTime, slaRemainingRatio, slaTone } from './datetime';
describe('formatDateTime', () => {
  it('đổi UTC sang giờ Việt Nam dạng HH:mm dd/MM/yyyy', () => {
    // 2026-10-10 02:30 UTC = 09:30 ngày 10/10/2026 giờ Việt Nam (UTC+7)
    expect(formatDateTime('2026-10-10T02:30:00Z')).toBe('09:30 10/10/2026');
  });
  it('giờ sau nửa đêm UTC vẫn đúng ngày Việt Nam', () => {
    // 2026-10-09 18:05 UTC = 01:05 ngày 10/10/2026
    expect(formatDateTime('2026-10-09T18:05:00Z')).toBe('01:05 10/10/2026');
  });
  it('chuỗi rỗng hoặc không hợp lệ trả chuỗi rỗng', () => {
    expect(formatDateTime(null)).toBe('');
    expect(formatDateTime('không phải ngày')).toBe('');
  });
});
describe('SLA', () => {
  const created = '2026-10-10T00:00:00Z';
  const deadline = '2026-10-10T10:00:00Z';
  it('tỷ lệ còn lại theo thời hạn', () => {
    expect(slaRemainingRatio(created, deadline, new Date('2026-10-10T08:00:00Z'))).toBeCloseTo(0.2);
    expect(slaRemainingRatio(created, deadline, new Date('2026-10-10T00:00:00Z'))).toBe(1);
  });
  it('quá hạn thì tỷ lệ âm', () => {
    expect(slaRemainingRatio(created, deadline, new Date('2026-10-10T11:00:00Z'))).toBeLessThan(0);
  });
  it('đổi màu theo FE-12: trên 20 % xanh, dưới 20 % vàng, quá hạn đỏ', () => {
    expect(slaTone(0.5)).toBe('ok');
    expect(slaTone(0.19)).toBe('warning');
    expect(slaTone(-0.01)).toBe('overdue');
  });
});
