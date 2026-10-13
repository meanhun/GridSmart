import { useContext } from 'react';
import { AuthContext } from './AuthContext';
import { hasPermission } from '@/lib/permissions';
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth phải được dùng bên trong AuthProvider.');
  return ctx;
}
/** Kiểm tra quyền cho người dùng hiện tại (FE-04). Backend vẫn là nơi chặn cuối cùng. */
export function usePermission(code, unitPath) {
  const { permissionContext } = useAuth();
  return hasPermission(permissionContext, code, unitPath);
}
