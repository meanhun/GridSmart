import { describe, expect, it } from 'vitest';
import {
  allowedChildLevelOrder,
  ancestorIds,
  countByLevel,
  defaultCollapsedGroupKeys,
  defaultCollapsedIds,
  flattenTree,
  foldText,
  gridMatches,
  groupGridsByUnit,
  groupRowKeys,
  matchIndex,
  searchTree,
  unitPathOf,
  visibleTreeRows,
} from './admin-tree';
import { boundaryPayload } from '@/features/admin/units/AdminUnitsPage';
const node = (id, name, children = []) => ({
  id,
  name,
  children,
  unitCode: `U${id}`,
  typeCode: 'T',
  typeName: 'Loại',
  levelCode: 'L',
  levelOrder: 1,
  parentId: null,
  unitPath: null,
  isActive: true,
});
describe('admin-tree', () => {
  it('duỗi cây theo thứ tự hiển thị và giữ độ sâu', () => {
    const tree = [node(1, 'Tỉnh', [node(2, 'Phường', [node(3, 'Khu phố')])]), node(4, 'Tỉnh khác')];
    expect(flattenTree(tree).map((f) => [f.node.id, f.depth])).toEqual([
      [1, 0],
      [2, 1],
      [3, 2],
      [4, 0],
    ]);
  });
  it('cấp đơn vị con là cấp cha + 1, cấp cao nhất là 1', () => {
    expect(allowedChildLevelOrder(null)).toBe(1);
    expect(allowedChildLevelOrder(2)).toBe(3);
  });
  it('đường dẫn đơn vị dạng /1/2/', () => {
    expect(unitPathOf([{ id: 1 }, { id: '2' }])).toBe('/1/2/');
  });
});
// Cây mẫu: Thành phố › 2 phường › khu phố
const unit = (id, name, levelCode, parentId, children = []) => ({ ...node(id, name, children), levelCode, parentId });
const sampleTree = () => [
  unit(1, 'Thành phố Đồng Nai', 'PROVINCE', null, [
    unit(2, 'Phường Trấn Biên', 'COMMUNE', 1, [unit(4, 'Khu phố 1', 'HAMLET', 2), unit(5, 'Khu phố 2', 'HAMLET', 2)]),
    unit(3, 'Phường Tam Hiệp', 'COMMUNE', 1, [unit(6, 'Khu phố 1', 'HAMLET', 3)]),
  ]),
];
const ids = (rows) => rows.map((f) => Number(f.node.id));
describe('tìm kiếm không dấu', () => {
  it('bỏ dấu, chữ hoa và đ, giữ nguyên độ dài chuỗi', () => {
    expect(foldText('Phường Trấn Biên')).toBe('phuong tran bien');
    expect(foldText('Đồng Nai')).toBe('dong nai');
    expect(foldText('Phường Trấn Biên')).toHaveLength('Phường Trấn Biên'.length);
  });
  it('vị trí khớp dùng được trên chuỗi gốc, từ khóa rỗng không khớp', () => {
    expect(matchIndex('Phường Tam Hiệp', 'tam hiep')).toBe(7);
    expect(matchIndex('Phường Tam Hiệp', 'TAM HIỆP')).toBe(7);
    expect(matchIndex('Phường Tam Hiệp', '  ')).toBe(-1);
    expect(matchIndex(null, 'a')).toBe(-1);
  });
});
describe('cây thu gọn và tìm kiếm', () => {
  it('mặc định mở tỉnh/TP, thu gọn phường', () => {
    const flat = flattenTree(sampleTree());
    const collapsed = defaultCollapsedIds(flat);
    expect([...collapsed].sort()).toEqual(['2', '3']);
    expect(ids(visibleTreeRows(flat, collapsed))).toEqual([1, 2, 3]);
  });
  it('mở một phường thì thấy khu phố của phường đó', () => {
    const flat = flattenTree(sampleTree());
    expect(ids(visibleTreeRows(flat, new Set(['3'])))).toEqual([1, 2, 4, 5, 3]);
    expect(ids(visibleTreeRows(flat, new Set(['1'])))).toEqual([1]);
  });
  it('tìm khu phố: giữ tổ tiên để biết thuộc phường nào', () => {
    const flat = flattenTree(sampleTree());
    const result = searchTree(flat, 'khu pho 2');
    expect([...result.matchedIds]).toEqual(['5']);
    expect(ids(visibleTreeRows(flat, new Set(), result.visibleIds))).toEqual([1, 2, 5]);
  });
  it('tìm phường: hiện luôn các khu phố bên trong', () => {
    const flat = flattenTree(sampleTree());
    const result = searchTree(flat, 'tam hiep');
    expect(ids(visibleTreeRows(flat, new Set(), result.visibleIds))).toEqual([1, 3, 6]);
    expect(countByLevel(flat, result.matchedIds)).toEqual({ COMMUNE: 1 });
  });
  it('từ khóa rỗng không lọc', () => {
    expect(searchTree(flattenTree(sampleTree()), ' ')).toBeNull();
  });
  it('tổ tiên từ cha lên gốc', () => {
    expect(ancestorIds(flattenTree(sampleTree()), 4)).toEqual(['2', '1']);
  });
});
describe('nhóm ô lưới theo đơn vị', () => {
  const grid = (gridId, adminUnitId, extra = {}) => ({
    properties: { gridId, adminUnitId, gridCode: `G${gridId}`, name: `Ô ${gridId}`, openIncidentCount: 1, overdueIncidentCount: 0, ...extra },
  });
  it('nhóm Tỉnh/TP › phường › khu phố, cộng dồn số liệu, đơn vị theo thứ tự cây', () => {
    const flat = flattenTree(sampleTree());
    const rows = groupGridsByUnit(flat, [grid(10, 6, { overdueIncidentCount: 1 }), grid(11, 4), grid(12, 4), grid(99, 404)]);
    expect(rows.map((r) => r.key)).toEqual(['u-1', 'g-99']);
    const city = rows[0];
    expect(city.gridCount).toBe(3);
    expect(city.openCount).toBe(3);
    expect(city.overdueCount).toBe(1);
    expect(city.children.map((r) => r.key)).toEqual(['u-2', 'u-3']);
    expect(city.children[0].children[0].children.map((r) => r.key)).toEqual(['g-11', 'g-12']);
    expect(groupRowKeys(rows)).toEqual(['u-1', 'u-2', 'u-4', 'u-3', 'u-6']);
  });
  it('mặc định thu gọn phường, khu phố mở sẵn', () => {
    const rows = groupGridsByUnit(flattenTree(sampleTree()), [grid(10, 6), grid(11, 4)]);
    expect([...defaultCollapsedGroupKeys(rows)]).toEqual(['u-2', 'u-3']);
  });
  it('tìm ô lưới theo mã, tên, khu phố hoặc phường', () => {
    const f = grid(1, 4, { gridCode: 'DNI-TB-KP01-G01', hamletName: 'Khu phố 1', communeName: 'Phường Trấn Biên' });
    expect(gridMatches(f, 'tran bien')).toBe(true);
    expect(gridMatches(f, 'kp01')).toBe(true);
    expect(gridMatches(f, 'tam hiep')).toBe(false);
  });
});
describe('boundaryPayload', () => {
  it('chuỗi rỗng không gửi ranh giới', () => {
    expect(boundaryPayload('   ')).toEqual({});
  });
  it('JSON được gửi như geometry, còn lại gửi như WKT', () => {
    expect(boundaryPayload('{"type":"Polygon","coordinates":[]}')).toEqual({ boundary: { type: 'Polygon', coordinates: [] } });
    expect(boundaryPayload('POLYGON((0 0,1 0,1 1,0 0))')).toEqual({ wkt: 'POLYGON((0 0,1 0,1 1,0 0))' });
  });
});
