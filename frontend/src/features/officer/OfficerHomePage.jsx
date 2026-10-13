import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { Alert, Button, Card, Empty, Flex, Spin, Tabs, Tag, Typography } from 'antd';
import { useGetApiOfficerIncidents } from '@/api/generated/endpoints/incidents/incidents';
import { useRealtimeEvent } from '@/features/realtime/RealtimeProvider';
import { formatDateTime, slaRemainingRatio, slaTone } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { getCurrentFix } from '@/lib/geo';
import { haversineMeters, toNumber } from '@/lib/geo-math';
const TABS = [
  { key: 'assigned', label: 'Mới giao' },
  { key: 'in_progress', label: 'Đang xử lý' },
  { key: 'done', label: 'Đã xong (7 ngày)' },
];
/** Danh sách việc của cán bộ ô lưới (FE-12). */
export function OfficerHomePage() {
  const [tab, setTab] = useState('assigned');
  const [here, setHere] = useState(null);
  const [now, setNow] = useState(() => new Date());
  const [freshAssignment, setFreshAssignment] = useState(null);
  const list = useGetApiOfficerIncidents({ tab });
  const items = useMemo(() => list.data?.data ?? [], [list.data]);
  useEffect(() => {
    getCurrentFix()
      .then(setHere)
      .catch(() => setHere(null));
    const timer = window.setInterval(() => setNow(new Date()), 30_000);
    return () => window.clearInterval(timer);
  }, []);
  // Việc mới giao: hiện ngay đầu danh sách, rung nếu thiết bị hỗ trợ (FE-12).
  useRealtimeEvent('incident.assigned', (payload) => {
    const code = payload?.trackingCode ?? null;
    setFreshAssignment(code);
    if (typeof navigator.vibrate === 'function') navigator.vibrate([200, 100, 200]);
  });
  const sorted = useMemo(() => {
    // Hạn SLA gần nhất lên đầu; việc không có hạn xuống cuối.
    const deadline = (item) => (item.slaDeadline ? Date.parse(item.slaDeadline) : Number.POSITIVE_INFINITY);
    return [...items].sort((a, b) => {
      if (freshAssignment && a.trackingCode === freshAssignment) return -1;
      if (freshAssignment && b.trackingCode === freshAssignment) return 1;
      return deadline(a) - deadline(b);
    });
  }, [items, freshAssignment]);
  return (
    <Flex vertical gap={16} component="section" aria-labelledby="officer-title">
      <Flex align="center" justify="space-between">
        <Typography.Title level={3} id="officer-title" style={{ margin: 0 }}>
          Việc của tôi
        </Typography.Title>
        <Button type="link" onClick={() => void list.refetch()}>
          Làm mới
        </Button>
      </Flex>

      <Tabs
        aria-label="Lọc việc"
        activeKey={tab}
        onChange={setTab}
        items={TABS.map((t) => ({ key: t.key, label: t.label }))}
        tabBarStyle={{ marginBottom: 0 }}
      />

      {list.isPending && (
        <Flex role="status" align="center" gap={8}>
          <Spin size="small" />
          <Typography.Text type="secondary">Đang tải việc…</Typography.Text>
        </Flex>
      )}
      {list.isError && <Alert type="error" showIcon title={errorMessage(list.error)} />}
      {list.isSuccess && sorted.length === 0 && <Empty description="Không có việc nào trong mục này." />}

      <Flex vertical gap={12}>
        {sorted.map((item) => (
          <IncidentCard key={item.id} item={item} here={here} now={now} fresh={item.trackingCode === freshAssignment} />
        ))}
      </Flex>
    </Flex>
  );
}
function IncidentCard({ item, here, now, fresh }) {
  const lat = toNumber(item.latitude);
  const lon = toNumber(item.longitude);
  const distance = here && lat !== null && lon !== null ? haversineMeters(here.latitude, here.longitude, lat, lon) : null;
  const deadline = item.slaDeadline;
  const ratio = deadline ? slaRemainingRatio(item.createdAt, deadline, now) : null;
  const tone = ratio === null ? null : slaTone(ratio);
  const toneType = tone ? { ok: 'success', warning: 'warning', overdue: 'danger' }[tone] : 'secondary';
  const toneLabel = tone ? { ok: 'Còn thời gian', warning: 'Sắp hết hạn', overdue: 'Quá hạn' }[tone] : 'Không có hạn SLA';
  return (
    <Link to={`/officer/incidents/${item.id}`} style={{ display: 'block' }}>
      <Card size="small" hoverable style={fresh ? { borderColor: '#21A691', boxShadow: '0 0 0 2px #21A691' } : undefined}>
        <Flex vertical gap={4}>
          <Flex align="flex-start" justify="space-between" gap={8}>
            <Typography.Text strong code>
              {item.trackingCode}
            </Typography.Text>
            {fresh && <Tag color="blue">Mới giao</Tag>}
          </Flex>
          <Typography.Text>{item.categoryName}</Typography.Text>
          <Typography.Text type="secondary">{item.addressText || 'Chưa có địa chỉ'}</Typography.Text>
          <Flex wrap justify="space-between" gap={8}>
            <Typography.Text type="secondary">
              {distance !== null ? `Cách ${formatDistance(distance)}` : 'Chưa xác định khoảng cách'}
            </Typography.Text>
            <Typography.Text strong type={toneType}>
              {deadline ? `${toneLabel} · hạn ${formatDateTime(deadline)}` : toneLabel}
            </Typography.Text>
          </Flex>
        </Flex>
      </Card>
    </Link>
  );
}
export function formatDistance(meters) {
  return meters < 1000 ? `${Math.round(meters)} m` : `${(meters / 1000).toFixed(1)} km`;
}
