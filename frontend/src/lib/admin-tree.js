/** Duỗi cây thành danh sách có độ sâu, theo thứ tự hiển thị. */
export function flattenTree(nodes, depth = 0) {
  return nodes.flatMap((node) => [{ node, depth }, ...flattenTree(node.children ?? [], depth + 1)]);
}
/**
 * Tên đầy đủ kèm đơn vị cha, ví dụ "Khu phố 1 · Phường Biên Hòa · Tỉnh Đồng Nai".
 * Dùng trong danh sách chọn, vì nhiều đơn vị cùng tên (các phường đều có "Khu phố 1").
 */
export function unitFullName(flat, id) {
  const byId = new Map(flat.map((f) => [String(f.node.id), f.node]));
  const names = [];
  let current = byId.get(String(id));
  while (current) {
    names.push(current.name);
    current = current.parentId != null ? byId.get(String(current.parentId)) : undefined;
  }
  return names.join(' · ');
}
/** Cấp hợp lệ cho đơn vị con: cấp của cha + 1 (levelOrder tăng từ tỉnh xuống khu phố). */
export function allowedChildLevelOrder(parentLevelOrder) {
  return parentLevelOrder == null ? 1 : Number(parentLevelOrder) + 1;
}
/** Đường dẫn dạng /1/2/ để so khớp phạm vi (giống backend). */
export function unitPathOf(path) {
  return `/${path.map((u) => u.id).join('/')}/`;
}
/**
 * Bỏ dấu tiếng Việt và chữ hoa để tìm "tran bien" ra "Trấn Biên".
 * Mỗi ký tự (NFC) đổi thành đúng một ký tự, nên vị trí khớp dùng lại được trên chuỗi gốc để tô sáng.
 */
export function foldText(text) {
  return Array.from(String(text ?? '').normalize('NFC'), (c) => {
    if (c === 'đ' || c === 'Đ') return 'd';
    const folded = c.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    return folded.length === 1 ? folded : c;
  }).join('');
}
/** Vị trí khớp đầu tiên của từ khóa trong chuỗi (không phân biệt dấu, hoa thường). -1 nếu không khớp hoặc từ khóa rỗng. */
export function matchIndex(text, query) {
  const q = foldText(query).trim();
  if (!q) return -1;
  return foldText(text).indexOf(q);
}
/** Đơn vị khớp từ khóa theo tên hoặc mã đơn vị. */
export function unitMatches(node, query) {
  return matchIndex(node.name, query) >= 0 || matchIndex(node.unitCode, query) >= 0;
}
/** Id các đơn vị tổ tiên (không gồm chính nó), từ cha lên gốc. */
export function ancestorIds(flat, id) {
  const byId = new Map(flat.map((f) => [String(f.node.id), f.node]));
  const ids = [];
  let current = byId.get(String(id));
  while (current?.parentId != null) {
    ids.push(String(current.parentId));
    current = byId.get(String(current.parentId));
  }
  return ids;
}
/**
 * Tìm trong cây: giữ đơn vị khớp, tổ tiên của nó (để biết nằm ở phường/tỉnh nào) và con cháu của nó
 * (tìm "Trấn Biên" thì thấy luôn các khu phố). Trả null khi từ khóa rỗng.
 */
export function searchTree(flat, query) {
  if (!foldText(query).trim()) return null;
  const childrenOf = new Map();
  for (const f of flat) {
    const parent = f.node.parentId != null ? String(f.node.parentId) : null;
    if (!childrenOf.has(parent)) childrenOf.set(parent, []);
    childrenOf.get(parent).push(String(f.node.id));
  }
  const matchedIds = new Set(flat.filter((f) => unitMatches(f.node, query)).map((f) => String(f.node.id)));
  const visibleIds = new Set();
  for (const id of matchedIds) {
    visibleIds.add(id);
    for (const a of ancestorIds(flat, id)) visibleIds.add(a);
    const stack = [...(childrenOf.get(id) ?? [])];
    while (stack.length > 0) {
      const child = stack.pop();
      visibleIds.add(child);
      stack.push(...(childrenOf.get(child) ?? []));
    }
  }
  return { matchedIds, visibleIds };
}
/** Đếm đơn vị khớp theo cấp, ví dụ { COMMUNE: 2, HAMLET: 5 }. */
export function countByLevel(flat, ids) {
  const counts = {};
  for (const f of flat) {
    if (!ids.has(String(f.node.id))) continue;
    counts[f.node.levelCode] = (counts[f.node.levelCode] ?? 0) + 1;
  }
  return counts;
}
/** Đơn vị có con, mặc định thu gọn từ cấp thứ hai: tỉnh/TP mở sẵn, phường/xã thu gọn khu phố/thôn. */
export function defaultCollapsedIds(flat) {
  return new Set(flat.filter((f) => f.depth >= 1 && (f.node.children ?? []).length > 0).map((f) => String(f.node.id)));
}
/** Id mọi đơn vị có con (dùng cho "Thu gọn tất cả"). */
export function parentIds(flat) {
  return flat.filter((f) => (f.node.children ?? []).length > 0).map((f) => String(f.node.id));
}
/**
 * Các dòng cây cần hiển thị: bỏ con cháu của đơn vị đang thu gọn.
 * Khi đang tìm kiếm (visibleIds khác null) chỉ giữ đơn vị nằm trong kết quả.
 */
