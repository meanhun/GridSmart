import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { GeoJSON, Marker } from 'react-leaflet';
import { Alert, Button, Card, Col, Descriptions, Flex, Input, Row, Spin, Tag, Typography } from 'antd';
import {
  getGetApiIncidentsIdQueryKey,
  postApiIncidentsIdAccept,
  postApiIncidentsIdResolve,
  postApiIncidentsIdReturn,
  useGetApiIncidentsId,
} from '@/api/generated/endpoints/incidents/incidents';
import { BaseMap, pinIcon } from '@/components/map/BaseMap';
import { useAuth, usePermission } from '@/features/auth/useAuth';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { getCurrentFix } from '@/lib/geo';
import { pointInGeometry, toNumber } from '@/lib/geo-math';
import { compressForUpload } from '@/lib/image';
import { statusLabel } from '@/lib/incident-status';
import { vi } from '@/locales/vi';
/** Chi tiết và xử lý sự cố của cán bộ (FE-13). Nút chỉ hiện khi đúng trạng thái và đúng người được giao. */
export function OfficerIncidentPage() {
  const { id } = useParams();
  const incidentId = Number(id);
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const detail = useGetApiIncidentsId(incidentId, { query: { enabled: Number.isFinite(incidentId), retry: false } });
  const canAccept = usePermission('incident.accept');
  const canResolve = usePermission('incident.resolve');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  if (detail.isPending)
    return (
      <Flex role="status" align="center" gap={8}>
        <Spin size="small" />
        <Typography.Text type="secondary">{vi.common.loading}</Typography.Text>
      </Flex>
    );
  if (detail.isError) {
    return (
      <Flex vertical gap={12}>
        <Alert type="error" showIcon title={errorMessage(detail.error)} />
        <Link to="/officer">{vi.common.back}</Link>
      </Flex>
    );
  }
  const d = detail.data.data;
  const mine = user !== null && d.assignedOfficerId === user.id;
  const status = d.status.toUpperCase();
  const lat = toNumber(d.latitude);
  const lon = toNumber(d.longitude);
  const boundary = d.gridBoundary ?? null;
  const refreshAll = async () => {
    await queryClient.invalidateQueries({ queryKey: getGetApiIncidentsIdQueryKey(incidentId) });
    await queryClient.invalidateQueries({ queryKey: ['/api/officer/incidents'] });
  };
  const run = async (action) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      await refreshAll();
      return true;
    } catch (err) {
      setError(errorMessage(err));
      // Trạng thái đã đổi ở nơi khác: tải lại để hiển thị đúng.
      if (typeof err === 'object' && err !== null && err.code === 'INVALID_STATUS_TRANSITION') {
        await detail.refetch();
      }
      return false;
    } finally {
      setBusy(false);
    }
  };
  const accept = () => run(() => postApiIncidentsIdAccept(incidentId));
  const slaItems = [{ key: 'sla', label: 'Hạn SLA', children: d.slaDeadline ? formatDateTime(d.slaDeadline) : 'Không có' }];
  if (d.isOverdue) {
    slaItems.push({ key: 'overdue', label: 'Tình trạng', children: <Tag color="error">Quá hạn</Tag> });
  }
  return (
    <Flex vertical gap={20} component="article" aria-labelledby="incident-title">
      <header>
        <Typography.Text type="secondary" code>
          {d.trackingCode}
        </Typography.Text>
        <Typography.Title level={3} id="incident-title" style={{ margin: 0 }}>
          {d.categoryName || 'Chưa phân loại'}
        </Typography.Title>
        <Typography.Text type="secondary">
          {statusLabel(d.status)} · gửi lúc {formatDateTime(d.createdAt)}
        </Typography.Text>
      </header>

      {d.images.length > 0 && (
        <Row gutter={[8, 8]}>
          {d.images.map((img) => (
            <Col key={img.id} xs={12}>
              <img
                src={img.url}
                alt={img.type === 'AFTER' ? 'Ảnh sau xử lý' : 'Ảnh hiện trường'}
                style={{ width: '100%', borderRadius: 8 }}
              />
            </Col>
          ))}
        </Row>
      )}

      {d.description && <Typography.Paragraph style={{ margin: 0 }}>{d.description}</Typography.Paragraph>}
      <Typography.Text type="secondary">Địa chỉ: {d.addressText || 'Chưa xác định'}</Typography.Text>

      {lat !== null && lon !== null && (
        <BaseMap label="Vị trí sự cố" height="240px" center={[lat, lon]} zoom={16} showLocateButton={false}>
          <Marker position={[lat, lon]} icon={pinIcon} />
          {boundary && <GeoJSON data={boundary} style={{ color: '#21A691', weight: 2, fillOpacity: 0.08 }} />}
        </BaseMap>
      )}

      {lat !== null && lon !== null && (
        <div>
          <Button href={`https://www.google.com/maps/dir/?api=1&destination=${lat},${lon}`} target="_blank" rel="noopener noreferrer">
            Chỉ đường
          </Button>
        </div>
      )}

      <Descriptions size="small" column={1} items={slaItems} />

      {error && <Alert type="error" showIcon title={error} />}

      {mine && status === 'ASSIGNED' && canAccept && (
        <Button type="primary" size="large" block disabled={busy} onClick={() => void accept()}>
          Nhận việc
        </Button>
      )}

      {mine && status === 'IN_PROGRESS' && canResolve && (
        <ResolvePanel incidentId={incidentId} boundary={boundary} busy={busy} onDone={() => void refreshAll()} onError={setError} />
      )}

      {mine && status === 'IN_PROGRESS' && canAccept && <ReturnPanel incidentId={incidentId} onDone={refreshAll} onError={setError} />}

      <div style={{ textAlign: 'center' }}>
        <Link to="/officer">{vi.common.back}</Link>
      </div>
    </Flex>
  );
}
/** Nghiệm thu: chụp ảnh After, lấy GPS độ chính xác cao, cảnh báo nếu ngoài ô lưới (FE-13). */
function ResolvePanel({ incidentId, boundary, busy, onDone, onError }) {
  const cameraRef = useRef(null);
  const [photo, setPhoto] = useState(null);
  const [fix, setFix] = useState(null);
  const [fixError, setFixError] = useState(false);
  const [note, setNote] = useState('');
  const [sending, setSending] = useState(false);
  const locate = () => {
    setFixError(false);
    getCurrentFix(15_000)
      .then(setFix)
      .catch(() => setFixError(true));
  };
  useEffect(locate, []);
  const outsideGrid = fix && boundary ? !pointInGeometry(fix.latitude, fix.longitude, boundary) : false;
  const submit = async () => {
    if (!photo) {
      onError('Hãy chụp ảnh After tại hiện trường.');
      return;
    }
    if (!fix) {
      onError('Chưa lấy được vị trí. Hãy thử lấy lại vị trí.');
      return;
    }
    setSending(true);
    onError(null);
    try {
      const image = await compressForUpload(photo);
      await postApiIncidentsIdResolve(incidentId, {
        Image: image,
        Latitude: fix.latitude,
        Longitude: fix.longitude,
        Note: note.trim() || undefined,
      });
      setPhoto(null);
      setNote('');
      onDone();
    } catch (err) {
      // Giữ nguyên ảnh đã chụp để cán bộ di chuyển tới đúng vị trí rồi gửi lại.
      onError(errorMessage(err));
    } finally {
      setSending(false);
    }
  };
  return (
    <Card size="small" title={<span id="resolve-title">Báo đã xử lý</span>} aria-labelledby="resolve-title">
      <Flex vertical gap={12}>
        <Button block onClick={() => cameraRef.current?.click()}>
          {photo ? 'Chụp lại ảnh After' : 'Chụp ảnh After'}
        </Button>
        <input
          ref={cameraRef}
          type="file"
          accept="image/*"
          capture="environment"

          style={{ display: 'none' }}
          aria-label="Chụp ảnh After bằng camera"
          onChange={(e) => setPhoto(e.target.files?.[0] ?? null)}
        />
        {photo && <Typography.Text type="secondary">Đã chọn ảnh: {photo.name}</Typography.Text>}

        <Flex vertical gap={8} aria-live="polite">
          {fix && <Typography.Text>Vị trí hiện tại, sai số khoảng ± {Math.round(fix.accuracy)} m.</Typography.Text>}
          {fixError && (
            <Typography.Text type="danger">
              Không lấy được vị trí.{' '}
              <Button type="link" size="small" onClick={locate} style={{ padding: 0 }}>
                Lấy lại vị trí
              </Button>
            </Typography.Text>
          )}
          {outsideGrid && (
            <Alert
              type="warning"
              showIcon
              title="Vị trí hiện tại nằm ngoài ô lưới được giao. Hãy di chuyển tới đúng hiện trường trước khi gửi."
            />
          )}
        </Flex>

        <div>
          <label htmlFor="resolve-note" style={{ display: 'block', marginBottom: 4 }}>
            Ghi chú xử lý (tùy chọn)
          </label>
          <Input.TextArea id="resolve-note" rows={2} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
        </div>

        <Button type="primary" size="large" block disabled={busy || sending} loading={sending} onClick={() => void submit()}>
          {sending ? 'Đang gửi…' : 'Gửi nghiệm thu'}
        </Button>
      </Flex>
    </Card>
  );
}
/** Trả lại sự cố, bắt buộc có lý do (FE-13). */
function ReturnPanel({ incidentId, onDone, onError }) {
  const [reason, setReason] = useState('');
  const [open, setOpen] = useState(false);
  const [sending, setSending] = useState(false);
  const submit = async () => {
    if (!reason.trim()) {
      onError('Hãy nhập lý do trả lại.');
      return;
    }
    setSending(true);
    onError(null);
    try {
      await postApiIncidentsIdReturn(incidentId, { reason: reason.trim() });
      setOpen(false);
      setReason('');
      await onDone();
    } catch (err) {
      onError(errorMessage(err));
    } finally {
      setSending(false);
    }
  };
  if (!open) {
    return (
      <Button danger block onClick={() => setOpen(true)}>
        Trả lại
      </Button>
    );
  }
  return (
    <Card size="small" title={<span id="return-title">Trả lại sự cố</span>} aria-labelledby="return-title">
      <Flex vertical gap={12}>
        <label htmlFor="return-reason">Lý do (bắt buộc)</label>
        <Input.TextArea id="return-reason" rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
        <Flex gap={8}>
          <Button type="primary" danger style={{ flex: 1 }} disabled={sending} loading={sending} onClick={() => void submit()}>
            Xác nhận trả lại
          </Button>
          <Button style={{ flex: 1 }} onClick={() => setOpen(false)}>
            Hủy
          </Button>
        </Flex>
      </Flex>
    </Card>
  );
}
