import { describe, expect, it } from 'vitest';
import { checkBoundaryText, geometryToWkt, GEOJSON_EXAMPLE, WKT_EXAMPLE } from './boundary-input';
describe('geometryToWkt', () => {
  it('đổi Polygon GeoJSON thành WKT đúng thứ tự kinh độ, vĩ độ', () => {
    const geometry = {
      type: 'Polygon',
      coordinates: [
        [
          [106.82, 10.94],
          [106.83, 10.94],
          [106.83, 10.95],
          [106.82, 10.94],
        ],
      ],
    };
    expect(geometryToWkt(geometry)).toBe('POLYGON((106.82 10.94, 106.83 10.94, 106.83 10.95, 106.82 10.94))');
  });
  it('kết quả đổi ra WKT vẫn qua được kiểm tra tọa độ', () => {
    const geometry = {
      type: 'Polygon',
      coordinates: [
        [
          [106.82, 10.94],
          [106.83, 10.94],
          [106.83, 10.95],
          [106.82, 10.94],
        ],
      ],
    };
    expect(checkBoundaryText(geometryToWkt(geometry))).toBeNull();
  });
  it('MultiPolygon và geometry rỗng', () => {
    const multi = {
      type: 'MultiPolygon',
      coordinates: [
        [
          [
            [106.82, 10.94],
            [106.83, 10.94],
            [106.83, 10.95],
            [106.82, 10.94],
          ],
        ],
      ],
    };
    expect(geometryToWkt(multi)).toMatch(/^MULTIPOLYGON\(\(\(/);
    expect(geometryToWkt(null)).toBeNull();
  });
});
describe('checkBoundaryText', () => {
  it('chuỗi rỗng không báo lỗi (bỏ qua ô không nhập)', () => {
    expect(checkBoundaryText('   ')).toBeNull();
  });
  it('mẫu WKT và GeoJSON hiển thị trên form đều hợp lệ', () => {
    expect(checkBoundaryText(WKT_EXAMPLE)).toBeNull();
    expect(checkBoundaryText(GEOJSON_EXAMPLE)).toBeNull();
  });
  it('nhập ngược kinh/vĩ độ trong WKT bị phát hiện', () => {
    const swapped = 'POLYGON((10.940 106.820, 10.940 106.821, 10.941 106.821, 10.941 106.820, 10.940 106.820))';
    expect(checkBoundaryText(swapped)).toContain('nằm ngoài tỉnh Đồng Nai');
  });
  it('nhập ngược kinh/vĩ độ trong GeoJSON bị phát hiện', () => {
    const swapped = '{"type":"Polygon","coordinates":[[[10.94,106.82],[10.94,106.821],[10.941,106.821],[10.94,106.82]]]}';
    expect(checkBoundaryText(swapped)).toContain('nằm ngoài tỉnh Đồng Nai');
  });
  it('chấp nhận Feature chứa geometry', () => {
    const feature =
      '{"type":"Feature","properties":{},"geometry":{"type":"Polygon","coordinates":[[[106.82,10.94],[106.821,10.94],[106.821,10.941],[106.82,10.94]]]}}';
    expect(checkBoundaryText(feature)).toBeNull();
  });
  it('WKT không bắt đầu bằng POLYGON bị báo kèm ví dụ', () => {
    expect(checkBoundaryText('106.82 10.94, 106.83 10.94')).toContain('POLYGON((...))');
  });
  it('ít hơn 4 điểm không khép kín bị báo', () => {
    expect(checkBoundaryText('POLYGON((106.82 10.94, 106.83 10.94, 106.83 10.95))')).toContain('ít nhất 4 điểm');
  });
  it('JSON sai cú pháp bị báo', () => {
    expect(checkBoundaryText('{"type":"Polygon",')).toContain('không đúng định dạng JSON');
  });
  it('loại hình học không phải đa giác bị báo', () => {
    expect(checkBoundaryText('{"type":"Point","coordinates":[106.82,10.94]}')).toContain('"Polygon" hoặc "MultiPolygon"');
  });
});
