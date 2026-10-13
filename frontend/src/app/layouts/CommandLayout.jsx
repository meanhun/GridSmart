import { useEffect, useState } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import { Badge, Breadcrumb, Button, Drawer, Flex, Layout, Menu, Select, Typography } from 'antd';
import {
  AlertOutlined,
  ApartmentOutlined,
  AppstoreOutlined,
  AuditOutlined,
  BarChartOutlined,
  EnvironmentOutlined,
  FieldTimeOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
  SafetyCertificateOutlined,
  TagsOutlined,
  TeamOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { useGetApiDashboardSummary } from '@/api/generated/endpoints/dashboard/dashboard';
import { NotificationBell } from '@/features/notifications/NotificationBell';
import { RealtimeStatusBadge } from '@/features/realtime/RealtimeStatusBadge';
import { useAuth } from '@/features/auth/useAuth';
import { UnitScopeProvider, useUnitScope } from '@/features/command/UnitScopeContext';
import { DEMO_ENABLED } from '@/features/admin/DemoPage';
import { hasPermission } from '@/lib/permissions';
import { vi } from '@/locales/vi';
import { AppFooter, BrandLink, UserMenu } from './AppShell';
import { useShellMode } from './useShellMode';

const OPERATION_ITEMS = [
  { permission: 'dashboard.view', path: '/command', label: 'Bản đồ điều hành', icon: <EnvironmentOutlined /> },
  { permission: 'incident.view', path: '/command/incidents', label: 'Sự cố', icon: <AlertOutlined /> },
  { permission: 'report.view', path: '/command/reports', label: 'Báo cáo', icon: <BarChartOutlined /> },
];

const ADMIN_ITEMS = [
  { permission: 'admin_unit.view', path: '/command/admin/units', label: 'Đơn vị hành chính', icon: <ApartmentOutlined /> },
  { permission: 'grid.view', path: '/command/admin/grids', label: 'Ô lưới', icon: <AppstoreOutlined /> },
  { permission: 'category.manage', path: '/command/admin/categories', label: 'Danh mục', icon: <TagsOutlined /> },
  { permission: 'sla.manage', path: '/command/admin/sla', label: 'SLA', icon: <FieldTimeOutlined /> },
  { permission: 'user.view', path: '/command/admin/users', label: 'Người dùng', icon: <TeamOutlined /> },
  { permission: 'role.manage', path: '/command/admin/roles', label: 'Vai trò và quyền', icon: <SafetyCertificateOutlined /> },
  ...(DEMO_ENABLED ? [{ permission: 'role.manage', path: '/command/demo', label: 'Tua nhanh SLA (demo)', icon: <ThunderboltOutlined /> }] : []),
];

const REVIEW_PATH = '/command/review';
const SIDER_WIDTH = 240;
const SIDER_COLLAPSED_WIDTH = 72;
const COLLAPSE_KEY = 'gridsmart.sidebarCollapsed';

/** Nhãn breadcrumb theo đường dẫn; trang chi tiết (id) dùng nhãn riêng theo trang cha. */
const CRUMB_LABELS = Object.fromEntries([
  ...[...OPERATION_ITEMS, ...ADMIN_ITEMS].map((i) => [i.path, i.label]),
  [REVIEW_PATH, 'Hàng chờ duyệt'],
  ['/command/admin', 'Quản trị'],
]);
const DETAIL_LABELS = { '/command/incidents': 'Chi tiết sự cố', '/command/admin/users': 'Chi tiết người dùng' };

function readCollapsedPreference() {
  try {
    return window.localStorage.getItem(COLLAPSE_KEY) === '1';
  } catch {
    return false;
  }
}

/** Trung tâm điều hành (`/command`): header, sidebar, footer cố định, chỉ vùng nội dung cuộn (FE-14). Menu và nút hiển thị theo quyền. */
export function CommandLayout() {
  return (
    <UnitScopeProvider>
      <CommandShell />
    </UnitScopeProvider>
  );
}

function CommandShell() {
  const { logout, permissionContext } = useAuth();
  const { unitId, setUnitId, options, global } = useUnitScope();
  const summary = useGetApiDashboardSummary({ unitId });
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const mode = useShellMode();
  const pending = summary.data ? Number(summary.data.data.pendingReview) : undefined;

  // Máy tính: nhớ lựa chọn thu gọn. Máy tính bảng: mặc định thu gọn còn biểu tượng. Điện thoại: menu dạng ngăn kéo.
  const [desktopCollapsed, setDesktopCollapsed] = useState(readCollapsedPreference);
  const [tabletExpanded, setTabletExpanded] = useState(false);
  // Ngăn kéo nhớ trang lúc mở: chuyển sang trang khác là tự đóng
  const [drawerPath, setDrawerPath] = useState(null);
  const drawerOpen = drawerPath === pathname;
  const setDrawerOpen = (open) => setDrawerPath(open ? pathname : null);
  const collapsed = mode === 'desktop' ? desktopCollapsed : !tabletExpanded;
  useEffect(() => {
    try {
      window.localStorage.setItem(COLLAPSE_KEY, desktopCollapsed ? '1' : '0');
    } catch {
      // Trình duyệt chặn lưu trữ: chỉ mất ghi nhớ lựa chọn
    }
  }, [desktopCollapsed]);

  const toggleMenu = () => {
    if (mode === 'mobile') setDrawerOpen(!drawerOpen);
    else if (mode === 'tablet') setTabletExpanded((v) => !v);
    else setDesktopCollapsed((v) => !v);
  };

  const allowed = (items) => items.filter((item) => hasPermission(permissionContext, item.permission));
  const operationItems = allowed(OPERATION_ITEMS).map((item) => ({ key: item.path, label: item.label, icon: item.icon }));
  const adminItems = allowed(ADMIN_ITEMS).map((item) => ({ key: item.path, label: item.label, icon: item.icon }));
  const showReview = hasPermission(permissionContext, 'incident.classify');
  const hasPending = pending !== undefined && pending > 0;
  const menuItems = [
    ...operationItems,
    ...(showReview
      ? [
          {
            key: REVIEW_PATH,
            // Khi thu gọn chỉ còn biểu tượng: chấm đỏ báo có việc chờ duyệt
            icon: (
              <Badge dot={hasPending && collapsed && mode !== 'mobile'} offset={[2, 2]}>
                <AuditOutlined />
              </Badge>
            ),
            label: (
              <Flex justify="space-between" align="center">
                <span>Hàng chờ duyệt</span>
                {hasPending && <Badge count={pending} size="small" aria-label={`${pending} chờ duyệt`} />}
              </Flex>
            ),
          },
        ]
      : []),
    ...(adminItems.length ? [{ type: 'group', key: 'admin-group', label: 'Quản trị', children: adminItems }] : []),
  ];
  // Mục đang mở: trang /command khớp đúng, các trang con khớp theo tiền tố.
  const selectedKeys = [...operationItems, ...adminItems, { key: REVIEW_PATH }]
    .map((item) => item.key)
    .filter((key) => (key === '/command' ? pathname === '/command' : pathname.startsWith(key)));

  const menu = (inDrawer) => (
    <Menu
      mode="inline"
      className="app-shell__sider-menu"
      aria-label="Điều hướng trung tâm điều hành"
      selectedKeys={selectedKeys}
      items={menuItems}
      // Trong Sider, Menu tự theo trạng thái thu gọn của Sider; trong ngăn kéo luôn mở đầy đủ.
      inlineCollapsed={inDrawer ? false : undefined}
      onClick={({ key }) => navigate(key)}
    />
  );

  const scopeSelect = (
    <Flex align="center" gap="small" style={{ flex: mode === 'mobile' ? '1 1 100%' : 'none' }}>
      <Typography.Text type="secondary" style={{ whiteSpace: 'nowrap' }}>
        Phạm vi
      </Typography.Text>
      <Select
        aria-label="Chọn phạm vi xem"
        style={{ flex: 1, minWidth: mode === 'mobile' ? 0 : 220 }}
        value={unitId ?? ''}
        onChange={(value) => setUnitId(value ? Number(value) : undefined)}
        options={[
          { value: '', label: global ? 'Toàn thành phố' : 'Toàn bộ phạm vi của tôi' },
          ...options.map((u) => ({ value: u.unitId, label: u.unitName })),
        ]}
      />
    </Flex>
  );

  const menuToggleIcon = mode === 'mobile' ? <MenuOutlined /> : collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />;

  return (
    <div className="app-shell">
      <Layout.Header className="app-shell__header">
        <Button
          type="text"
          icon={menuToggleIcon}
          onClick={toggleMenu}
          aria-label={mode === 'mobile' ? 'Mở menu' : collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          aria-expanded={mode === 'mobile' ? drawerOpen : !collapsed}
          aria-controls="command-navigation"
        />
        <BrandLink to="/command" subtitle="Trung tâm điều hành" />
        <Flex align="center" gap={mode === 'mobile' ? 4 : 12} style={{ marginLeft: 'auto', flex: 'none' }}>
          {mode === 'desktop' && <RealtimeStatusBadge />}
          <NotificationBell />
          <UserMenu showRealtime={mode !== 'desktop'} />
        </Flex>
      </Layout.Header>

      <div className="app-shell__body">
        {mode !== 'mobile' && (
          <Layout.Sider
            id="command-navigation"
            className="app-shell__sider"
            theme="light"
            width={SIDER_WIDTH}
            collapsedWidth={SIDER_COLLAPSED_WIDTH}
            collapsed={collapsed}
            trigger={null}
          >
            {menu(false)}
            <div style={{ padding: collapsed ? 8 : 12, borderTop: '1px solid rgba(39, 64, 62, 0.12)' }}>
              <Button
                type="text"
                block
                icon={<LogoutOutlined />}
                onClick={() => void logout()}
                aria-label={collapsed ? vi.nav.logout : undefined}
                title={collapsed ? vi.nav.logout : undefined}
                style={{ justifyContent: collapsed ? 'center' : 'flex-start' }}
              >
                {!collapsed && vi.nav.logout}
              </Button>
            </div>
          </Layout.Sider>
        )}

        <div className="app-shell__main">
          <div className="app-shell__toolbar">
            <Breadcrumb items={crumbsFor(pathname)} style={{ minWidth: 0 }} />
            {scopeSelect}
          </div>
          <main className="app-shell__content" id="main-content">
            <div className="app-shell__page">
              <div className="app-shell__card">
                <Outlet />
              </div>
            </div>
          </main>
          <AppFooter />
        </div>
      </div>

      {mode === 'mobile' && (
        <Drawer
          id="command-navigation"
          placement="left"
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          size={280}
          styles={{ body: { padding: 0, display: 'flex', flexDirection: 'column' }, wrapper: { maxWidth: '85vw' } }}
          title={<BrandLink to="/command" subtitle="Trung tâm điều hành" />}
          footer={
            <Flex vertical gap={8}>
              <RealtimeStatusBadge />
              <Button block icon={<LogoutOutlined />} onClick={() => void logout()}>
                {vi.nav.logout}
              </Button>
            </Flex>
          }
        >
          {menu(true)}
        </Drawer>
      )}
    </div>
  );
}

/** Breadcrumb: Trung tâm điều hành › mục › chi tiết. Mục "Quản trị" không có trang riêng nên không là liên kết. */
function crumbsFor(pathname) {
  const parts = pathname.split('/').filter(Boolean);
  const crumbs = [{ path: '/command', label: 'Trung tâm điều hành', link: true }];
  let path = '';
  parts.forEach((seg, i) => {
    path += `/${seg}`;
    if (i === 0) return;
    const parent = path.slice(0, path.lastIndexOf('/'));
    const label = CRUMB_LABELS[path] ?? DETAIL_LABELS[parent] ?? seg;
    crumbs.push({ path, label, link: path !== '/command/admin' });
  });
  if (pathname === '/command' || pathname === '/command/') crumbs.push({ path: '/command', label: 'Bản đồ điều hành', link: false });
  return crumbs.map((c, i) => ({
    title: i < crumbs.length - 1 && c.link ? <Link to={c.path}>{c.label}</Link> : c.label,
  }));
}