export function visibleTreeRows(flat, collapsed, visibleIds = null) {
  const rows = [];
  let hiddenBelow = Infinity;
  for (const f of flat) {
    if (f.depth > hiddenBelow) continue;
    hiddenBelow = Infinity;
    const id = String(f.node.id);
    if (visibleIds && !visibleIds.has(id)) {
      hiddenBelow = f.depth;
      continue;
    }
    rows.push(f);
    if (collapsed.has(id) && (f.node.children ?? []).length > 0) hiddenBelow = f.depth;
  }
  return rows;
}
/** Ô lưới khớp từ khóa theo mã, tên, khu phố/thôn hoặc phường/xã. */
export function gridMatches(feature, query) {
  const p = feature.properties ?? {};
  return [p.gridCode, p.name, p.hamletName, p.communeName].some((t) => matchIndex(t, query) >= 0);
}
/**
 * Nhóm ô lưới theo cây đơn vị cho bảng thu gọn được: Tỉnh/TP › Phường/xã › Khu phố/thôn › ô lưới.
 * Chỉ giữ nhánh có ô lưới; mỗi dòng đơn vị cộng dồn số ô lưới, số sự cố mở và quá hạn của cả nhánh.
 * Ô lưới thuộc đơn vị không có trong cây được xếp cuối, không nhóm.
 */
export function groupGridsByUnit(flat, features) {
  const byId = new Map(flat.map((f) => [String(f.node.id), f]));
  const rowsByUnit = new Map();
  const roots = [];
  const orphans = [];
  const ensure = (entry) => {
    const id = String(entry.node.id);
    let row = rowsByUnit.get(id);
    if (row) return row;
    row = { key: `u-${id}`, kind: 'unit', node: entry.node, depth: entry.depth, children: [], gridCount: 0, openCount: 0, overdueCount: 0 };
    rowsByUnit.set(id, row);
    const parent = entry.node.parentId != null ? byId.get(String(entry.node.parentId)) : undefined;
    (parent ? ensure(parent).children : roots).push(row);
    return row;
  };
  for (const feature of features) {
    const p = feature.properties ?? {};
    const gridRow = { key: `g-${p.gridId}`, kind: 'grid', feature };
    const unit = byId.get(String(p.adminUnitId));
    if (!unit) {
      orphans.push(gridRow);
      continue;
    }
    ensure(unit).children.push(gridRow);
    for (let e = unit; e; e = e.node.parentId != null ? byId.get(String(e.node.parentId)) : undefined) {
      const row = rowsByUnit.get(String(e.node.id));
      row.gridCount += 1;
      row.openCount += Number(p.openIncidentCount ?? 0);
      row.overdueCount += Number(p.overdueIncidentCount ?? 0);
    }
  }
  // Đơn vị con xếp theo thứ tự trong cây (không theo thứ tự gặp ô lưới đầu tiên)
  const order = new Map(flat.map((f, i) => [`u-${f.node.id}`, i]));
  const sortChildren = (rows) => {
    rows.sort((a, b) => (a.kind === 'unit' && b.kind === 'unit' ? order.get(a.key) - order.get(b.key) : 0));
    for (const r of rows) if (r.kind === 'unit') sortChildren(r.children);
  };
  sortChildren(roots);
  return [...roots, ...orphans];
}
/** Khóa mọi dòng đơn vị trong cây ô lưới (để mở rộng/thu gọn tất cả). */
export function groupRowKeys(rows) {
  return rows.flatMap((r) => (r.kind === 'unit' ? [r.key, ...groupRowKeys(r.children)] : []));
}
/** Dòng đơn vị mặc định thu gọn: phường/xã (cấp thứ hai trở xuống) còn chứa đơn vị con. Khu phố mở sẵn để thấy ô lưới khi mở phường. */
export function defaultCollapsedGroupKeys(rows) {
  return new Set(
    rows.flatMap(function walk(r) {
      if (r.kind !== 'unit') return [];
      const hasUnitChildren = r.children.some((c) => c.kind === 'unit');
      return [...(r.depth >= 1 && hasUnitChildren ? [r.key] : []), ...r.children.flatMap(walk)];
    }),
  );
}
