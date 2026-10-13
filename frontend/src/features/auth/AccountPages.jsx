import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { Alert, Button, Form, Input, Typography } from 'antd';
import { postApiAuthChangePassword, postApiAuthRegister } from '@/api/generated/endpoints/auth/auth';
import { errorMessage } from '@/lib/error-messages';
import { useAuth } from './useAuth';
const MIN_PASSWORD = 8;
/** Đổi mật khẩu. Sau khi đổi, phiên bị đăng xuất và người dùng đăng nhập lại (FE-03). */
export function ChangePasswordPage() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    if (next.length < MIN_PASSWORD) return setError(`Mật khẩu mới tối thiểu ${MIN_PASSWORD} ký tự.`);
    if (next !== confirm) return setError('Mật khẩu xác nhận không khớp.');
    setBusy(true);
    setError(null);
    try {
      await postApiAuthChangePassword({ currentPassword: current, newPassword: next });
      await logout();
      navigate('/login', { replace: true, state: { notice: 'Đã đổi mật khẩu. Vui lòng đăng nhập lại.' } });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Form onFinish={submit} layout="vertical" style={{ maxWidth: 384, margin: '0 auto' }} aria-labelledby="pw-title">
      <Typography.Title level={3} id="pw-title">
        Đổi mật khẩu
      </Typography.Title>
      <Field id="pw-current" label="Mật khẩu hiện tại" value={current} onChange={setCurrent} />
      <Field id="pw-new" label="Mật khẩu mới" value={next} onChange={setNext} />
      <Field id="pw-confirm" label="Nhập lại mật khẩu mới" value={confirm} onChange={setConfirm} />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 24 }} />}
      <Button type="primary" htmlType="submit" block loading={busy}>
        Đổi mật khẩu
      </Button>
    </Form>
  );
}
/** Đăng ký tài khoản công dân (P1). Sau khi đăng ký chuyển tới trang đăng nhập. */
export function RegisterPage() {
  const { status } = useAuth();
  const navigate = useNavigate();
  const [phone, setPhone] = useState('');
  const [fullName, setFullName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  if (status === 'authenticated') return <Navigate to="/" replace />;
  const submit = async () => {
    if (!/^\+?[0-9]{9,15}$/.test(phone.trim())) return setError('Số điện thoại chưa đúng định dạng.');
    if (!fullName.trim()) return setError('Hãy nhập họ tên.');
    if (password.length < MIN_PASSWORD) return setError(`Mật khẩu tối thiểu ${MIN_PASSWORD} ký tự.`);
    setBusy(true);
    setError(null);
    try {
      await postApiAuthRegister({ phone: phone.trim(), fullName: fullName.trim(), password });
      navigate('/login', { replace: true, state: { notice: 'Đăng ký thành công. Hãy đăng nhập bằng số điện thoại.' } });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Form onFinish={submit} layout="vertical" style={{ maxWidth: 384, margin: '0 auto' }} aria-labelledby="register-title">
      <Typography.Title level={3} id="register-title">
        Đăng ký công dân
      </Typography.Title>
      <Field id="reg-phone" label="Số điện thoại" value={phone} onChange={setPhone} type="tel" />
      <Field id="reg-name" label="Họ và tên" value={fullName} onChange={setFullName} />
      <Field id="reg-password" label="Mật khẩu" value={password} onChange={setPassword} type="password" />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 24 }} />}
      <Button type="primary" htmlType="submit" block loading={busy}>
        Đăng ký
      </Button>
      <Typography.Paragraph style={{ textAlign: 'center', marginTop: 16, marginBottom: 0 }}>
        Đã có tài khoản? <Link to="/login">Đăng nhập</Link>
      </Typography.Paragraph>
    </Form>
  );
}
function Field({ id, label, value, onChange, type = 'password' }) {
  const Control = type === 'password' ? Input.Password : Input;
  return (
    <Form.Item label={label} htmlFor={id}>
      <Control
        id={id}
        type={type === 'password' ? undefined : type}
        autoComplete={type === 'password' ? 'new-password' : undefined}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </Form.Item>
  );
}
