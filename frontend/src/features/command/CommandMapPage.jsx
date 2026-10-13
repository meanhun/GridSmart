import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Checkbox, Col, Flex, List, Row, Statistic, Typography } from 'antd';
import L from 'leaflet';
import 'leaflet.heat';
import { CircleMarker, GeoJSON, Popup, useMap } from 'react-leaflet';
import {
  useGetApiDashboardGridStatus,
  useGetApiDashboardHeatmap,
  useGetApiDashboardSummary,
} from '@/api/generated/endpoints/dashboard/dashboard';
import { useGetApiGridsId } from '@/api/generated/endpoints/grids/grids';
import { useGetApiIncidents } from '@/api/generated/endpoints/incidents/incidents';
import { BaseMap } from '@/components/map/BaseMap';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { gridColor, isOpenStatus, priorityText, statusText } from '@/lib/incident-labels';
import { gridFeatures } from '@/lib/grid-geojson';
import { toNumber } from '@/lib/geo-math';
import { useUnitScope } from './UnitScopeContext';
/** Kiểu chung cho các bảng nổi phía trên bản đồ (cao hơn lớp Leaflet). */
const FLOAT = { position: 'absolute', zIndex: 500 };
/** Bản đồ điều hành toàn màn hình (FE-15): lớp bật tắt, bảng số liệu nổi, khung ô lưới, chế độ trình chiếu. */
export function CommandMapPage() {
  const { unitId } = useUnitScope();
  const [layers, setLayers] = useState({ grids: true, markers: true, heat: false });
  const [selectedGrid, setSelectedGrid] = useState(null);
  const [present, setPresent] = useState(false);
  // Điện thoại: thu gọn sẵn bảng lớp hiển thị để chừa chỗ cho bản đồ
  const [layersOpen, setLayersOpen] = useState(() => window.matchMedia?.('(min-width: 768px)').matches ?? true);
  const queryClient = useQueryClient();
  const grids = useGetApiDashboardGridStatus({ unitId });
  const summary = useGetApiDashboardSummary({ unitId });
  const heat = useGetApiDashboardHeatmap({ unitId }, { query: { enabled: layers.heat } });
  const incidents = useGetApiIncidents({ UnitId: unitId, PageSize: 200 });
  const gridDetail = useGetApiGridsId(selectedGrid ?? 0, { query: { enabled: selectedGrid !== null } });
  // Trình chiếu làm mới số liệu mỗi phút, phòng khi realtime bị gián đoạn.
  useEffect(() => {
    if (!present) return;
    const timer = window.setInterval(() => {
      void queryClient.invalidateQueries({ queryKey: ['/api/dashboard'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/incidents'] });
    }, 60_000);
    return () => window.clearInterval(timer);
  }, [present, queryClient]);
  const openIncidents = useMemo(
    () => (incidents.data?.data.items ?? []).filter((i) => isOpenStatus(i.status) && toNumber(i.latitude) !== null),
    [incidents.data],
  );
  const features = gridFeatures(grids.data?.data);
  const selectedFeatures = features.find((f) => f.properties.gridId === selectedGrid);
  const selectedIncidents = openIncidents.filter((i) => i.gridId === selectedGrid);
  const s = summary.data?.data;
  const content = (
    <div style={{ position: 'relative', flex: 1, height: '100%', width: '100%', minHeight: 0, fontSize: present ? 18 : undefined }}>
      {/* Lớp tuyệt đối lấp đầy khung: chiều cao không phụ thuộc chuỗi % của các khung cha */}
      <div style={{ position: 'absolute', inset: 0 }}>
        <BaseMap label="Bản đồ điều hành" height="100%" showLocateButton={false}>
          {layers.grids && features.length > 0 && (
            <GeoJSON
              key={`grids-${features.length}-${features.map((f) => `${f.properties.gridId}${f.properties.statusColor}`).join('|')}`}
              data={{ type: 'FeatureCollection', features }}
              style={(feature) => {
                const p = feature.properties;
                const c = gridColor(p.statusColor);
                return { color: c.fill, weight: 2, fillColor: c.fill, fillOpacity: 0.25 };
              }}
              onEachFeature={(feature, layer) => {
                const p = feature.properties;
                const c = gridColor(p.statusColor);
                layer.bindTooltip(
                  `<strong>${p.gridCode}</strong> · ${p.name}<br/>${c.label} · mở ${p.openIncidentCount}, quá hạn ${p.overdueIncidentCount}`,
                );
                layer.on('click', () => setSelectedGrid(p.gridId));
              }}
            />
          )}
          {layers.markers &&
            openIncidents.map((i) => (
              <CircleMarker
                key={i.id}
                center={[toNumber(i.latitude), toNumber(i.longitude)]}
                radius={i.isOverdue ? 9 : 7}
                pathOptions={{ color: '#FFFFFF', weight: 2, fillColor: i.isOverdue ? '#b91c1c' : '#21A691', fillOpacity: 0.9 }}
              >
                <Popup>
                  <IncidentPopup item={i} />
                </Popup>
              </CircleMarker>
            ))}
          {layers.heat && heat.data?.data && <HeatLayer points={heat.data.data} />}
        </BaseMap>
      </div>

      {/* Cột bảng nổi bên trái: số liệu rồi lớp hiển thị, xếp chồng theo chiều dọc để không đè nhau */}
      <Flex vertical gap={8} style={{ ...FLOAT, left: 12, top: 12, width: 300, maxWidth: 'calc(100% - 24px)' }}>
        <SummaryPanel loading={summary.isPending} error={summary.isError ? errorMessage(summary.error) : null} values={s} />
        <Card
          size="small"
          title="Lớp hiển thị"
          extra={
            <Button type="link" size="small" onClick={() => setLayersOpen((v) => !v)} aria-expanded={layersOpen}>
              {layersOpen ? 'Thu gọn' : 'Mở'}
            </Button>
          }
          styles={{ body: layersOpen ? undefined : { display: 'none' } }}
        >
          <Flex vertical gap={8}>
            <Checkbox checked={layers.grids} onChange={(e) => setLayers({ ...layers, grids: e.target.checked })}>
              Ô lưới theo trạng thái
            </Checkbox>
            <Checkbox checked={layers.markers} onChange={(e) => setLayers({ ...layers, markers: e.target.checked })}>
              Sự cố đang mở
            </Checkbox>
            <Checkbox checked={layers.heat} onChange={(e) => setLayers({ ...layers, heat: e.target.checked })}>
              Heatmap mật độ
            </Checkbox>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Màu ô lưới: <Typography.Text type="danger">đỏ</Typography.Text> quá hạn ·{' '}
              <Typography.Text type="warning">vàng</Typography.Text> có việc · <Typography.Text type="success">xanh</Typography.Text>{' '}
              bình thường
            </Typography.Text>
          </Flex>
        </Card>
      </Flex>

      {selectedGrid !== null && selectedFeatures && (
        <Card
          size="small"
          role="complementary"
          aria-label="Chi tiết ô lưới"
          style={{ ...FLOAT, right: 12, top: 12, bottom: 64, width: 320, maxWidth: 'calc(100% - 24px)', overflowY: 'auto' }}
          title={
            <div style={{ whiteSpace: 'normal' }}>
              <Typography.Text code>{selectedFeatures.properties.gridCode}</Typography.Text>
              <Typography.Title level={5} style={{ margin: 0 }}>
                {selectedFeatures.properties.name}
              </Typography.Title>
              <Typography.Text type="secondary" style={{ fontWeight: 'normal' }}>
                {gridColor(selectedFeatures.properties.statusColor).label}
              </Typography.Text>
            </div>
          }
          extra={
            <Button type="link" size="small" onClick={() => setSelectedGrid(null)}>
              Đóng
            </Button>
          }
        >
          <Typography.Text strong>Cán bộ phụ trách</Typography.Text>
          <List
            size="small"
            split={false}
            dataSource={gridDetail.data?.data.officers ?? []}
            rowKey="userId"
            locale={{ emptyText: gridDetail.isSuccess ? 'Chưa phân công.' : ' ' }}
            renderItem={(o) => (
              <List.Item style={{ padding: '2px 0' }}>
                {o.fullName} {o.isPrimary ? '(chính)' : ''}
              </List.Item>
            )}
            style={{ marginBottom: 16 }}
          />
          <Typography.Text strong>Sự cố đang mở ({selectedIncidents.length})</Typography.Text>
          <List
            size="small"
            split={false}
            dataSource={selectedIncidents}
            rowKey="id"
            locale={{ emptyText: 'Không có sự cố đang mở.' }}
            renderItem={(i) => (
              <List.Item style={{ padding: '4px 0' }}>
                <Link to={`/command/incidents/${i.id}`} style={{ display: 'block', width: '100%' }}>
                  <Card size="small" hoverable styles={{ body: { padding: 8 } }}>
                    <Typography.Text code>{i.trackingCode}</Typography.Text> · {i.categoryName || 'Chưa phân loại'}
                    <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                      {statusText(i.status)} · {priorityText(i.priority)}
                      {i.isOverdue ? ' · quá hạn' : ''}
                    </Typography.Text>
                  </Card>
                </Link>
              </List.Item>
            )}
          />
        </Card>
      )}

      <Button type="primary" onClick={() => setPresent((v) => !v)} style={{ ...FLOAT, left: 56, bottom: 12 }}>
        {present ? 'Thoát trình chiếu' : 'Chế độ trình chiếu'}
      </Button>
    </div>
  );
  if (present) {
    return <div style={{ position: 'fixed', inset: 0, zIndex: 1000, background: '#FFFFFF', padding: 8 }}>{content}</div>;
  }
  // Lấp đầy vùng nội dung của khung cố định (header, breadcrumb, footer đã trừ sẵn); màn hình thấp thì cuộn.
  return <div style={{ flex: 1, minHeight: 420, display: 'flex', flexDirection: 'column' }}>{content}</div>;
}
function SummaryPanel({ values, loading, error }) {
  if (error) {
    return <Alert type="error" showIcon role="alert" title={error} />;
  }
  const items = [
    ['Đang mở', values?.openIncidents],
    ['Quá hạn', values?.overdueIncidents],
    ['Chờ duyệt', values?.pendingReview],
    ['Đã xử lý hôm nay', values?.resolvedToday],
  ];
  return (
    <Card size="small" aria-busy={loading}>
      <Row gutter={[12, 8]}>
        {items.map(([label, value]) => (
          <Col key={label} span={12}>
            <Statistic title={label} value={value ?? (loading ? '…' : '—')} styles={{ content: { fontSize: 20 } }} />
          </Col>
        ))}
      </Row>
    </Card>
  );
}
function IncidentPopup({ item }) {
  return (
    <div>
      <Typography.Text code strong>
        {item.trackingCode}
      </Typography.Text>
      <p style={{ margin: 0 }}>
        {item.categoryName || 'Chưa phân loại'} · {priorityText(item.priority)}
      </p>
      <Typography.Text type="secondary" style={{ display: 'block' }}>
        {statusText(item.status)} · gửi {formatDateTime(item.createdAt)}
      </Typography.Text>
      <Link to={`/command/incidents/${item.id}`}>Mở chi tiết</Link>
    </div>
  );
}
/** Heatmap bằng leaflet.heat (FE-15). */
function HeatLayer({ points }) {
  const map = useMap();
  useEffect(() => {
    const data = points
      .map((p) => [toNumber(p.lat), toNumber(p.lon), toNumber(p.weight) ?? 1])
      .filter((p) => p[0] !== null && p[1] !== null);
    const layer = L.heatLayer(data, { radius: 25, blur: 18 }).addTo(map);
    return () => {
      layer.remove();
    };
  }, [points, map]);
  return null;
}
