import { usePermission } from '@/features/auth/useAuth';
/** Ẩn nút hoặc khu vực giao diện khi không có quyền (FE-04). */
export function Can({ permission, unitPath, children, fallback = null }) {
  const allowed = usePermission(permission, unitPath);
  return <>{allowed ? children : fallback}</>;
}
