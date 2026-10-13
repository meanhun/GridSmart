import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Form, Input, Row, Select, Typography } from 'antd';
import {
  postApiIncidentsIdCancel,
  postApiIncidentsIdClassify,
  postApiIncidentsIdDispatch,
  postApiIncidentsIdReassign,
} from '@/api/generated/endpoints/incidents/incidents';
import { useGetApiCategories } from '@/api/generated/endpoints/config/config';
import { useGetApiGrids } from '@/api/generated/endpoints/grids/grids';
import { useGetApiOfficers } from '@/api/generated/endpoints/incidents/incidents';
import { isApiError } from '@/api/errors';
import { errorMessage } from '@/lib/error-messages';
import { gridFeatures } from '@/lib/grid-geojson';
import { PRIORITY_OPTIONS } from '@/lib/incident-labels';
/** Làm mới mọi dữ liệu liên quan sau khi đổi sự cố. */
export function useRefreshIncidentData() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all(
      ['/api/incidents', '/api/officer/incidents', '/api/dashboard', '/api/grids', '/api/notifications'].map((key) =>
        queryClient.invalidateQueries({ queryKey: [key] }),
      ),
    );
}
function FormShell({ title, children, error, onSubmit, submitLabel, tone = 'brand', busy = false }) {
  return (
    <Card size="small" title={title}>
      <form onSubmit={onSubmit} aria-label={title}>
        <Form component={false} layout="vertical">
          {children}
          {error && <Alert type="error" showIcon role="alert" message={error} style={{ marginBottom: 12 }} />}
          <Button type="primary" danger={tone === 'danger'} htmlType="submit" loading={busy}>
            {submitLabel}
          </Button>
        </Form>
      </form>
    </Card>
  );
}
/** Hook gọi thao tác và xử lý lỗi thống nhất. Trả true nếu thành công. */
function useAction(onDone, onStale) {
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const run = async (action) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      onDone();
    } catch (err) {
      setError(errorMessage(err));
      if (isApiError(err) && err.code === 'INVALID_STATUS_TRANSITION') onStale();
    } finally {
      setBusy(false);
    }
  };
  return { error, busy, run, setError };
}
/** Hai cột khi compact (hàng chờ duyệt), một cột khi không; xuống một cột ở màn hình hẹp. */
function FieldRow({ compact, children }) {
  const items = Array.isArray(children) ? children : [children];
  return (
    <Row gutter={12}>
      {items.map((child, index) => (
        <Col key={index} xs={24} sm={compact ? 12 : 24}>
          {child}
        </Col>
      ))}
    </Row>
  );
}
/** Phân loại: chọn danh mục và mức ưu tiên (incident.classify). */
export function ClassifyForm({ incidentId, onDone, onStale, compact }) {
  const categories = useGetApiCategories();
  const [categoryId, setCategoryId] = useState('');
  const [priority, setPriority] = useState('2');
  const action = useAction(onDone, onStale);
  const options = categories.data?.data.filter((c) => c.isActive) ?? [];
  const submit = (e) => {
    e.preventDefault();
    if (!categoryId) {
      action.setError('Hãy chọn danh mục.');
      return;
    }
    void action.run(() => postApiIncidentsIdClassify(incidentId, { categoryId: Number(categoryId), priority: Number(priority) }));
  };
  return (
    <FormShell
      title="Phân loại"
      error={action.error}
      onSubmit={submit}
      submitLabel={action.busy ? 'Đang lưu…' : 'Lưu phân loại'}
      busy={action.busy}
    >
      <FieldRow compact={compact}>
        <Form.Item label="Danh mục" htmlFor={`cat-${incidentId}`}>
          <Select
            id={`cat-${incidentId}`}
            placeholder="Chọn danh mục"
            value={categoryId || undefined}
            onChange={(v) => setCategoryId(v ?? '')}
            options={options.map((c) => ({ value: String(c.id), label: c.name }))}
          />
        </Form.Item>
        <Form.Item label="Mức ưu tiên" htmlFor={`pri-${incidentId}`}>
          <Select
            id={`pri-${incidentId}`}
            value={priority}
            onChange={(v) => setPriority(v)}
            options={PRIORITY_OPTIONS.map((p) => ({ value: String(p.value), label: p.label }))}
          />
        </Form.Item>
      </FieldRow>
    </FormShell>
  );
}
/** Điều phối: chọn ô lưới và cán bộ (incident.dispatch). Danh sách cán bộ kèm số việc đang mở. */
export function DispatchForm({ incidentId, onDone, onStale, compact }) {
  const grids = useGetApiGrids();
  const [gridId, setGridId] = useState('');
  const [officerId, setOfficerId] = useState('');
  const officers = useGetApiOfficers({ gridId: gridId ? Number(gridId) : undefined }, { query: { enabled: gridId !== '' } });
  const action = useAction(onDone, onStale);
  const gridOptions = gridFeatures(grids.data?.data);
  const submit = (e) => {
    e.preventDefault();
    if (!gridId || !officerId) {
      action.setError('Hãy chọn ô lưới và cán bộ.');
      return;
    }
    void action.run(() => postApiIncidentsIdDispatch(incidentId, { gridId: Number(gridId), officerId: Number(officerId) }));
  };
  return (
    <FormShell
      title="Điều phối"
      error={action.error}
      onSubmit={submit}
      submitLabel={action.busy ? 'Đang điều phối…' : 'Điều phối'}
      busy={action.busy}
    >
      <FieldRow compact={compact}>
        <Form.Item label="Ô lưới" htmlFor={`grid-${incidentId}`}>
          <Select
            id={`grid-${incidentId}`}
            placeholder="Chọn ô lưới"
            value={gridId || undefined}
            onChange={(v) => {
              setGridId(v ?? '');
              setOfficerId('');
            }}
            options={gridOptions.map((f) => ({
              value: String(f.properties.gridId),
              label: `${f.properties.gridCode} · ${f.properties.name}`,
            }))}
          />
        </Form.Item>
        <Form.Item label="Cán bộ" htmlFor={`off-${incidentId}`}>
          <Select
            id={`off-${incidentId}`}
            placeholder={gridId ? 'Chọn cán bộ' : 'Chọn ô lưới trước'}
            value={officerId || undefined}
            disabled={!gridId}
            onChange={(v) => setOfficerId(v ?? '')}
            options={(officers.data?.data ?? []).map((o) => ({
              value: String(o.id),
              label: `${o.fullName} · đang mở ${o.openIncidentCount} việc`,
            }))}
          />
        </Form.Item>
      </FieldRow>
    </FormShell>
  );
}
/** Giao lại cho cán bộ khác, bắt buộc nhập lý do (incident.reassign). */
export function ReassignForm({ incidentId, onDone, onStale, compact }) {
  const officers = useGetApiOfficers();
  const [officerId, setOfficerId] = useState('');
  const [reason, setReason] = useState('');
  const action = useAction(onDone, onStale);
  const submit = (e) => {
    e.preventDefault();
    if (!officerId) return action.setError('Hãy chọn cán bộ.');
    if (!reason.trim()) return action.setError('Hãy nhập lý do giao lại.');
    void action.run(() => postApiIncidentsIdReassign(incidentId, { officerId: Number(officerId), reason: reason.trim() }));
  };
  return (
    <FormShell
      title="Giao lại"
      error={action.error}
      onSubmit={submit}
      submitLabel={action.busy ? 'Đang giao lại…' : 'Giao lại'}
      busy={action.busy}
    >
      <FieldRow compact={compact}>
        <Form.Item label="Cán bộ mới" htmlFor={`re-${incidentId}`}>
          <Select
            id={`re-${incidentId}`}
            placeholder="Chọn cán bộ"
            value={officerId || undefined}
            onChange={(v) => setOfficerId(v ?? '')}
            options={(officers.data?.data ?? []).map((o) => ({
              value: String(o.id),
              label: `${o.fullName} · đang mở ${o.openIncidentCount} việc`,
            }))}
          />
        </Form.Item>
        <Form.Item label="Lý do" htmlFor={`rr-${incidentId}`}>
          <Input id={`rr-${incidentId}`} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </Form.Item>
      </FieldRow>
    </FormShell>
  );
}
/** Hủy sự cố, bắt buộc lý do. Hủy cả ticket con (incident.cancel). */
export function CancelForm({ incidentId, onDone, onStale }) {
  const [reason, setReason] = useState('');
  const action = useAction(onDone, onStale);
  const submit = (e) => {
    e.preventDefault();
    if (!reason.trim()) return action.setError('Hãy nhập lý do hủy.');
    void action.run(() => postApiIncidentsIdCancel(incidentId, { reason: reason.trim() }));
  };
  return (
    <FormShell
      title="Hủy sự cố"
      error={action.error}
      onSubmit={submit}
      submitLabel={action.busy ? 'Đang hủy…' : 'Xác nhận hủy'}
      tone="danger"
      busy={action.busy}
    >
      <Typography.Paragraph>Hủy sự cố cũng hủy toàn bộ phản ánh con đã gộp vào nó.</Typography.Paragraph>
      <Form.Item label="Lý do hủy" htmlFor={`cx-${incidentId}`}>
        <Input.TextArea id={`cx-${incidentId}`} rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Form.Item>
    </FormShell>
  );
}
