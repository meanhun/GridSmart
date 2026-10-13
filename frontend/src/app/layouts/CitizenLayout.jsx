import { Outlet, useLocation, useNavigate } from 'react-router';
import { Button, Flex, Grid } from 'antd';
import { LayoutOutlined, LoginOutlined, SearchOutlined } from '@ant-design/icons';
import { useAuth } from '@/features/auth/useAuth';
import { defaultLandingPath } from '@/lib/permissions';
import { vi } from '@/locales/vi';
import { AppFooter, BrandLink } from './AppShell';

/** Cổng công dân (`/`), ưu tiên điện thoại. Header và footer cố định, chỉ nội dung cuộn. Có lối vào đăng nhập cho cán bộ và quản lý. */
export function CitizenLayout() {
  const { status, user, permissionContext } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const screens = Grid.useBreakpoint();
  const staffPath = user ? defaultLandingPath(permissionContext) : '/';
  // Trên điện thoại rất hẹp chỉ hiện biểu tượng; nhãn vẫn được đọc bởi trình đọc màn hình (aria-label).
  const showLabel = Boolean(screens.sm);
  const navButton = (to, icon, text, props = {}) => (
    <Button
      type={props.type ?? 'text'}
      icon={icon}
      onClick={() => navigate(to)}
      aria-label={showLabel ? undefined : text}
      aria-current={pathname === to || pathname.startsWith(`${to}/`) ? 'page' : undefined}
    >
      {showLabel ? text : null}
    </Button>
  );

  return (
    <div className="app-shell app-shell--plain">
      <header className="app-shell__header">
        <div className="app-shell__header-inner">
          <BrandLink to="/" subtitle={vi.nav.citizenHome} />
          <nav aria-label="Điều hướng công dân" style={{ marginLeft: 'auto', flex: 'none' }}>
            <Flex align="center" gap={4}>
              {navButton('/track', <SearchOutlined />, vi.nav.track)}
              {status === 'authenticated' && staffPath !== '/'
                ? navButton(staffPath, <LayoutOutlined />, 'Khu quản lý', { type: 'primary' })
                : status === 'anonymous'
                  ? navButton('/login', <LoginOutlined />, vi.nav.login, { type: 'default' })
                  : null}
            </Flex>
          </nav>
        </div>
      </header>
      <main className="app-shell__content" id="main-content">
        <div className="app-shell__page app-shell__page--narrow">
          <Outlet />
        </div>
      </main>
      <AppFooter />
    </div>
  );
}
