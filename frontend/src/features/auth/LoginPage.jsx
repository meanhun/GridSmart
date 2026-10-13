import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { Alert, Button, Card, Flex, Form, Input, Typography } from 'antd';
import { CameraOutlined, EnvironmentOutlined, LockOutlined, SearchOutlined, UserOutlined } from '@ant-design/icons';
import { z } from 'zod';
import { isApiError } from '@/api/errors';
import { errorMessage } from '@/lib/error-messages';
import { defaultLandingPath } from '@/lib/permissions';
import { vi } from '@/locales/vi';
import { useAuth } from './useAuth';
const schema = z.object({
  username: z.string().trim().min(1, 'Hãy nhập tên đăng nhập.'),
  password: z.string().min(1, 'Hãy nhập mật khẩu.'),
});
/** Màn hình đăng nhập cho cán bộ, lãnh đạo, quản trị. Người dân có lối đi riêng tới màn hình gửi phản ánh. */
export function LoginPage() {
  const { login, status, user } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [serverError, setServerError] = useState(null);
  const [showPassword, setShowPassword] = useState(false);
  // Controller (không phải register) vì antd Input không chuyển giá trị cho react-hook-form qua ref.
  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { username: '', password: '' } });
  const state = location.state;
  const from = state?.from;
  const notice = state?.notice;
  // Trang đích được quyết định ở đây, một lần, cho cả lần vừa đăng nhập lẫn lần mở lại khi đã có phiên.
  // Không gọi navigate() song song để tránh hai lệnh chuyển trang tranh nhau.
  if (status === 'authenticated' && user) {
    const landing = defaultLandingPath({ permissions: user.permissions, scopes: user.scopes });
    return <Navigate to={from ?? landing} replace />;
  }
  const onSubmit = async (values) => {
    setServerError(null);
    try {
      await login(values.username, values.password);
    } catch (err) {
      // ACCOUNT_LOCKED: backend ghi kèm thời điểm mở khóa trong detail.
      if (isApiError(err) && err.code === 'ACCOUNT_LOCKED' && err.message) {
        setServerError(err.message);
        return;
      }
      setServerError(errorMessage(err));
    }
  };
  return (
    <main style={{ display: 'flex', minHeight: '100vh', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '40px 16px' }}>
      <Flex vertical gap={24} style={{ width: '100%', maxWidth: 448 }}>
        <Flex vertical align="center" gap={12} component="header">
          <Flex
            align="center"
            justify="center"
            style={{ width: 64, height: 64, borderRadius: 16, background: '#21A691', color: '#FFFFFF' }}
          >
            <EnvironmentOutlined style={{ fontSize: 32 }} aria-hidden="true" />
          </Flex>
          <div style={{ textAlign: 'center' }}>
            <Typography.Text strong type="secondary" style={{ textTransform: 'uppercase' }}>
              {vi.appName}
            </Typography.Text>
            <Typography.Title level={2} style={{ margin: 0 }}>
              {vi.auth.loginTitle}
            </Typography.Title>
          </div>
        </Flex>

        <Card>
          <Flex vertical gap={20}>
            {notice && <Alert type="success" showIcon title={notice} />}

            <Form layout="vertical" onFinish={() => handleSubmit(onSubmit)()} noValidate>
              <Form.Item
                label={vi.auth.username}
                htmlFor="username"
                help={errors.username?.message}
                validateStatus={errors.username ? 'error' : undefined}
              >
                <Controller
                  name="username"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      id="username"
                      size="large"
                      autoComplete="username"
                      autoCapitalize="none"
                      prefix={<UserOutlined aria-hidden="true" />}
                      aria-invalid={errors.username ? true : undefined}
                    />
                  )}
                />
              </Form.Item>

              <Form.Item
                label={vi.auth.password}
                htmlFor="password"
                help={errors.password?.message}
                validateStatus={errors.password ? 'error' : undefined}
              >
                <Controller
                  name="password"
                  control={control}
                  render={({ field }) => (
                    <Input.Password
                      {...field}
                      id="password"
                      size="large"
                      autoComplete="current-password"
                      prefix={<LockOutlined aria-hidden="true" />}
                      visibilityToggle={{ visible: showPassword, onVisibleChange: setShowPassword }}
                      aria-invalid={errors.password ? true : undefined}
                    />
                  )}
                />
              </Form.Item>

              {serverError && <Alert type="error" showIcon title={serverError} style={{ marginBottom: 24 }} />}

              <Button type="primary" htmlType="submit" size="large" block loading={isSubmitting}>
                {isSubmitting ? vi.auth.submitting : vi.auth.submit}
              </Button>
            </Form>
          </Flex>
        </Card>

        <Card>
          <Flex vertical align="center" gap={12} style={{ textAlign: 'center' }}>
            <Typography.Text strong>Bạn là người dân?</Typography.Text>
            <Typography.Text type="secondary">
              Không cần đăng nhập để báo sự cố trên đường phố, vỉa hè hay hệ thống thoát nước.
            </Typography.Text>
            <Button size="large" block icon={<CameraOutlined aria-hidden="true" />} onClick={() => navigate('/')}>
              Gửi phản ánh ngay
            </Button>
            <Button type="link" icon={<SearchOutlined aria-hidden="true" />} onClick={() => navigate('/track')}>
              Tra cứu tiến độ phản ánh đã gửi
            </Button>
          </Flex>
        </Card>
      </Flex>
    </main>
  );
}
