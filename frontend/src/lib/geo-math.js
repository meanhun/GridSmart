// Toán địa lý dùng ở giao diện: khoảng cách và kiểm tra điểm trong ô lưới (FE-12, FE-13).
const EARTH_RADIUS_M = 6_371_000;
/** Khoảng cách đường chim bay giữa hai tọa độ, đơn vị mét. */
export function haversineMeters(lat1, lon1, lat2, lon2) {
  const toRad = (deg) => (deg * Math.PI) / 180;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 2 * EARTH_RADIUS_M * Math.asin(Math.min(1, Math.sqrt(a)));
}
/**
 * Đổi tọa độ từ API sang số. OpenAPI sinh kiểu `number | string` cho số thập phân,
 * nên cần chuẩn hóa trước khi tính toán. Giá trị không hợp lệ trả null.
 */
export function toNumber(value) {
  if (value === null || value === undefined || value === '') return null;
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : null;
}
/** Thuật toán ray casting: điểm (lat, lon) có nằm trong vòng không. */
export function pointInRing(lat, lon, ring) {
  let inside = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, yi] = ring[i];
    const [xj, yj] = ring[j];
    const crosses = yi > lat !== yj > lat && lon < ((xj - xi) * (lat - yi)) / (yj - yi) + xi;
    if (crosses) inside = !inside;
  }
  return inside;
}
/** Điểm nằm trong đa giác GeoJSON (Polygon hoặc MultiPolygon). Vòng ngoài là vòng đầu tiên của mỗi polygon. */
export function pointInGeometry(lat, lon, geometry) {
  if (geometry.type === 'Polygon') {
    const rings = geometry.coordinates;
    return rings.length > 0 && pointInRing(lat, lon, rings[0]);
  }
  if (geometry.type === 'MultiPolygon') {
    const polygons = geometry.coordinates;
    return polygons.some((rings) => rings.length > 0 && pointInRing(lat, lon, rings[0]));
  }
  return false;
}
