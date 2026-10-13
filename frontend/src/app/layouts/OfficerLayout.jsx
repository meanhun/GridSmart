import { Outlet } from 'react-router';
import { Flex } from 'antd';
import { RealtimeStatusBadge } from '@/features/realtime/RealtimeStatusBadge';
import { NotificationBell } from '@/features/notifications/NotificationBell';
import { AppFooter, BrandLink, UserMenu } from './AppShell';
import { useShellMode } from './useShellMode';

/** Cổng cán bộ ô lưới (`/officer`), ưu tiên điện thoại (FE-12 đến FE-13). Header và footer cố định, chỉ nội dung cuộn. */
export function OfficerLayout() {
  const mode = useShellMode();
  return (
    <div className="app-shell app-shell--plain">
      <header className="app-shell__header">
        <div className="app-shell__header-inner">
          <BrandLink to="/officer" subtitle="Cán bộ ô lưới" />
          <Flex align="center" gap={mode === 'mobile' ? 4 : 12} style={{ marginLeft: 'auto', flex: 'none' }}>
            {mode !== 'mobile' && <RealtimeStatusBadge />}
            <NotificationBell />
            <UserMenu showRealtime={mode === 'mobile'} />
          </Flex>
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
