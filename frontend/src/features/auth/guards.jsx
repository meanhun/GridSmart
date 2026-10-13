import { Navigate, Outlet, useLocation } from 'react-router';
import { Spin } from 'antd';
import { useAuth } from './useAuth';
import { hasPermission } from '@/lib/permissions';
import { ForbiddenPage } from './ForbiddenPage';
import { vi } from '@/locales/vi';

function FullPageStatus({ children }) {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <Spin tip={children} size="large">
        <div style={{ width: 120, height: 60 }} />
      </Spin>
    </div>
  );
}

/** Yêu cầu đăng nhập. Chưa đăng nhập thì chuyển tới /login và nhớ đường dẫn cũ. */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();
  if (status === 'loading') return <FullPageStatus>{vi.common.loading}</FullPageStatus>;
  if (status === 'anonymous') return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}

/** Yêu cầu quyền cho route. Thiếu quyền thì hiện trang 403. */
export function RequirePermission({ permission, unitPath }) {
  const { status, permissionContext } = useAuth();
  if (status === 'loading') return <FullPageStatus>{vi.common.loading}</FullPageStatus>;
  if (status === 'anonymous') return <Navigate to="/login" replace />;
  if (!hasPermission(permissionContext, permission, unitPath)) return <ForbiddenPage />;
  return <Outlet />;
}
