import { describe, expect, it } from 'vitest';
import { ApiError } from '@/api/errors';
import { errorMessage, formatWait, messageForCode } from './error-messages';
import { vi } from '@/locales/vi';
describe('messageForCode', () => {
  it('đổi mã lỗi nghiệp vụ sang câu tiếng Việt đúng với tài liệu', () => {
    expect(messageForCode('AFTER_PHOTO_OUT_OF_GRID')).toBe('Ảnh nghiệm thu phải chụp tại hiện trường. Vị trí hiện tại cách ô lưới quá xa.');
  });
  it('mã không biết thì trả câu chung, không trả mã kỹ thuật', () => {
    expect(messageForCode('SOMETHING_NEW')).toBe(vi.errors.UNKNOWN);
    expect(messageForCode(undefined)).toBe(vi.errors.UNKNOWN);
  });
});
describe('errorMessage', () => {
  it('lỗi API có mã thì dùng câu của mã', () => {
    expect(errorMessage(new ApiError(400, 'IMAGE_TOO_LARGE', 'raw'))).toBe(vi.errors.IMAGE_TOO_LARGE);
  });
  it('RATE_LIMITED kèm thời gian chờ khi có Retry-After', () => {
    const message = errorMessage(new ApiError(429, 'RATE_LIMITED', 'raw', 120));
    expect(message).toBe(`${vi.errors.RATE_LIMITED} Thử lại sau 2 phút.`);
  });
  it('RATE_LIMITED không có Retry-After thì chỉ hiện câu chung', () => {
    expect(errorMessage(new ApiError(429, 'RATE_LIMITED', 'raw'))).toBe(vi.errors.RATE_LIMITED);
  });
  it('lỗi mạng (fetch thất bại) hiện câu mất kết nối', () => {
    expect(errorMessage(new TypeError('Failed to fetch'))).toBe(vi.errors.NETWORK);
  });
  it('lỗi lạ không lộ chi tiết kỹ thuật', () => {
    expect(errorMessage(new Error('NullReferenceException at X'))).toBe(vi.errors.UNKNOWN);
  });
});
describe('formatWait', () => {
  it('đổi giây sang câu dễ đọc', () => {
    expect(formatWait(30)).toBe('30 giây');
    expect(formatWait(61)).toBe('2 phút');
  });
});
