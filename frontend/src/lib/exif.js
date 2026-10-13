import exifr from 'exifr';
/**
 * Đọc GPS từ EXIF của ảnh (FE-08 bước 2, khi không lấy được vị trí trực tiếp).
 * Trả null nếu ảnh không có GPS hoặc không đọc được.
 */
export async function readGpsFromExif(file) {
  try {
    const gps = await exifr.gps(file);
    if (!gps || typeof gps.latitude !== 'number' || typeof gps.longitude !== 'number') return null;
    return { latitude: gps.latitude, longitude: gps.longitude };
  } catch {
    return null;
  }
}
