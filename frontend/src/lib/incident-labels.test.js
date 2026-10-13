import { describe, expect, it } from 'vitest';
import { gridColor, isOpenStatus, priorityText, reviewReasonText, statusText } from './incident-labels';
describe('incident-labels', () => {
  it('chỉ NEW, ASSIGNED, IN_PROGRESS là sự cố đang mở', () => {
    expect(isOpenStatus('new')).toBe(true);
    expect(isOpenStatus('IN_PROGRESS')).toBe(true);
    expect(isOpenStatus('RESOLVED')).toBe(false);
    expect(isOpenStatus('CANCELLED')).toBe(false);
  });
  it('nhãn trạng thái tiếng Việt, mã lạ giữ nguyên', () => {
    expect(statusText('ASSIGNED')).toBe('Đã giao');
    expect(statusText('WEIRD')).toBe('WEIRD');
  });
  it('mức ưu tiên 1 2 3 đổi đúng nhãn', () => {
    expect(priorityText(1)).toBe('Thấp');
    expect(priorityText('3')).toBe('Khẩn');
    expect(priorityText(null)).toBe('—');
  });
  it('lý do chờ duyệt không biết thì là Khác', () => {
    expect(reviewReasonText('NO_GRID')).toBe('Ngoài ô lưới');
    expect(reviewReasonText(null)).toBe('Khác');
    expect(reviewReasonText('SOMETHING')).toBe('Khác');
  });
  it('màu ô lưới có nhãn kèm theo', () => {
    expect(gridColor('red').label).toBe('Quá hạn');
    expect(gridColor(undefined).label).toBe('Không rõ');
  });
});
