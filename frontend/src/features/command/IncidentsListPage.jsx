import { Link, useSearchParams } from 'react-router';
import { Alert, Card, Checkbox, Col, DatePicker, Flex, Form, Input, Row, Select, Table, Typography } from 'antd';
import dayjs from 'dayjs';
import { useGetApiIncidents } from '@/api/generated/endpoints/incidents/incidents';
import { useGetApiCategories } from '@/api/generated/endpoints/config/config';
import { useGetApiGrids } from '@/api/generated/endpoints/grids/grids';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { gridFeatures } from '@/lib/grid-geojson';
import { PRIORITY_OPTIONS, statusText } from '@/lib/incident-labels';
import { useUnitScope } from './UnitScopeContext';
const STATUS_OPTIONS = ['NEW', 'ASSIGNED', 'IN_PROGRESS', 'RESOLVED', 'CANCELLED'];
const PAGE_SIZE = 20;
/** Danh sách sự cố phân trang phía server, bộ lọc lưu trên URL để chia sẻ được (FE-16). */
export function IncidentsListPage() {
  const [params, setParams] = useSearchParams();
  const { unitId: scopeUnit, options } = useUnitScope();
  const categories = useGetApiCategories();
  const grids = useGetApiGrids();
  const get = (key) => params.get(key) ?? '';
  const page = Math.max(1, Number(params.get('page') ?? 1));
  const unitFilter = get('unitId') ? Number(get('unitId')) : scopeUnit;
  const query = {
    Q: get('q') || undefined,
    Status: get('status') || undefined,
    UnitId: unitFilter,
    GridId: get('gridId') ? Number(get('gridId')) : undefined,
    CategoryId: get('categoryId') ? Number(get('categoryId')) : undefined,
    Priority: get('priority') ? Number(get('priority')) : undefined,
    IsOverdue: get('overdue') === '1' ? true : undefined,
    IsManualReview: get('review') === '1' ? true : undefined,
    From: get('from') || undefined,
    To: get('to') || undefined,
    IncludeChildren: true,
    Page: page,
    PageSize: PAGE_SIZE,
  };
  const list = useGetApiIncidents(query);
  const update = (key, value) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    next.delete('page');
    setParams(next, { replace: true });
  };
  const goPage = (p) => {
    const next = new URLSearchParams(params);
    next.set('page', String(p));
    setParams(next, { replace: true });
  };
  const data = list.data?.data;
  const gridOptions = gridFeatures(grids.data?.data);
  const all = { value: '', label: 'Tất cả' };
  const columns = [
    {
      title: 'Mã',
      dataIndex: 'trackingCode',
      render: (code, i) => (
        <Link style={{ fontFamily: 'monospace' }} to={`/command/incidents/${i.id}`}>
          {code}
        </Link>
      ),
    },
    { title: 'Danh mục', dataIndex: 'categoryName', render: (v) => v || 'Chưa phân loại' },
    { title: 'Đơn vị', dataIndex: 'adminUnitName' },
    { title: 'Cán bộ', dataIndex: 'assignedOfficerName', render: (v) => v || '—' },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      render: (v, i) => (
        <>
          {statusText(v)}
          {i.isManualReview ? ' · chờ duyệt' : ''}
        </>
      ),
    },
    {
      title: 'SLA',
      dataIndex: 'slaRemainingSeconds',
      render: (v, i) =>
        i.isOverdue ? <Typography.Text type="danger" strong>Quá hạn</Typography.Text> : slaText(v),
    },
    { title: 'Trùng', dataIndex: 'duplicateCount', render: (v) => v ?? 0 },
    { title: 'Tạo lúc', dataIndex: 'createdAt', render: (v) => formatDateTime(v) },
  ];
  return (
    <Flex vertical gap="middle" component="section" aria-labelledby="list-title">
      <Typography.Title level={3} id="list-title" style={{ margin: 0 }}>
        Sự cố
      </Typography.Title>

      <Card size="small">
        <Form component="div" layout="vertical" role="search">
          <Row gutter={[12, 0]}>
            <Col xs={24} md={12}>
              <Form.Item label="Tìm theo mã hoặc mô tả">
                <Input
                  defaultValue={get('q')}
                  onPressEnter={(e) => update('q', e.target.value)}
                  onBlur={(e) => update('q', e.target.value)}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Trạng thái">
                <Select
                  value={get('status')}
                  onChange={(v) => update('status', v)}
                  options={[all, ...STATUS_OPTIONS.map((s) => ({ value: s, label: statusText(s) }))]}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Đơn vị">
                <Select
                  value={get('unitId')}
                  onChange={(v) => update('unitId', v)}
                  options={[
                    { value: '', label: 'Trong phạm vi của tôi' },
                    ...options.map((u) => ({ value: String(u.unitId), label: u.unitName })),
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Ô lưới">
                <Select
                  value={get('gridId')}
                  onChange={(v) => update('gridId', v)}
                  options={[all, ...gridOptions.map((f) => ({ value: String(f.properties.gridId), label: f.properties.gridCode }))]}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Danh mục">
                <Select
                  value={get('categoryId')}
                  onChange={(v) => update('categoryId', v)}
                  options={[all, ...(categories.data?.data ?? []).map((c) => ({ value: String(c.id), label: c.name }))]}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Ưu tiên">
                <Select
                  value={get('priority')}
                  onChange={(v) => update('priority', v)}
                  options={[all, ...PRIORITY_OPTIONS.map((p) => ({ value: String(p.value), label: p.label }))]}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={3}>
              <Form.Item label=" " colon={false}>
                <Checkbox checked={get('overdue') === '1'} onChange={(e) => update('overdue', e.target.checked ? '1' : '')}>
                  Quá hạn
                </Checkbox>
              </Form.Item>
            </Col>
            <Col xs={12} md={3}>
              <Form.Item label=" " colon={false}>
                <Checkbox checked={get('review') === '1'} onChange={(e) => update('review', e.target.checked ? '1' : '')}>
                  Chờ duyệt
                </Checkbox>
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Từ ngày">
                <DatePicker
                  style={{ width: '100%' }}
                  value={get('from') ? dayjs(get('from')) : null}
                  onChange={(d) => update('from', d ? d.format('YYYY-MM-DD') : '')}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={6}>
              <Form.Item label="Đến ngày">
                <DatePicker
                  style={{ width: '100%' }}
                  value={get('to') ? dayjs(get('to')) : null}
                  onChange={(d) => update('to', d ? d.format('YYYY-MM-DD') : '')}
                />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {list.isError && <Alert type="error" showIcon role="alert" message={errorMessage(list.error)} />}

      <Table
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={data?.items ?? []}
        loading={list.isPending}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'Không có sự cố phù hợp bộ lọc.' }}
        pagination={{
          current: page,
          pageSize: PAGE_SIZE,
          total: data ? Number(data.totalCount) : 0,
          showSizeChanger: false,
          showTotal: (total) => `${total} sự cố`,
          onChange: goPage,
        }}
      />
    </Flex>
  );
}
function slaText(seconds) {
  if (seconds === null || seconds === undefined) return '—';
  const s = Number(seconds);
  if (!Number.isFinite(s)) return '—';
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return h > 0 ? `còn ${h} giờ ${m} phút` : `còn ${m} phút`;
}
