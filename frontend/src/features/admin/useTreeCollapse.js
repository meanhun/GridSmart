import { useState } from 'react';
/**
 * Trạng thái thu gọn của cây (tập id/khóa đang thu gọn).
 * Khi duyệt: bắt đầu từ defaultCollapsed. Khi tìm kiếm: mọi nhánh trong kết quả mở sẵn, thu gọn riêng,
 * xóa từ khóa thì quay lại trạng thái duyệt trước đó.
 */
export function useTreeCollapse(defaultCollapsed, searching) {
  const [browse, setBrowse] = useState(null);
  const [search, setSearch] = useState(() => new Set());
  const collapsed = searching ? search : (browse ?? defaultCollapsed);
  const update = (fn) => (searching ? setSearch((prev) => fn(prev)) : setBrowse((prev) => fn(prev ?? defaultCollapsed)));
  return {
    collapsed,
    toggle: (id) =>
      update((prev) => {
        const next = new Set(prev);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        return next;
      }),
    expandAll: () => update(() => new Set()),
    collapseAll: (ids) => update(() => new Set(ids)),
    /** Mở các nhánh chứa một mục (ví dụ mục vừa chọn trên bản đồ), ở cả chế độ duyệt và tìm kiếm. */
    reveal: (ids) => {
      const without = (prev) => {
        const next = new Set(prev);
        for (const id of ids) next.delete(id);
        return next;
      };
      setBrowse((prev) => without(prev ?? defaultCollapsed));
      setSearch(without);
    },
    resetSearch: () => setSearch(new Set()),
  };
}
