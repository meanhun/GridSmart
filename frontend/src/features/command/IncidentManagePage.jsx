import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { DeleteOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Descriptions, Empty, Flex, List, Row, Space, Spin, Timeline, Typography } from 'antd';
import { GeoJSON, Marker } from 'react-leaflet';
import {
  deleteApiIncidentsId,
  useGetApiIncidentsId,
  useGetApiIncidentsIdChildren,
  useGetApiIncidentsIdLogs,
} from '@/api/generated/endpoints/incidents/incidents';
import { BaseMap, pinIcon } from '@/components/map/BaseMap';
import { usePermission } from '@/features/auth/useAuth';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { isOpenStatus, priorityText, reviewReasonText, statusText } from '@/lib/incident-labels';
import { toNumber } from '@/lib/geo-math';
import { vi } from '@/locales/vi';
import { CancelForm, ClassifyForm, DispatchForm, ReassignForm, useRefreshIncidentData } from './IncidentActionForms';
/** Chi tiết sự cố cho cán bộ quản lý (FE-17). Mỗi nút chỉ hiện khi có quyền với phạm vi và trạng thái cho phép. */
export function IncidentManagePage() {
  const { id } = useParams();
  const incidentId = Number(id);
  const detail = useGetApiIncidentsId(incidentId, { query: { enabled: Number.isFinite(incidentId), retry: false } });
  const logs = useGetApiIncidentsIdLogs(incidentId, { query: { enabled: Number.isFinite(incidentId) } });
  const children = useGetApiIncidentsIdChildren(incidentId, { query: { enabled: Number.isFinite(incidentId) } });
  const refresh = useRefreshIncidentData();
  const [panel, setPanel] = useState(null);
  const [notice, setNotice] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleteError, setDeleteError] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const navigate = useNavigate();
  // Quyền theo phạm vi đơn vị của sự cố (đường dẫn dạng /1/2/).
  const unitPath = detail.data ? `/${detail.data.data.adminUnitPath.map((u) => u.id).join('/')}/` : undefined;
  const canClassify = usePermission('incident.classify', unitPath);
  const canDispatch = usePermission('incident.dispatch', unitPath);
  const canReassign = usePermission('incident.reassign', unitPath);
  const canCancel = usePermission('incident.cancel', unitPath);
  // Xóa hẳn không cần lý do: chỉ quản trị hệ thống (quyền incident.delete) mới thấy nút này.
  const canDelete = usePermission('incident.delete');
  if (detail.isPending)
    return (
      <Flex role="status" justify="center" style={{ padding: 24 }}>
        <Spin tip={vi.common.loading}>
          <div style={{ padding: 24 }} />
        </Spin>
      </Flex>
    );
  if (detail.isError) {
    return (
      <Flex vertical gap="small">
        <Alert type="error" showIcon role="alert" message={errorMessage(detail.error)} />
        <Link to="/command/incidents">{vi.common.back}</Link>
      </Flex>
    );
  }
  const d = detail.data.data;
  const status = d.status.toUpperCase();
  const open = isOpenStatus(status);
  const lat = toNumber(d.latitude);
  const lon = toNumber(d.longitude);
  const boundary = d.gridBoundary ?? null;
  const allowed = {
    classify: canClassify && open,
    dispatch: canDispatch && status === 'NEW',
    reassign: canReassign && (status === 'ASSIGNED' || status === 'IN_PROGRESS'),
    cancel: canCancel && open,
  };
  const anyAction = Object.values(allowed).some(Boolean);
  const onDone = async (message) => {
    setPanel(null);
    setNotice(message);
    await refresh();
  };
  // Trạng thái đã đổi ở nơi khác: tải lại dữ liệu và báo cho người dùng.
  const onStale = async () => {
    setPanel(null);
    await Promise.all([detail.refetch(), logs.refetch(), children.refetch()]);
  };
  const togglePanel = (p) => setPanel(panel === p ? null : p);
  const panelProps = { incidentId, onStale };
  const infoItems = [
    { key: 'unit', label: 'Đơn vị', children: d.adminUnitPath.map((u) => u.name).join(' › ') || '—' },
    { key: 'grid', label: 'Ô lưới', children: d.gridCode ? `${d.gridCode} · ${d.gridName}` : 'Chưa thuộc ô lưới' },
    { key: 'officer', label: 'Cán bộ', children: d.assignedOfficerName || 'Chưa giao' },
    { key: 'sla', label: 'Hạn SLA', children: d.slaDeadline ? formatDateTime(d.slaDeadline) : '—' },
    { key: 'address', label: 'Địa chỉ', children: d.addressText || '—' },
    { key: 'phone', label: 'SĐT phản ánh', children: d.reporterPhone || '—' },
  ];
  if (d.isManualReview) infoItems.push({ key: 'review', label: 'Chờ duyệt', children: reviewReasonText(d.reviewReason) });
  if (d.parentTrackingCode) {
    infoItems.push({
      key: 'parent',
      label: 'Gộp vào',
      children: <Link to={`/command/incidents/${d.parentIncidentId}`}>{d.parentTrackingCode}</Link>,
    });
  }
  return (
    <Flex vertical gap="large" component="article" aria-labelledby="manage-title">
      <Flex wrap justify="space-between" align="flex-start" gap="middle" component="header">
        <div>
          <Typography.Text type="secondary" style={{ fontFamily: 'monospace' }}>
            {d.trackingCode}
          </Typography.Text>
          <Typography.Title level={3} id="manage-title" style={{ margin: 0 }}>
            {d.categoryName || 'Chưa phân loại'}
          </Typography.Title>
          <Typography.Text type="secondary">
            {statusText(d.status)} · {priorityText(d.priority)} · gửi {formatDateTime(d.createdAt)}
            {d.isOverdue ? ' · quá hạn' : ''}
          </Typography.Text>
        </div>
        <Link to="/command/incidents">Danh sách sự cố</Link>
      </Flex>

      {notice && <Alert type="success" showIcon role="status" message={notice} />}

      <Row gutter={[24, 24]}>
        <Col xs={24} lg={12}>
          <Card title="Thông tin" id="info-title">
            <Flex vertical gap="middle">
              <Descriptions column={1} size="small" items={infoItems} />
              {d.description && <Typography.Paragraph style={{ margin: 0 }}>{d.description}</Typography.Paragraph>}
              {d.images.length > 0 && (
                <Row gutter={[8, 8]}>
                  {d.images.map((img) => (
                    <Col key={img.id} span={12}>
                      <figure style={{ margin: 0 }}>
                        <img
                          src={img.url}
                          alt={img.type === 'AFTER' ? 'Ảnh sau xử lý' : 'Ảnh hiện trường'}
                          style={{ width: '100%', borderRadius: 6 }}
                        />
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {img.type === 'AFTER' ? 'Sau xử lý' : 'Hiện trường'}
                        </Typography.Text>
                      </figure>
                    </Col>
                  ))}
                </Row>
              )}
            </Flex>
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card title="Vị trí" id="map-title">
            {lat !== null && lon !== null ? (
              <BaseMap label="Vị trí sự cố" height="300px" center={[lat, lon]} zoom={16} showLocateButton={false}>
                <Marker position={[lat, lon]} icon={pinIcon} />
                {boundary && <GeoJSON data={boundary} style={{ color: '#21A691', weight: 2, fillOpacity: 0.08 }} />}
              </BaseMap>
            ) : (
              <Typography.Text type="secondary">Sự cố chưa có tọa độ.</Typography.Text>
            )}
          </Card>
        </Col>
      </Row>

      {anyAction && (
        <Card title="Thao tác" id="actions-title">
          <Flex vertical gap="middle">
            <Space wrap>
              {allowed.classify && (
                <Button
                  aria-pressed={panel === 'classify'}
                  type={panel === 'classify' ? 'primary' : 'default'}
                  ghost={panel === 'classify'}
                  onClick={() => togglePanel('classify')}
                >
                  Phân loại
                </Button>
              )}
              {allowed.dispatch && (
                <Button
                  aria-pressed={panel === 'dispatch'}
                  type={panel === 'dispatch' ? 'primary' : 'default'}
                  ghost={panel === 'dispatch'}
                  onClick={() => togglePanel('dispatch')}
                >
                  Điều phối
                </Button>
              )}
              {allowed.reassign && (
                <Button
                  aria-pressed={panel === 'reassign'}
                  type={panel === 'reassign' ? 'primary' : 'default'}
                  ghost={panel === 'reassign'}
                  onClick={() => togglePanel('reassign')}
                >
                  Giao lại
                </Button>
              )}
              {allowed.cancel && (
                <Button
                  danger
                  aria-pressed={panel === 'cancel'}
                  type={panel === 'cancel' ? 'primary' : 'default'}
                  ghost={panel === 'cancel'}
                  onClick={() => togglePanel('cancel')}
                >
                  Hủy
                </Button>
              )}
            </Space>
            {panel === 'classify' && <ClassifyForm {...panelProps} onDone={() => void onDone('Đã lưu phân loại.')} />}
            {panel === 'dispatch' && <DispatchForm {...panelProps} onDone={() => void onDone('Đã điều phối cán bộ.')} />}
            {panel === 'reassign' && <ReassignForm {...panelProps} onDone={() => void onDone('Đã giao lại sự cố.')} />}
            {panel === 'cancel' && <CancelForm {...panelProps} onDone={() => void onDone('Đã hủy sự cố.')} />}
          </Flex>
        </Card>
      )}

      {canDelete && (
        <Card title={<Typography.Text type="danger">Xóa phản ánh</Typography.Text>} id="delete-title">
          <Flex vertical gap="small">
            <Typography.Paragraph style={{ margin: 0 }}>
              Xóa hẳn phản ánh cùng nhật ký, thông báo, đánh giá và các phản ánh trùng đã gộp vào nó. Không cần nhập lý do, nhưng không thể
              hoàn tác.
            </Typography.Paragraph>
            {deleteError && <Alert type="error" showIcon role="alert" message={deleteError} />}
            {confirmDelete ? (
              <Space wrap>
                <Button
                  type="primary"
                  danger
                  loading={deleting}
                  icon={<DeleteOutlined aria-hidden="true" />}
                  onClick={async () => {
                    setDeleting(true);
                    setDeleteError(null);
                    try {
                      await deleteApiIncidentsId(incidentId);
                      await refresh();
                      navigate('/command/incidents', { replace: true });
                    } catch (err) {
                      setDeleteError(errorMessage(err));
                      setDeleting(false);
                    }
                  }}
                >
                  {deleting ? 'Đang xóa…' : 'Xác nhận xóa'}
                </Button>
                <Button onClick={() => setConfirmDelete(false)}>Không xóa</Button>
              </Space>
            ) : (
              <div>
                <Button danger icon={<DeleteOutlined aria-hidden="true" />} onClick={() => setConfirmDelete(true)}>
                  Xóa phản ánh
                </Button>
              </div>
            )}
          </Flex>
        </Card>
      )}

      <Card title={`Phản ánh gộp vào sự cố này (${d.childCount})`} id="children-title" size="small">
        {children.isSuccess && children.data.data.length === 0 && (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có phản ánh con." />
        )}
        <List
          size="small"
          dataSource={children.data?.data ?? []}
          rowKey="id"
          renderItem={(c) => (
            <List.Item key={c.id}>
              <Space>
                <Link style={{ fontFamily: 'monospace' }} to={`/command/incidents/${c.id}`}>
                  {c.trackingCode}
                </Link>
                <Typography.Text type="secondary">{statusText(c.status)}</Typography.Text>
              </Space>
            </List.Item>
          )}
          locale={{ emptyText: ' ' }}
        />
      </Card>

      <Card title="Nhật ký xử lý" id="logs-title" size="small">
        {logs.isError && <Alert type="error" showIcon role="alert" message={errorMessage(logs.error)} style={{ marginBottom: 12 }} />}
        <Timeline
          items={(logs.data?.data ?? []).map((l) => ({
            key: l.id,
            content: (
              <>
                <div>{l.actionNote}</div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {formatDateTime(l.createdAt)} · {l.actionByName || 'Hệ thống'}
                </Typography.Text>
              </>
            ),
          }))}
        />
      </Card>
    </Flex>
  );
}
