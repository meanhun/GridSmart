import { beforeEach, describe, expect, it, vi as vitest } from 'vitest';
import { findLocalReport, readLocalReports, saveLocalReport } from './local-reports';
const base = { phone: '0911222333', createdAt: '2026-10-10T02:00:00Z' };
describe('local-reports', () => {
  beforeEach(() => window.localStorage.clear());
  it('lưu và đọc lại phản ánh, mới nhất đứng đầu', () => {
    saveLocalReport({ ...base, trackingCode: 'DN-2026-A001' });
    saveLocalReport({ ...base, trackingCode: 'DN-2026-A002' });
    expect(readLocalReports().map((r) => r.trackingCode)).toEqual(['DN-2026-A002', 'DN-2026-A001']);
  });
  it('lưu trùng mã thì thay bản cũ, không nhân đôi', () => {
    saveLocalReport({ ...base, trackingCode: 'DN-2026-A001' });
    saveLocalReport({ ...base, trackingCode: 'DN-2026-A001', categoryName: 'Đường hỏng' });
    const all = readLocalReports();
    expect(all).toHaveLength(1);
    expect(all[0].categoryName).toBe('Đường hỏng');
  });
  it('tìm theo mã không phân biệt hoa thường', () => {
    saveLocalReport({ ...base, trackingCode: 'DN-2026-A001' });
    expect(findLocalReport('dn-2026-a001')?.phone).toBe('0911222333');
    expect(findLocalReport('DN-9999')).toBeUndefined();
  });
  it('dữ liệu hỏng trong localStorage không làm vỡ ứng dụng', () => {
    window.localStorage.setItem('gridsmart.myReports', '{không phải json');
    expect(readLocalReports()).toEqual([]);
  });
  it('bộ nhớ bị chặn thì ghi lỗi được nuốt, không ném ra ngoài', () => {
    const spy = vitest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    expect(() => saveLocalReport({ ...base, trackingCode: 'DN-1' })).not.toThrow();
    spy.mockRestore();
  });
});
