import { describe, expect, it } from 'vitest';
import { haversineMeters, pointInGeometry, pointInRing } from './geo-math';
// Hình vuông nhỏ quanh Biên Hòa, đơn vị [lon, lat]
const square = [
  [106.82, 10.94],
  [106.83, 10.94],
  [106.83, 10.95],
  [106.82, 10.95],
  [106.82, 10.94],
];
describe('haversineMeters', () => {
  it('cùng một điểm thì bằng 0', () => {
    expect(haversineMeters(10.9447, 106.8243, 10.9447, 106.8243)).toBe(0);
  });
  it('một độ vĩ tuyến xấp xỉ 111 km', () => {
    expect(haversineMeters(10, 106, 11, 106) / 1000).toBeCloseTo(111.2, 0);
  });
  it('khoảng cách 0.001 độ vĩ tuyến xấp xỉ 111 m', () => {
    expect(haversineMeters(10.9, 106.8, 10.901, 106.8)).toBeGreaterThan(105);
    expect(haversineMeters(10.9, 106.8, 10.901, 106.8)).toBeLessThan(115);
  });
});
describe('pointInRing', () => {
  it('điểm bên trong hình vuông', () => {
    expect(pointInRing(10.945, 106.825, square)).toBe(true);
  });
  it('điểm bên ngoài hình vuông', () => {
    expect(pointInRing(10.96, 106.825, square)).toBe(false);
    expect(pointInRing(10.945, 106.84, square)).toBe(false);
  });
});
describe('pointInGeometry', () => {
  it('hỗ trợ Polygon', () => {
    expect(pointInGeometry(10.945, 106.825, { type: 'Polygon', coordinates: [square] })).toBe(true);
  });
  it('hỗ trợ MultiPolygon', () => {
    const shifted = square.map(([lon, lat]) => [lon + 0.1, lat]);
    const geometry = { type: 'MultiPolygon', coordinates: [[square], [shifted]] };
    expect(pointInGeometry(10.945, 106.925, geometry)).toBe(true);
    expect(pointInGeometry(10.945, 106.95, geometry)).toBe(false);
  });
  it('loại hình học không hỗ trợ thì trả false', () => {
    expect(pointInGeometry(10, 106, { type: 'Point', coordinates: [106, 10] })).toBe(false);
  });
});
