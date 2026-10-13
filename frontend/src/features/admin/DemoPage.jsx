import { useState } from 'react';
import { Alert, Button, Card, Flex, Form, Input, Select, Typography } from 'antd';
import { postApiDemoIncidentsIdFastForward } from '@/api/generated/endpoints/demo/demo';
import { usePermission } from '@/features/auth/useAuth';
import { errorMessage } from '@/lib/error-messages';
/** Chỉ bản build demo mới bật trang này (FE-27). Backend cũng chặn ngoài môi trường Demo. */
export const DEMO_ENABLED = import.meta.env.VITE_DEMO === 'true';
const MINUTE_OPTIONS = [
  { value: 60, label: '1 giờ' },
  { value: 240, label: '4 giờ' },
  { value: 1440, label: '1 ngày' },
];
/** Trang ẩn tua nhanh SLA để trình diễn leo thang và quá hạn ngay (FE-27). */
export function DemoPage() {
  const isAdmin = usePermission('role.manage');
  const [incidentId, setIncidentId] = useState('');
  const [minutes, setMinutes] = useState(MINUTE_OPTIONS[0].value);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  if (!DEMO_ENABLED) return <Typography.Text type="secondary">Trang này chỉ có trong bản build demo.</Typography.Text>;
  if (!isAdmin) return <Typography.Text type="secondary">Chỉ quản trị hệ thống mới dùng được trang này.</Typography.Text>;
  const submit = async () => {
    if (!/^\d+$/.test(incidentId)) return setError('Mã sự cố phải là số.');
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await postApiDemoIncidentsIdFastForward(Number(incidentId), { minutes: Number(minutes) });
      setNotice(`Đã tua nhanh sự cố ${incidentId}. Kiểm tra leo thang và quá hạn trên bản đồ điều hành.`);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Flex vertical gap="middle" style={{ maxWidth: 576 }} component="section" aria-labelledby="demo-title">
      <Typography.Title level={3} id="demo-title" style={{ margin: 0 }}>
        Tua nhanh SLA (demo)
      </Typography.Title>
      <Typography.Paragraph style={{ margin: 0 }}>
        Lùi thời gian của sự cố để kích hoạt leo thang và quá hạn ngay, không cần chờ thật.
      </Typography.Paragraph>
      <Card>
        <Form layout="vertical" onFinish={submit}>
          <Form.Item label="Mã sự cố (id)" htmlFor="demo-incident-id">
            <Input id="demo-incident-id" inputMode="numeric" value={incidentId} onChange={(e) => setIncidentId(e.target.value)} />
          </Form.Item>
          <Form.Item label="Lùi thời gian" htmlFor="demo-minutes">
            <Select id="demo-minutes" value={minutes} onChange={setMinutes} options={MINUTE_OPTIONS} />
          </Form.Item>
          {notice && <Alert type="success" showIcon title={notice} role="status" style={{ marginBottom: 16 }} />}
          {error && <Alert type="error" showIcon title={error} role="alert" style={{ marginBottom: 16 }} />}
          <Button type="primary" htmlType="submit" loading={busy}>
            Tua nhanh
          </Button>
        </Form>
      </Card>
    </Flex>
  );
}
