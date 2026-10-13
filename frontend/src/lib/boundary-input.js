// Kiểm tra ranh giới người dùng dán vào form (WKT hoặc GeoJSON) trước khi gửi lên server.
// Mục tiêu: bắt lỗi nhập ngược kinh/vĩ độ ngay trên form, thay vì để lưu sai vị trí.
/** Khung tọa độ tỉnh Đồng Nai, khớp với kiểm tra phía backend. */
export const PROVINCE_BOUNDS = { minLat: 10.2, maxLat: 12.2, minLon: 105.9, maxLon: 108.2 };
/** Mẫu hiển thị cho người dùng. Lưu ý thứ tự: kinh độ (≈106.8) trước, vĩ độ (≈10.9) sau. */
export const WKT_EXAMPLE = 'POLYGON((106.820 10.940, 106.821 10.940, 106.821 10.941, 106.820 10.941, 106.820 10.940))';
export const GEOJSON_EXAMPLE =
  '{"type":"Polygon","coordinates":[[[106.820,10.940],[106.821,10.940],[106.821,10.941],[106.820,10.941],[106.820,10.940]]]}';
/** Chuỗi tọa độ WKT "x y, x y, ..." từ một vòng [kinh độ, vĩ độ]. */
function ringToWkt(ring) {
  return `(${ring.map(([lon, lat]) => `${lon} ${lat}`).join(', ')})`;
}
/**
 * Chuyển geometry GeoJSON (Polygon hoặc MultiPolygon) thành WKT để hiển thị trong ô nhập.
 * Trả null nếu không phải đa giác.
 */
export function geometryToWkt(geometry) {
  if (!geometry) return null;
  if (geometry.type === 'Polygon') {
    const rings = geometry.coordinates;
    return `POLYGON(${rings.map(ringToWkt).join(', ')})`;
  }
  if (geometry.type === 'MultiPolygon') {
    const polygons = geometry.coordinates;
    return `MULTIPOLYGON(${polygons.map((rings) => `(${rings.map(ringToWkt).join(', ')})`).join(', ')})`;
  }
  return null;
}
/** Đọc JSON, trả undefined nếu sai cú pháp. */
function parseJson(text) {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}
/** Lấy các cặp [kinh độ, vĩ độ] từ mảng tọa độ GeoJSON lồng nhau. */
function collectPairs(node, out) {
  if (!Array.isArray(node)) return;
  if (node.length >= 2 && typeof node[0] === 'number' && typeof node[1] === 'number') {
    out.push([node[0], node[1]]);
    return;
  }
  for (const child of node) collectPairs(child, out);
}
/** Trả thông báo lỗi tiếng Việt, hoặc null nếu ranh giới hợp lệ về định dạng và vị trí. */
export function checkBoundaryText(text) {
  const trimmed = text.trim();
  if (!trimmed) return null;
  const pairs = [];
  if (trimmed.startsWith('{')) {
    const parsed = parseJson(trimmed);
    if (parsed === undefined) return 'Nội dung GeoJSON không đúng định dạng JSON. Kiểm tra dấu ngoặc và dấu phẩy.';
    const geometry = parsed.type === 'Feature' ? parsed.geometry : parsed;
    if (!geometry || (geometry.type !== 'Polygon' && geometry.type !== 'MultiPolygon')) {
      return 'GeoJSON phải có "type": "Polygon" hoặc "MultiPolygon".';
    }
    collectPairs(geometry.coordinates, pairs);
  } else {
    if (!/^(MULTI)?POLYGON/i.test(trimmed)) {
      return `WKT phải bắt đầu bằng POLYGON((...)). Ví dụ: ${WKT_EXAMPLE}`;
    }
    const numbers = (trimmed.match(/-?\d+(?:\.\d+)?/g) ?? []).map(Number);
    if (numbers.length % 2 !== 0) return 'WKT có số tọa độ lẻ: mỗi điểm cần đủ một cặp kinh độ và vĩ độ.';
    for (let i = 0; i < numbers.length; i += 2) pairs.push([numbers[i], numbers[i + 1]]);
  }
  if (pairs.length < 4) return 'Ranh giới cần ít nhất 4 điểm. Điểm cuối phải trùng điểm đầu để khép kín.';
  const outside = pairs.find(
    ([lon, lat]) =>
      lat < PROVINCE_BOUNDS.minLat || lat > PROVINCE_BOUNDS.maxLat || lon < PROVINCE_BOUNDS.minLon || lon > PROVINCE_BOUNDS.maxLon,
  );
  if (outside) {
    const [lon, lat] = outside;
    return `Điểm (${lon}, ${lat}) nằm ngoài tỉnh Đồng Nai. Kiểm tra thứ tự: kinh độ (≈106.8) đứng trước, vĩ độ (≈10.9) đứng sau.`;
  }
  return null;
}
