import { useEffect, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { useParams } from 'react-router';
import { Alert, Button, Flex, Form, Input, Spin, Typography } from 'antd';
import { z } from 'zod';
import { useGetApiPublicTrack } from '@/api/generated/endpoints/public/public';
import { useRealtime } from '@/features/realtime/RealtimeProvider';
import { errorMessage } from '@/lib/error-messages';
import { findLocalReport } from '@/lib/local-reports';
import { TrackDetail } from './TrackDetail';
const schema = z.object({
  code: z.string().trim().min(1, 'Vui lòng nhập mã tra cứu.'),
  phone: z
    .string()
    .trim()
    .regex(/^\+?[0-9]{9,15}$/, 'Số điện thoại chưa đúng định dạng.'),
});
/** Tra cứu tiến độ (FE-09). Mở trực tiếp /track/:code thì điền sẵn mã và SĐT đã lưu trên máy. */
export function TrackPage() {
  const { code: routeCode } = useParams();
  const [query, setQuery] = useState(() => {
    if (!routeCode) return null;
    const local = findLocalReport(routeCode);
    return local ? { code: local.trackingCode, phone: local.phone } : null;
  });
  // Controller vì antd Input không chuyển giá trị cho react-hook-form qua ref (xem LoginPage).
  const {
    control,
    handleSubmit,
    formState: { errors },
    reset,
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { code: routeCode ?? '', phone: '' },
  });
  // Đổi mã trên đường dẫn thì reset form và truy vấn lại.
  useEffect(() => {
    if (!routeCode) return;
    const local = findLocalReport(routeCode);
    reset({ code: routeCode, phone: local?.phone ?? '' });
    setQuery(local ? { code: local.trackingCode, phone: local.phone } : null);
  }, [routeCode, reset]);
  return (
    <Flex vertical gap={24}>
      <Typography.Title level={3} style={{ margin: 0 }}>
        Tra cứu tiến độ
      </Typography.Title>

      {!query && (
        <Form layout="vertical" onFinish={handleSubmit((values) => setQuery(values))} noValidate>
          <Form.Item
            label="Mã tra cứu"
            htmlFor="code"
            help={errors.code?.message}
            validateStatus={errors.code ? 'error' : undefined}
          >
            <Controller
              name="code"
              control={control}
              render={({ field }) => (
                <Input {...field} id="code" autoCapitalize="characters" aria-invalid={errors.code ? true : undefined} />
              )}
            />
          </Form.Item>
          <Form.Item
            label="Số điện thoại đã dùng khi gửi"
            htmlFor="phone"
            help={errors.phone?.message}
            validateStatus={errors.phone ? 'error' : undefined}
          >
            <Controller
              name="phone"
              control={control}
              render={({ field }) => (
                <Input {...field} id="phone" type="tel" aria-invalid={errors.phone ? true : undefined} />
              )}
            />
          </Form.Item>
          <Button type="primary" htmlType="submit" block>
            Tra cứu
          </Button>
        </Form>
      )}

      {query && <TrackResultView code={query.code} phone={query.phone} onChangeQuery={() => setQuery(null)} />}
    </Flex>
  );
}
function TrackResultView({ code, phone, onChangeQuery }) {
  const track = useGetApiPublicTrack({ code, phone }, { query: { retry: false } });
  const { state, joinTracking } = useRealtime();
  // Nhận cập nhật realtime của phản ánh này (FE-06, nhóm tra cứu).
  useEffect(() => {
    if (state !== 'connected') return;
    void joinTracking(code, phone).catch(() => undefined);
  }, [state, code, phone, joinTracking]);
  if (track.isPending)
    return (
      <div role="status">
        <Spin tip="Đang tải…">
          <div style={{ padding: 24 }} />
        </Spin>
      </div>
    );
  if (track.isError) {
    return (
      <Flex vertical gap={12}>
        <Alert type="error" showIcon role="alert" message={errorMessage(track.error)} />
        <div>
          <Button type="link" onClick={onChangeQuery} style={{ padding: 0 }}>
            Nhập lại mã và số điện thoại
          </Button>
        </div>
      </Flex>
    );
  }
  return (
    <Flex vertical gap={16}>
      <TrackDetail data={track.data.data} phone={phone} />
      <div>
        <Button type="link" onClick={onChangeQuery} style={{ padding: 0 }}>
          Tra cứu mã khác
        </Button>
      </div>
    </Flex>
  );
}
