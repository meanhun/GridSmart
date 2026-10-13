import { Link } from 'react-router';
import { Avatar, Button, Dropdown, Flex, Grid, Typography } from 'antd';
import { DownOutlined, KeyOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { useAuth } from '@/features/auth/useAuth';
import { RealtimeStatusBadge } from '@/features/realtime/RealtimeStatusBadge';
import { vi } from '@/locales/vi';
import './shell.css';

const YEAR = new Date().getFullYear();

/** Logo và tên ứng dụng ở header. */
export function BrandLink({ to, subtitle }) {
  return (
    <Link to={to} className="app-shell__brand" aria-label={`${vi.appName} – về trang chính`}>
      <img src="/icon.svg" alt="" />
      <span style={{ minWidth: 0 }}>
        <span className="app-shell__brand-name" style={{ display: 'block' }}>
          {vi.appName}
        </span>
        {subtitle && <span className="app-shell__brand-sub">{subtitle}</span>}
      </span>
    </Link>
  );
}

/** Menu tài khoản: tên người dùng, đổi mật khẩu, đăng xuất. Trên điện thoại chỉ hiện ảnh đại diện. */
export function UserMenu({ showRealtime = false }) {
  const { user, logout } = useAuth();
  const screens = Grid.useBreakpoint();
  const items = [
    {
      key: 'who',
      disabled: true,
      label: (
        <Flex vertical style={{ cursor: 'default' }}>
          <Typography.Text strong>{user?.fullName}</Typography.Text>
          {user?.username && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {user.username}
            </Typography.Text>
          )}
          {showRealtime && (
            <span style={{ marginTop: 4 }}>
              <RealtimeStatusBadge />
            </span>
          )}
        </Flex>
      ),
    },
    { type: 'divider' },
    { key: 'password', icon: <KeyOutlined />, label: <Link to="/account/password">{vi.nav.changePassword}</Link> },
    { key: 'logout', icon: <LogoutOutlined />, danger: true, label: vi.nav.logout, onClick: () => void logout() },
  ];
  return (
    <Dropdown menu={{ items }} trigger={['click']} placement="bottomRight">
      <Button type="text" aria-label={`Tài khoản ${user?.fullName ?? ''}`} style={{ paddingInline: 6, height: 40 }}>
        <Flex align="center" gap={8}>
          <Avatar size="small" icon={<UserOutlined />} style={{ background: '#21A691' }} />
          {screens.md && (
            <Typography.Text style={{ maxWidth: 180 }} ellipsis>
              {user?.fullName}
            </Typography.Text>
          )}
          <DownOutlined style={{ fontSize: 10 }} aria-hidden="true" />
        </Flex>
      </Button>
    </Dropdown>
  );
}

/** Footer dùng chung, luôn nằm dưới cùng màn hình. */
export function AppFooter() {
  return (
    <footer className="app-shell__footer">
      <span>
        © {YEAR} {vi.appName} · Thành phố Đồng Nai
      </span>
      <span className="app-shell__footer-extra">Quản lý đô thị theo ô lưới</span>
    </footer>
  );
}
