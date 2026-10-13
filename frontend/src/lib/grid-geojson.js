// Kiểu đọc FeatureCollection ô lưới từ /api/grids và /api/dashboard/grid-status.
// Backend trả GeoJSON nên client sinh ra kiểu JsonElement; module này gắn kiểu cụ thể cho các thuộc tính đang dùng.
export function gridFeatures(data) {
  const fc = data;
  return Array.isArray(fc?.features) ? fc.features : [];
}
