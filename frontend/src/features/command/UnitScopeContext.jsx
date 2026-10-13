import { createContext, useContext, useMemo, useState } from 'react';
import { useAuth } from '@/features/auth/useAuth';
const UnitScopeContext = createContext(null);
/** Phạm vi đang xem trên thanh đầu trang điều hành (FE-04). Chỉ liệt kê đơn vị trong quyền của người dùng. */
export function UnitScopeProvider({ children }) {
  const { user } = useAuth();
  const [unitId, setUnitId] = useState(undefined);
  const value = useMemo(() => {
    const scopes = user?.scopes ?? [];
    const seen = new Map();
    for (const s of scopes) {
      if (s.unitId == null || !s.unitPath) continue;
      const id = Number(s.unitId);
      if (!seen.has(id)) {
        seen.set(id, { unitId: id, unitName: s.unitName ?? `Đơn vị ${id}`, unitPath: s.unitPath });
      }
    }
    const options = [...seen.values()].sort((a, b) => a.unitPath.localeCompare(b.unitPath));
    return {
      unitId,
      setUnitId,
      options,
      global: scopes.some((s) => s.unitPath == null),
    };
  }, [user, unitId]);
  return <UnitScopeContext.Provider value={value}>{children}</UnitScopeContext.Provider>;
}
export function useUnitScope() {
  const ctx = useContext(UnitScopeContext);
  if (!ctx) throw new Error('useUnitScope phải được dùng bên trong UnitScopeProvider.');
  return ctx;
}
