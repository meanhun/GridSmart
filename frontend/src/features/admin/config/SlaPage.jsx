import { useEffect, useState } from 'react';
import { Alert, Button, Flex, Input, InputNumber, Table, Typography } from 'antd';
import { getGetApiSlaPoliciesQueryKey, putApiSlaPolicies, useGetApiSlaPolicies } from '@/api/generated/endpoints/config/config';
import { useQueryClient } from '@tanstack/react-query';
import { PRIORITY_OPTIONS, priorityText } from '@/lib/incident-labels';
import { errorMessage } from '@/lib/error-messages';
/**
 * Cấu hình SLA theo mức ưu tiên (FE-23). Thay đổi chỉ áp dụng cho sự cố được điều phối sau thời điểm lưu.
 */
export function SlaPage() {
  const list = useGetApiSlaPolicies();
  const queryClient = useQueryClient();
  const [drafts, setDrafts] = useState([]);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!list.data) return;
    setDrafts(
      list.data.data.map((p) => ({
        priority: Number(p.priority),
        slaHours: String(p.slaHours),
        escalationPercent: String(p.escalationPercent),
        description: p.description ?? '',
      })),
    );
  }, [list.data]);
  const update = (priority, patch) => setDrafts((ds) => ds.map((d) => (d.priority === priority ? { ...d, ...patch } : d)));
  const save = async () => {
    setError(null);
    for (const d of drafts) {
      const hours = Number(d.slaHours);
      const pct = Number(d.escalationPercent);
      if (!Number.isFinite(hours) || hours <= 0) return setError(`Số giờ SLA của ${priorityText(d.priority)} phải lớn hơn 0.`);
      if (!Number.isFinite(pct) || pct < 0 || pct > 100)
        return setError(`Tỷ lệ leo thang của ${priorityText(d.priority)} phải từ 0 đến 100.`);
    }
    setBusy(true);
    try {
      await putApiSlaPolicies(
        drafts.map((d) => ({
          priority: d.priority,
          slaHours: Number(d.slaHours),
          escalationPercent: Number(d.escalationPercent),
          description: d.description,
        })),
      );
      setNotice('Đã lưu cấu hình SLA. Thay đổi áp dụng cho sự cố điều phối từ thời điểm này.');
      await queryClient.invalidateQueries({ queryKey: getGetApiSlaPoliciesQueryKey() });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  // InputNumber trả number hoặc null khi xóa trắng; giữ state dạng chuỗi như trước để logic kiểm tra không đổi.
  const numberToText = (v) => (v === null || v === undefined ? '' : String(v));
  const columns = [
    { title: 'Mức ưu tiên', key: 'priority', render: (_, d) => priorityText(d.priority) },
    {
      title: 'Số giờ',
      key: 'slaHours',
      render: (_, d) => (
        <InputNumber
          aria-label={`Số giờ SLA ${priorityText(d.priority)}`}
          min={1}
          style={{ width: 96 }}
          value={d.slaHours === '' ? null : Number(d.slaHours)}
          onChange={(v) => update(d.priority, { slaHours: numberToText(v) })}
        />
      ),
    },
    {
      title: 'Leo thang (%)',
      key: 'escalationPercent',
      render: (_, d) => (
        <InputNumber
          aria-label={`Tỷ lệ leo thang ${priorityText(d.priority)}`}
          min={0}
          max={100}
          style={{ width: 96 }}
          value={d.escalationPercent === '' ? null : Number(d.escalationPercent)}
          onChange={(v) => update(d.priority, { escalationPercent: numberToText(v) })}
        />
      ),
    },
    {
      title: 'Ghi chú',
      key: 'description',
      render: (_, d) => (
        <Input
          aria-label={`Ghi chú ${priorityText(d.priority)}`}
          value={d.description}
          onChange={(e) => update(d.priority, { description: e.target.value })}
        />
      ),
    },
  ];
  return (
    <section aria-labelledby="sla-title">
      <Flex vertical gap="middle">
        <Typography.Title level={3} id="sla-title" style={{ margin: 0 }}>
          Thời hạn SLA
        </Typography.Title>
        <Typography.Paragraph style={{ margin: 0 }}>
          Thay đổi chỉ áp dụng cho sự cố được điều phối sau thời điểm lưu. Sự cố đang xử lý giữ hạn cũ.
        </Typography.Paragraph>
        {notice && <Alert type="success" showIcon title={notice} role="status" />}
        {error && <Alert type="error" showIcon title={error} role="alert" />}
        {list.isError && <Alert type="error" showIcon title={errorMessage(list.error)} role="alert" />}

        <Table
          aria-label="Cấu hình SLA theo mức ưu tiên"
          rowKey="priority"
          columns={columns}
          dataSource={drafts}
          loading={list.isPending}
          pagination={false}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: `Chưa có cấu hình SLA. Mức ưu tiên: ${PRIORITY_OPTIONS.map((p) => p.label).join(', ')}.` }}
        />

        <div>
          <Button type="primary" loading={busy} disabled={busy || drafts.length === 0} onClick={() => void save()}>
            {busy ? 'Đang lưu…' : 'Lưu cấu hình SLA'}
          </Button>
        </div>
      </Flex>
    </section>
  );
}
