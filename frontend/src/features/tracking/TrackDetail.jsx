import { useState } from 'react';
import { Alert, Card, Descriptions, Flex, Slider, Steps, Timeline, Typography } from 'antd';
import { MessageOutlined } from '@ant-design/icons';
import { formatDateTime } from '@/lib/datetime';
import { PUBLIC_STEPS, publicStep, statusLabel } from '@/lib/incident-status';
import { RatingForm } from './RatingForm';
/** Chi tiết tiến độ cho công dân (FE-09, FE-10). */
export function TrackDetail({ data, phone }) {
  const step = publicStep(data.status);
  const cancelled = data.status.toUpperCase() === 'CANCELLED';
  const items = [
    { key: 'category', label: 'Danh mục', children: data.categoryName || 'Chưa phân loại' },
    { key: 'address', label: 'Địa chỉ', children: data.addressText || 'Chưa xác định' },
    { key: 'created', label: 'Gửi lúc', children: formatDateTime(data.createdAt) },
  ];
  if (data.resolvedAt) {
    items.push({ key: 'resolved', label: 'Hoàn thành lúc', children: formatDateTime(data.resolvedAt) });
  }
  return (
    <article aria-labelledby="track-title">
      <Flex vertical gap={20}>
        <header>
          <Typography.Text code>{data.trackingCode}</Typography.Text>
          <Typography.Title level={4} id="track-title" style={{ margin: 0 }}>
            {statusLabel(data.status)}
          </Typography.Title>
        </header>

        {cancelled ? (
          <Alert type="info" showIcon role="status" message="Phản ánh này đã bị hủy." />
        ) : (
          <Steps
            size="small"
            labelPlacement="vertical"
            aria-label="Tiến trình xử lý"
            current={step === null ? -1 : step}
            items={PUBLIC_STEPS.map((label) => ({ title: label }))}
          />
        )}

        <Descriptions size="small" column={1} items={items} />

        {data.isMerged && (
          <Alert type="info" showIcon message="Phản ánh của bạn đã được gộp với một phản ánh khác cùng vị trí." />
        )}

        {data.staffNotes.length > 0 && (
          <Card
            size="small"
            aria-labelledby="staff-reason-title"
            title={
              <Flex align="center" gap={8} id="staff-reason-title">
                <MessageOutlined aria-hidden="true" /> Lý do từ cán bộ xử lý
              </Flex>
            }
          >
            <Flex vertical gap={12}>
              {data.staffNotes.map((note, index) => (
                <div key={`${note.at}-${index}`}>
                  <Typography.Text strong>{note.action}</Typography.Text>
                  <Typography.Paragraph style={{ margin: 0 }}>{note.reason}</Typography.Paragraph>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {formatDateTime(note.at)}
                  </Typography.Text>
                </div>
              ))}
            </Flex>
          </Card>
        )}

        <BeforeAfter before={data.beforeImages} after={data.afterImages} />

        {data.timeline.length > 0 && (
          <section aria-labelledby="timeline-title">
            <Typography.Title level={5} id="timeline-title">
              Diễn biến
            </Typography.Title>
            <Timeline
              items={data.timeline.map((item, i) => ({
                key: `${item.at}-${i}`,
                children: (
                  <>
                    <div>{item.message}</div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {formatDateTime(item.at)}
                    </Typography.Text>
                  </>
                ),
              }))}
            />
          </section>
        )}

        {data.canRate ? (
          <RatingForm trackingCode={data.trackingCode} phone={phone} />
        ) : data.rating ? (
          <Typography.Text>
            Bạn đã đánh giá {data.rating}/5 sao.
            {data.ratingFeedback ? ` “${data.ratingFeedback}”` : ''}
          </Typography.Text>
        ) : null}
      </Flex>
    </article>
  );
}
/** Ảnh Before và After đặt cạnh nhau. Khi đủ cả hai, có thanh trượt so sánh (FE-09). */
export function BeforeAfter({ before, after }) {
  const [split, setSplit] = useState(50);
  const beforeUrl = before[0]?.url;
  const afterUrl = after[0]?.url;
  if (!beforeUrl && !afterUrl) return null;
  if (beforeUrl && afterUrl) {
    return (
      <section aria-labelledby="compare-title">
        <Flex vertical gap={8}>
          <Typography.Title level={5} id="compare-title" style={{ margin: 0 }}>
            Trước và sau xử lý
          </Typography.Title>
          <div style={{ position: 'relative', aspectRatio: '4 / 3', overflow: 'hidden', borderRadius: 8, background: '#B4B4B2' }}>
            <img
              src={beforeUrl}
              alt="Ảnh trước xử lý"
              style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'cover' }}
            />
            <img
              src={afterUrl}
              alt="Ảnh sau xử lý"
              style={{
                position: 'absolute',
                inset: 0,
                width: '100%',
                height: '100%',
                objectFit: 'cover',
                clipPath: `inset(0 ${100 - split}% 0 0)`,
              }}
            />
          </div>
          <label htmlFor="compare-split">
            <Typography.Text>Kéo để so sánh trước và sau</Typography.Text>
          </label>
          <Slider id="compare-split" min={0} max={100} value={split} onChange={(v) => setSplit(Number(v))} />
        </Flex>
      </section>
    );
  }
  const only = beforeUrl ?? afterUrl;
  return (
    <section aria-labelledby="photo-title">
      <Flex vertical gap={8}>
        <Typography.Title level={5} id="photo-title" style={{ margin: 0 }}>
          {beforeUrl ? 'Ảnh hiện trường' : 'Ảnh nghiệm thu'}
        </Typography.Title>
        <img src={only} alt={beforeUrl ? 'Ảnh trước xử lý' : 'Ảnh sau xử lý'} style={{ width: '100%', borderRadius: 8 }} />
      </Flex>
    </section>
  );
}
