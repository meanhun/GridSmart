import { useState } from 'react';
import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Alert, Breadcrumb, Button, Card, Col, DatePicker, Flex, Row, Statistic, Table, Tabs, Typography } from 'antd';
import dayjs from 'dayjs';
import { useGetApiDashboardSummary, useGetApiDashboardTrend } from '@/api/generated/endpoints/dashboard/dashboard';
import { useGetApiReportsByCategory, useGetApiReportsByUnit, useGetApiReportsOfficerKpi } from '@/api/generated/endpoints/reports/reports';
import { Can } from '@/components/Can';
import { usePermission } from '@/features/auth/useAuth';
import { useUnitScope } from '@/features/command/UnitScopeContext';
import { downloadFile } from '@/lib/download';
import { errorMessage } from '@/lib/error-messages';
import { toNumber } from '@/lib/geo-math';
const TABS = [
  { key: 'overview', label: 'Tổng quan' },
  { key: 'unit', label: 'Theo đơn vị' },
  { key: 'category', label: 'Theo danh mục' },
  { key: 'kpi', label: 'KPI cán bộ' },
];
const EXPORT_TYPE = {
  overview: 'incidents',
  unit: 'by-unit',
  category: null,
  kpi: 'officer-kpi',
};
const DATE_FORMAT = 'YYYY-MM-DD';
function isoDate(d) {
  return d.toISOString().slice(0, 10);
}
/** Báo cáo và dashboard điều hành (FE-20). Bộ lọc thời gian dùng chung cho mọi tab. */
export function ReportsPage() {
  const { unitId } = useUnitScope();
  const canKpi = usePermission('kpi.view');
  const canExport = usePermission('report.export');
  const [tab, setTab] = useState('overview');
  const [range, setRange] = useState(() => {
    const to = new Date();
    const from = new Date(to.getTime() - 29 * 86_400_000);
    return { from: isoDate(from), to: isoDate(to) };
  });
  const [exportError, setExportError] = useState(null);
  // Đường đi xuống cấp đơn vị: tỉnh → phường → khu phố. Stack để quay lại.
  const [unitStack, setUnitStack] = useState([{ id: undefined, name: 'Toàn bộ' }]);
  const parentUnitId = unitStack[unitStack.length - 1].id;
  const summary = useGetApiDashboardSummary({ unitId });
  const trend = useGetApiDashboardTrend({ unitId, days: 30 });
  const byUnit = useGetApiReportsByUnit({ parentUnitId, from: range.from, to: range.to }, { query: { enabled: tab === 'unit' } });
  const byCategory = useGetApiReportsByCategory({ from: range.from, to: range.to, unitId }, { query: { enabled: tab === 'category' } });
  const kpi = useGetApiReportsOfficerKpi({ unitId, from: range.from, to: range.to }, { query: { enabled: tab === 'kpi' && canKpi } });
  const exportType = EXPORT_TYPE[tab];
  const onExport = async () => {
    if (!exportType) return;
    setExportError(null);
    const q = new URLSearchParams({ type: exportType, format: 'xlsx', from: range.from, to: range.to });
    if (unitId) q.set('unitId', String(unitId));
    try {
      await downloadFile(`/api/reports/export?${q.toString()}`, `${exportType}.xlsx`);
    } catch (err) {
      setExportError(errorMessage(err));
    }
  };
  const visibleTabs = TABS.filter((t) => t.key !== 'kpi' || canKpi);
  const openUnit = (r) => setUnitStack([...unitStack, { id: toNumber(r.unitId), name: r.name }]);
  const summaryData = summary.data?.data;
  const unitColumns = [
    { title: 'Đơn vị', dataIndex: 'name', key: 'name' },
    { title: 'Tổng', dataIndex: 'totalIncidents', key: 'totalIncidents' },
    { title: 'Đang mở', dataIndex: 'openIncidents', key: 'openIncidents' },
    { title: 'Đã xử lý', dataIndex: 'resolvedIncidents', key: 'resolvedIncidents' },
    {
      title: 'Quá hạn',
      dataIndex: 'overdueIncidents',
      key: 'overdueIncidents',
      render: (v) => (Number(v) > 0 ? <Typography.Text type="danger" strong>{v}</Typography.Text> : v),
    },
  ];
  const categoryColumns = [
    { title: 'Danh mục', dataIndex: 'categoryName', key: 'categoryName' },
    { title: 'Tổng', dataIndex: 'total', key: 'total' },
    { title: 'Đang mở', dataIndex: 'open', key: 'open' },
    { title: 'Đã xử lý', dataIndex: 'resolved', key: 'resolved' },
    { title: 'Quá hạn', dataIndex: 'overdue', key: 'overdue' },
  ];
  return (
    <Flex vertical gap={20} component="section" aria-labelledby="reports-title">
      <Flex wrap gap={12} align="flex-end" justify="space-between">
        <Typography.Title level={3} id="reports-title" style={{ margin: 0 }}>
          Báo cáo
        </Typography.Title>
        <Flex wrap gap={8} align="flex-end">
          <Flex vertical>
            <label htmlFor="reports-from">Từ ngày</label>
            <DatePicker
              id="reports-from"
              allowClear={false}
              format={DATE_FORMAT}
              value={dayjs(range.from)}
              onChange={(d) => d && setRange({ ...range, from: d.format(DATE_FORMAT) })}
            />
          </Flex>
          <Flex vertical>
            <label htmlFor="reports-to">Đến ngày</label>
            <DatePicker
              id="reports-to"
              allowClear={false}
              format={DATE_FORMAT}
              value={dayjs(range.to)}
              onChange={(d) => d && setRange({ ...range, to: d.format(DATE_FORMAT) })}
            />
          </Flex>
          {canExport && exportType && (
            <Button type="primary" onClick={() => void onExport()}>
              Xuất Excel
            </Button>
          )}
        </Flex>
      </Flex>
      {exportError && <Alert type="error" showIcon role="alert" title={exportError} />}

      <Tabs
        aria-label="Loại báo cáo"
        activeKey={tab}
        onChange={setTab}
        items={visibleTabs.map((t) => ({ key: t.key, label: t.label }))}
        style={{ marginBottom: -12 }}
      />

      {tab === 'overview' && (
        <Flex vertical gap={24}>
          <Row gutter={[12, 12]}>
            {[
              { label: 'Đang mở', value: summaryData?.openIncidents },
              { label: 'Quá hạn', value: summaryData?.overdueIncidents, danger: true },
              { label: 'Chờ duyệt', value: summaryData?.pendingReview },
              { label: 'Hoàn thành hôm nay', value: summaryData?.resolvedToday },
              { label: 'Đúng hạn', value: percent(summaryData?.onTimeRate) },
              { label: 'Trùng lặp', value: percent(summaryData?.duplicateRate) },
              { label: 'Nhận việc TB (phút)', value: summaryData?.avgDispatchMinutes },
              { label: 'Xử lý TB (giờ)', value: summaryData?.avgResolutionHours },
            ].map((c) => (
              <Col key={c.label} xs={12} md={6}>
                <Card size="small">
                  <Statistic title={c.label} value={c.value ?? '—'} styles={c.danger ? { content: { color: '#b91c1c' } } : undefined} />
                </Card>
              </Col>
            ))}
          </Row>
          <Card size="small" title="Xu hướng 30 ngày">
            {trend.isError && <Alert type="error" showIcon role="alert" title={errorMessage(trend.error)} style={{ marginBottom: 8 }} />}
            <div style={{ height: 240 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={trend.data?.data ?? []}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" fontSize={11} />
                  <YAxis allowDecimals={false} fontSize={11} />
                  <Tooltip />
                  <Legend />
                  <Line type="monotone" dataKey="created" name="Sự cố mới" stroke="#21A691" dot={false} />
                  <Line type="monotone" dataKey="resolved" name="Đã xử lý" stroke="#21A691" dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </Card>
        </Flex>
      )}

      {tab === 'unit' && (
        <Flex vertical gap={12}>
          <Breadcrumb
            aria-label="Đường đi đơn vị"
            separator="›"
            items={unitStack.map((u, i) => ({
              key: `${u.id}-${i}`,
              title: (
                <Button type="link" size="small" style={{ padding: 0 }} onClick={() => setUnitStack(unitStack.slice(0, i + 1))}>
                  {u.name}
                </Button>
              ),
            }))}
          />
          {byUnit.isError && <Alert type="error" showIcon role="alert" title={errorMessage(byUnit.error)} />}
          <Table
            aria-label="Báo cáo theo đơn vị. Bấm một dòng để xem đơn vị con."
            size="small"
            columns={unitColumns}
            dataSource={byUnit.data?.data ?? []}
            rowKey={(r) => String(r.unitId)}
            pagination={false}
            scroll={{ x: 'max-content' }}
            loading={byUnit.isLoading}
            locale={{ emptyText: 'Không có đơn vị con trong phạm vi.' }}
            onRow={(r) => ({
              tabIndex: 0,
              style: { cursor: 'pointer' },
              onClick: () => openUnit(r),
              onKeyDown: (e) => {
                if (e.key === 'Enter') openUnit(r);
              },
            })}
          />
        </Flex>
      )}

      {tab === 'category' && (
        <Flex vertical gap={16}>
          {byCategory.isError && <Alert type="error" showIcon role="alert" title={errorMessage(byCategory.error)} />}
          <Card size="small">
            <div style={{ height: 264 }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={byCategory.data?.data ?? []}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="categoryName" fontSize={11} />
                  <YAxis allowDecimals={false} fontSize={11} />
                  <Tooltip />
                  <Legend />
                  <Bar dataKey="open" name="Đang mở" fill="#21A691" />
                  <Bar dataKey="resolved" name="Đã xử lý" fill="#21A691" />
                  <Bar dataKey="overdue" name="Quá hạn" fill="#b91c1c" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>
          <Table
            aria-label="Số liệu theo danh mục"
            size="small"
            columns={categoryColumns}
            dataSource={byCategory.data?.data ?? []}
            rowKey={(r) => String(r.categoryId)}
            pagination={false}
            scroll={{ x: 'max-content' }}
            loading={byCategory.isLoading}
          />
        </Flex>
      )}

      {tab === 'kpi' && (
        <Can permission="kpi.view" fallback={<Typography.Text type="secondary">Bạn không có quyền xem KPI cán bộ.</Typography.Text>}>
          <KpiTable rows={kpi.data?.data ?? []} loading={kpi.isPending} error={kpi.isError ? errorMessage(kpi.error) : null} />
        </Can>
      )}
    </Flex>
  );
}
/** Tỷ lệ có thể là 0..1 hoặc 0..100 tùy endpoint; đưa về phần trăm để hiển thị thống nhất. */
export function percent(value) {
  const n = toNumber(value);
  if (n === null) return undefined;
  return `${Math.round(n <= 1 ? n * 100 : n)}%`;
}
/** So sánh hai ô KPI: số thì so số, chuỗi thì so theo localeCompare. */
function compareKey(key) {
  return (a, b) => {
    const av = toNumber(a[key]) ?? (typeof a[key] === 'string' ? String(a[key]) : 0);
    const bv = toNumber(b[key]) ?? (typeof b[key] === 'string' ? String(b[key]) : 0);
    if (typeof av === 'string' || typeof bv === 'string') return String(av).localeCompare(String(bv));
    return av - bv;
  };
}
function isLowOnTime(r) {
  const ratio = toNumber(r.onTimeRate);
  return ratio !== null && (ratio <= 1 ? ratio : ratio / 100) < 0.8;
}
/** Bảng KPI có sắp xếp. Cán bộ dưới 80 % đúng hạn được tô đỏ (FE-20). */
function KpiTable({ rows, loading, error }) {
  if (error) return <Alert type="error" showIcon role="alert" title={error} />;
  const col = (key, title, extra = {}) => ({ title, dataIndex: key, key, sorter: compareKey(key), sortDirections: ['ascend', 'descend'], ...extra });
  const columns = [
    col('fullName', 'Cán bộ'),
    col('assigned', 'Được giao'),
    col('resolved', 'Đã xử lý'),
    col('openNow', 'Đang mở'),
    col('onTimeRate', 'Đúng hạn', {
      defaultSortOrder: 'ascend',
      render: (_, r) => (isLowOnTime(r) ? <Typography.Text type="danger" strong>{percent(r.onTimeRate)}</Typography.Text> : percent(r.onTimeRate)),
    }),
    col('avgAcceptMinutes', 'Nhận việc TB (phút)'),
    col('avgResolveHours', 'Xử lý TB (giờ)'),
    col('avgRating', 'Điểm đánh giá'),
    col('escalatedCount', 'Bị leo thang'),
  ];
  return (
    <Table
      aria-label="KPI cán bộ. Bấm tiêu đề cột để sắp xếp."
      size="small"
      columns={columns}
      dataSource={rows}
      rowKey={(r) => String(r.officerId)}
      pagination={false}
      scroll={{ x: 'max-content' }}
      loading={loading}
      locale={{ emptyText: 'Chưa có dữ liệu KPI trong khoảng thời gian này.' }}
      onRow={(r) => ({ style: isLowOnTime(r) ? { background: '#fef2f2' } : undefined })}
    />
  );
}
