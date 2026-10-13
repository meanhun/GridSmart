import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router';
import { App, Badge, Button, Flex, List, Popover, Spin, Typography } from 'antd';
import { BellOutlined } from '@ant-design/icons';
import {
  getGetApiNotificationsQueryKey,
  getGetApiNotificationsUnreadCountQueryKey,
  postApiNotificationsIdRead,
  postApiNotificationsReadAll,
  useGetApiNotifications,
  useGetApiNotificationsUnreadCount,
} from '@/api/generated/endpoints/notifications/notifications';
import { useRealtimeEvent } from '@/features/realtime/RealtimeProvider';
import { useAuth } from '@/features/auth/useAuth';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { vi } from '@/locales/vi';
/** Nhóm thông báo cần nổi bật màu đỏ (FE-19). */
const URGENT_TYPES = ['ESCALATION', 'OVERDUE'];
/** Chuông thông báo: số chưa đọc, danh sách thả xuống, đánh dấu đã đọc (FE-19). Dùng chung cho cổng cán bộ và điều hành. */
export function NotificationBell() {
  const { status } = useAuth();
  const { message } = App.useApp();
  const enabled = status === 'authenticated';
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const unread = useGetApiNotificationsUnreadCount({ query: { enabled } });
  const list = useGetApiNotifications({ page: 1, pageSize: 20 }, { query: { enabled: enabled && open } });
  const count = Number(unread.data?.data.count ?? 0);
  // Toast khi có thông báo mới đến realtime.
  useRealtimeEvent('notification.created', (payload) => {
    const title = payload?.title;
    if (title) message.info({ content: title, duration: 5 });
  });
  if (!enabled) return null;
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: getGetApiNotificationsUnreadCountQueryKey() });
    await queryClient.invalidateQueries({ queryKey: getGetApiNotificationsQueryKey() });
  };
  const markRead = async (item) => {
    if (item.isRead) return;
    try {
      await postApiNotificationsIdRead(Number(item.id));
      await refresh();
    } catch (err) {
      message.error({ content: errorMessage(err), duration: 5 });
    }
  };
  const readAll = async () => {
    try {
      await postApiNotificationsReadAll();
      await refresh();
    } catch (err) {
      message.error({ content: errorMessage(err), duration: 5 });
    }
  };
  const items = list.data?.data.items ?? [];
  const panel = (
    <div style={{ width: 320, maxWidth: '85vw' }}>
      <Flex justify="space-between" align="center" style={{ marginBottom: 8 }}>
        <Typography.Text strong>{vi.notifications.title}</Typography.Text>
        <Button type="link" size="small" onClick={() => void readAll()} disabled={count === 0}>
          {vi.notifications.readAll}
        </Button>
      </Flex>
      <div style={{ maxHeight: 384, overflowY: 'auto' }}>
        {list.isPending && (
          <Flex justify="center" gap={8} style={{ padding: 12 }}>
            <Spin size="small" />
            <Typography.Text type="secondary">{vi.common.loading}</Typography.Text>
          </Flex>
        )}
        <List
          size="small"
          dataSource={items}
          rowKey="id"
          locale={{ emptyText: list.isSuccess ? vi.notifications.empty : ' ' }}
          renderItem={(item) => {
            const urgent = URGENT_TYPES.includes(item.type);
            return (
              <List.Item key={item.id} style={{ padding: 0 }}>
                <Link
                  to={item.incidentId ? `/officer/incidents/${item.incidentId}` : '#'}
                  onClick={() => {
                    void markRead(item);
                    setOpen(false);
                  }}
                  style={{ display: 'block', width: '100%', padding: '8px 4px' }}
                >
                  <Typography.Text type={urgent ? 'danger' : item.isRead ? 'secondary' : undefined} strong={!item.isRead}>
                    {item.title}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                    {item.content}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, opacity: 0.7 }}>
                    {formatDateTime(item.createdAt)}
                  </Typography.Text>
                </Link>
              </List.Item>
            );
          }}
        />
      </div>
    </div>
  );
  return (
    <Popover content={panel} trigger="click" open={open} onOpenChange={setOpen} placement="bottomRight" arrow={false}>
      <Badge count={count} overflowCount={99} size="small">
        <Button
          type="text"
          icon={<BellOutlined />}
          aria-label={`${vi.notifications.title}, ${count} chưa đọc`}
          aria-expanded={open}
        />
      </Badge>
    </Popover>
  );
}
