import { useState } from 'react';
import { Link } from 'react-router';
import { Alert, Button, Card, Empty, Flex, Space, Spin, Typography } from 'antd';
import { useGetApiIncidents } from '@/api/generated/endpoints/incidents/incidents';
import { Can } from '@/components/Can';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
import { REVIEW_REASON_ORDER, reviewReasonText, REVIEW_REASON_LABEL } from '@/lib/incident-labels';
import { CancelForm, ClassifyForm, DispatchForm, useRefreshIncidentData } from './IncidentActionForms';
import { useUnitScope } from './UnitScopeContext';
/** Hàng chờ duyệt của điều phối viên (FE-18). Nhóm theo lý do, xử lý nhanh ngay trên dòng. */
export function ReviewQueuePage() {
  const { unitId } = useUnitScope();
  const list = useGetApiIncidents({ IsManualReview: true, UnitId: unitId, IncludeChildren: true, PageSize: 200 });
  const items = list.data?.data.items ?? [];
  const groups = new Map();
  for (const reason of REVIEW_REASON_ORDER) groups.set(reason, []);
  for (const item of items) {
    const key = item.reviewReason && REVIEW_REASON_LABEL[item.reviewReason.toUpperCase()] ? item.reviewReason.toUpperCase() : 'OTHER';
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }
  return (
    <Flex vertical gap="large" component="section" aria-labelledby="review-title">
      <Typography.Title level={3} id="review-title" style={{ margin: 0 }}>
        Hàng chờ duyệt ({items.length})
      </Typography.Title>

      {list.isError && <Alert type="error" showIcon role="alert" message={errorMessage(list.error)} />}
      {list.isPending && (
        <Flex role="status" justify="center">
          <Spin tip="Đang tải…">
            <div style={{ padding: 24 }} />
          </Spin>
        </Flex>
      )}
      {list.isSuccess && items.length === 0 && <Empty description="Không có sự cố nào chờ duyệt." />}

      {[...groups.entries()].map(([reason, rows]) =>
        rows.length === 0 ? null : (
          <Flex vertical gap="small" component="section" key={reason} aria-label={reviewReasonText(reason === 'OTHER' ? null : reason)}>
            <Typography.Title level={5} style={{ margin: 0 }}>
              {reviewReasonText(reason === 'OTHER' ? null : reason)} ({rows.length})
            </Typography.Title>
            <Flex vertical gap="small">
              {rows.map((row) => (
                <ReviewRow key={row.id} item={row} />
              ))}
            </Flex>
          </Flex>
        ),
      )}
    </Flex>
  );
}
function ReviewRow({ item }) {
  const [open, setOpen] = useState(null);
  const [notice, setNotice] = useState(null);
  const refresh = useRefreshIncidentData();
  const done = async () => {
    setOpen(null);
    setNotice('Đã xử lý. Sự cố đã rời hàng chờ.');
    await refresh();
  };
  const stale = async () => {
    setOpen(null);
    await refresh();
  };
  const props = { incidentId: Number(item.id), onDone: () => void done(), onStale: () => void stale(), compact: true };
  const toggle = (key) => setOpen(open === key ? null : key);
  return (
    <Card size="small">
      <Flex wrap justify="space-between" align="center" gap="small">
        <div>
          <Link to={`/command/incidents/${item.id}`} style={{ fontFamily: 'monospace' }}>
            {item.trackingCode}
          </Link>
          <div>
            {item.categoryName || 'Chưa phân loại'} · {item.addressText || 'Chưa có địa chỉ'}
          </div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Gửi {formatDateTime(item.createdAt)}
          </Typography.Text>
        </div>
        <Space wrap>
          <Can permission="incident.classify">
            <Button size="small" aria-pressed={open === 'classify'} onClick={() => toggle('classify')}>
              Phân loại
            </Button>
          </Can>
          <Can permission="incident.dispatch">
            <Button size="small" aria-pressed={open === 'dispatch'} onClick={() => toggle('dispatch')}>
              Điều phối
            </Button>
          </Can>
          <Can permission="incident.cancel">
            <Button size="small" danger aria-pressed={open === 'cancel'} onClick={() => toggle('cancel')}>
              Hủy
            </Button>
          </Can>
        </Space>
      </Flex>
      {notice && <Alert type="success" showIcon role="status" message={notice} style={{ marginTop: 8 }} />}
      {open === 'classify' && (
        <div style={{ marginTop: 12 }}>
          <ClassifyForm {...props} />
        </div>
      )}
      {open === 'dispatch' && (
        <div style={{ marginTop: 12 }}>
          <DispatchForm {...props} />
        </div>
      )}
      {open === 'cancel' && (
        <div style={{ marginTop: 12 }}>
          <CancelForm {...props} />
        </div>
      )}
    </Card>
  );
}
