import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, App, Button, Card, Col, DatePicker, Flex, Form, Input, Row, Segmented, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import {
  deleteApiUsersIdRolesUserRoleId,
  getGetApiUsersIdQueryKey,
  getGetApiUsersQueryKey,
  postApiUsers,
  postApiUsersIdLock,
  postApiUsersIdResetPassword,
  postApiUsersIdRoles,
  postApiUsersIdUnlock,
  putApiUsersId,
  useGetApiRoles,
  useGetApiUsers,
  useGetApiUsersId,
} from '@/api/generated/endpoints/users/users';
import { Can } from '@/components/Can';
import { Example, FieldHint } from '@/components/FieldHint';
import { usePermission } from '@/features/auth/useAuth';
import { useUnitScope } from '@/features/command/UnitScopeContext';
import { formatDateTime } from '@/lib/datetime';
import { errorMessage } from '@/lib/error-messages';
/** Danh sách và tạo người dùng (FE-24). */
export function UsersPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') ?? '';
  const page = Number(params.get('page') ?? 1);
  const lockedOnly = params.get('locked') === '1';
  const list = useGetApiUsers({ q: q || undefined, page, pageSize: 20, locked: lockedOnly ? true : undefined });
  const [creating, setCreating] = useState(false);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const queryClient = useQueryClient();
  const canManage = usePermission('user.manage');
  const data = list.data?.data;
  const showLocked = (value) => {
    setParams({ q, ...(value ? { locked: '1' } : {}) }, { replace: true });
  };
  const unlock = async (id, name) => {
    setError(null);
    setNotice(null);
    try {
      await postApiUsersIdUnlock(Number(id));
      setNotice(`Đã mở khóa tài khoản ${name}.`);
      await queryClient.invalidateQueries({ queryKey: ['/api/users'] });
    } catch (err) {
      setError(errorMessage(err));
    }
  };
  const columns = [
    {
      title: 'Họ tên',
      key: 'fullName',
      render: (_, u) => <Link to={`/command/admin/users/${u.id}`}>{u.fullName}</Link>,
    },
    {
      title: 'Tài khoản',
      key: 'username',
      render: (_, u) => <span style={{ fontFamily: 'monospace' }}>{u.username ?? '—'}</span>,
    },
    {
      title: 'Vai trò',
      key: 'roles',
      render: (_, u) => u.roleSummary.join(', ') || '—',
    },
    {
      title: 'Trạng thái',
      key: 'status',
      render: (_, u) => {
        const lockedUntil = u.lockedUntil ? Date.parse(u.lockedUntil) : null;
        const isLocked = lockedUntil !== null && lockedUntil > Date.now();
        if (isLocked) {
          return (
            <Tag color="error">
              {lockedUntil > Date.parse('9999-01-01') ? 'Khóa thủ công' : `Đang khóa đến ${formatDateTime(u.lockedUntil)}`}
            </Tag>
          );
        }
        return u.isActive ? <Tag color="success">Hoạt động</Tag> : <Tag>Đã vô hiệu hóa</Tag>;
      },
    },
    {
      title: 'Đăng nhập gần nhất',
      key: 'lastLoginAt',
      render: (_, u) => (u.lastLoginAt ? formatDateTime(u.lastLoginAt) : 'Chưa đăng nhập'),
    },
    {
      title: <span style={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0,0,0,0)' }}>Thao tác</span>,
      key: 'actions',
      align: 'right',
      render: (_, u) => {
        const lockedUntil = u.lockedUntil ? Date.parse(u.lockedUntil) : null;
        const isLocked = lockedUntil !== null && lockedUntil > Date.now();
        return canManage && isLocked ? (
          <Button type="primary" ghost size="small" onClick={() => void unlock(u.id, u.fullName)}>
            Mở khóa
          </Button>
        ) : null;
      },
    },
  ];
  return (
    <Flex vertical gap="middle" component="section" aria-labelledby="users-title">
      <Flex wrap gap="small" align="center" justify="space-between">
        <Typography.Title level={3} id="users-title" style={{ margin: 0 }}>
          Người dùng
        </Typography.Title>
        <Segmented
          aria-label="Lọc người dùng"
          value={lockedOnly ? 'locked' : 'all'}
          onChange={(v) => showLocked(v === 'locked')}
          options={[
            { value: 'all', label: 'Tất cả' },
            { value: 'locked', label: `Đang bị khóa${lockedOnly && data ? ` (${data.totalCount})` : ''}` },
          ]}
        />
        <Space wrap>
          <Input
            aria-label="Tìm người dùng"
            placeholder="Tên, tài khoản, số điện thoại"
            defaultValue={q}
            allowClear
            onPressEnter={(e) => setParams({ q: e.target.value }, { replace: true })}
            style={{ width: 256, maxWidth: '100%' }}
          />
          <Can permission="user.manage">
            <Button type="primary" onClick={() => setCreating(true)}>
              Thêm người dùng
            </Button>
          </Can>
        </Space>
      </Flex>
      {notice && <Alert type="success" showIcon title={notice} role="status" />}
      {error && <Alert type="error" showIcon title={error} role="alert" />}
      {list.isError && <Alert type="error" showIcon title={errorMessage(list.error)} role="alert" />}

      {creating && (
        <UserCreateForm
          onCancel={() => setCreating(false)}
          onCreated={async (name) => {
            setCreating(false);
            setNotice(`Đã tạo người dùng ${name}.`);
            await queryClient.invalidateQueries({ queryKey: getGetApiUsersQueryKey() });
          }}
        />
      )}

      <Table
        rowKey="id"
        columns={columns}
        dataSource={data?.items ?? []}
        loading={list.isPending}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: lockedOnly ? 'Không có tài khoản nào đang bị khóa.' : 'Không tìm thấy người dùng.' }}
        pagination={
          data
            ? {
                current: page,
                pageSize: Number(data.pageSize),
                total: Number(data.totalCount),
                showSizeChanger: false,
                hideOnSinglePage: false,
                showTotal: (total) => `Trang ${data.page} · ${total} người dùng`,
                onChange: (p) => setParams({ q, page: String(p) }),
              }
            : false
        }
        aria-label="Danh sách người dùng"
      />
    </Flex>
  );
}
function UserCreateForm({ onCancel, onCreated }) {
  const [username, setUsername] = useState('');
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    if (!username.trim() || !fullName.trim() || password.length < 8) {
      setError('Hãy nhập tài khoản, họ tên và mật khẩu tối thiểu 8 ký tự.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await postApiUsers({ username: username.trim(), fullName: fullName.trim(), phone: phone || null, email: email || null, password });
      await onCreated(fullName.trim());
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card style={{ maxWidth: 768 }}>
      <Form layout="vertical" onFinish={submit} aria-label="Thêm người dùng">
        <Row gutter={16}>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Tài khoản đăng nhập"
              htmlFor="user-create-username"
              extra={
                <FieldHint>
                  Không dấu, không khoảng trắng, dùng để đăng nhập. Ví dụ: <Example>officer07</Example>, <Example>leader.bh</Example>.
                </FieldHint>
              }
            >
              <Input
                id="user-create-username"
                style={{ fontFamily: 'monospace' }}
                value={username}
                placeholder="officer07"
                onChange={(e) => setUsername(e.target.value)}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Họ và tên"
              htmlFor="user-create-fullname"
              extra={
                <FieldHint>
                  Họ tên đầy đủ có dấu, ví dụ <Example>Nguyễn Văn An</Example>.
                </FieldHint>
              }
            >
              <Input id="user-create-fullname" value={fullName} placeholder="Nguyễn Văn An" onChange={(e) => setFullName(e.target.value)} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Số điện thoại"
              htmlFor="user-create-phone"
              extra={
                <FieldHint>
                  10 chữ số, bắt đầu bằng 0. Ví dụ: <Example>0912345678</Example>. Có thể để trống.
                </FieldHint>
              }
            >
              <Input id="user-create-phone" type="tel" value={phone} placeholder="0912345678" onChange={(e) => setPhone(e.target.value)} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item label="Email" htmlFor="user-create-email">
              <Input id="user-create-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
            </Form.Item>
          </Col>
          <Col span={24}>
            <Form.Item label="Mật khẩu ban đầu" htmlFor="user-create-password">
              <Input.Password
                id="user-create-password"
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </Form.Item>
          </Col>
        </Row>
        {error && <Alert type="error" showIcon title={error} role="alert" style={{ marginBottom: 16 }} />}
        <Space>
          <Button type="primary" htmlType="submit" loading={busy}>
            Tạo người dùng
          </Button>
          <Button onClick={onCancel}>Hủy</Button>
        </Space>
      </Form>
    </Card>
  );
}
/** Chi tiết người dùng: bảng vai trò, gán vai trò có phạm vi và hạn dùng, thu hồi, khóa, đặt lại mật khẩu (FE-24). */
export function UserDetailPage() {
  const { modal } = App.useApp();
  const { id } = useParams();
  const userId = Number(id);
  const detail = useGetApiUsersId(userId, { query: { enabled: Number.isFinite(userId) } });
  const queryClient = useQueryClient();
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const canManage = usePermission('user.manage');
  const refresh = () => queryClient.invalidateQueries({ queryKey: getGetApiUsersIdQueryKey(userId) });
  const run = async (action, message) => {
    setError(null);
    setNotice(null);
    try {
      await action();
      setNotice(message);
      await refresh();
    } catch (err) {
      setError(errorMessage(err));
    }
  };
  if (detail.isPending)
    return (
      <Flex role="status" align="center" gap="small">
        <Spin size="small" />
        <Typography.Text type="secondary">Đang tải…</Typography.Text>
      </Flex>
    );
  if (detail.isError) return <Alert type="error" showIcon title={errorMessage(detail.error)} role="alert" />;
  const u = detail.data.data;
  const roleColumns = [
    { title: 'Vai trò', dataIndex: 'roleName', key: 'roleName' },
    { title: 'Phạm vi', key: 'scope', render: (_, r) => r.adminUnitName ?? 'Toàn hệ thống' },
    { title: 'Người gán', key: 'assignedBy', render: (_, r) => r.assignedByName ?? '—' },
    { title: 'Ngày gán', key: 'assignedAt', render: (_, r) => formatDateTime(r.assignedAt) },
    { title: 'Hạn dùng', key: 'expiresAt', render: (_, r) => (r.expiresAt ? formatDateTime(r.expiresAt) : 'Không hết hạn') },
    {
      title: 'Trạng thái',
      key: 'status',
      render: (_, r) => (r.isActive ? <Tag color="success">Đang hiệu lực</Tag> : <Tag>Đã hết hiệu lực</Tag>),
    },
    {
      title: <span style={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0,0,0,0)' }}>Thao tác</span>,
      key: 'actions',
      align: 'right',
      render: (_, r) =>
        canManage ? (
          <Button
            type="link"
            danger
            onClick={() => {
              modal.confirm({
                title: `Thu hồi vai trò ${r.roleName}? Người dùng phải đăng nhập lại để nhận quyền mới.`,
                okText: 'Thu hồi',
                okButtonProps: { danger: true },
                cancelText: 'Hủy',
                onOk: () =>
                  run(
                    () => deleteApiUsersIdRolesUserRoleId(userId, Number(r.id)),
                    'Đã thu hồi vai trò. Người dùng cần đăng nhập lại để nhận quyền mới.',
                  ),
              });
            }}
          >
            Thu hồi
          </Button>
        ) : null,
    },
  ];
  return (
    <Flex vertical gap="large" component="section" aria-labelledby="user-title">
      <Flex component="header" wrap gap="middle" align="flex-start" justify="space-between">
        <div>
          <Link to="/command/admin/users">Danh sách người dùng</Link>
          <Typography.Title level={3} id="user-title" style={{ margin: 0 }}>
            {u.fullName}
          </Typography.Title>
          <Typography.Text type="secondary">
            {u.username ?? '—'} · {u.phone ?? 'chưa có SĐT'} · {u.isActive ? 'hoạt động' : 'đã khóa tài khoản'}
            {u.lockedUntil ? ` · khóa tạm đến ${formatDateTime(u.lockedUntil)}` : ''}
          </Typography.Text>
        </div>
        <Can permission="user.manage">
          <Space wrap align="start">
            {u.isActive ? (
              <Button onClick={() => void run(() => postApiUsersIdLock(userId), 'Đã khóa tài khoản.')}>Khóa</Button>
            ) : (
              <Button onClick={() => void run(() => postApiUsersIdUnlock(userId), 'Đã mở khóa tài khoản.')}>Mở khóa</Button>
            )}
            <ResetPassword
              userId={userId}
              onDone={(m) => {
                setNotice(m);
              }}
              onError={setError}
            />
            <EditProfile
              key={u.id}
              user={u}
              onSaved={() => {
                setNotice('Đã lưu thông tin.');
                void refresh();
              }}
              onError={setError}
            />
          </Space>
        </Can>
      </Flex>

      {notice && <Alert type="success" showIcon title={notice} role="status" />}
      {error && <Alert type="error" showIcon title={error} role="alert" />}

      <Flex vertical gap="small" component="section" aria-labelledby="roles-title">
        <Typography.Title level={5} id="roles-title" style={{ margin: 0 }}>
          Vai trò và phạm vi
        </Typography.Title>
        <Table
          rowKey="id"
          columns={roleColumns}
          dataSource={u.roles}
          pagination={false}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: 'Chưa có vai trò.' }}
          aria-label="Vai trò được gán cho người dùng"
        />
        {canManage && (
          <AssignRole
            userId={userId}
            onDone={(m) => {
              setNotice(m);
              void refresh();
            }}
            onError={setError}
          />
        )}
      </Flex>
    </Flex>
  );
}
function AssignRole({ userId, onDone, onError }) {
  const roles = useGetApiRoles();
  const { options, global } = useUnitScope();
  const [roleId, setRoleId] = useState('');
  const [unitId, setUnitId] = useState('');
  const [expiresAt, setExpiresAt] = useState('');
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    if (!roleId) return onError('Hãy chọn vai trò.');
    if (!unitId && !global) return onError('Hãy chọn đơn vị phạm vi trong quyền của bạn.');
    setBusy(true);
    onError(null);
    try {
      await postApiUsersIdRoles(userId, {
        roleId: Number(roleId),
        adminUnitId: unitId ? Number(unitId) : null,
        expiresAt: expiresAt || null,
      });
      setRoleId('');
      setExpiresAt('');
      onDone('Đã gán vai trò.');
    } catch (err) {
      onError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card>
      <Form layout="vertical" onFinish={submit} aria-label="Gán vai trò">
        <Row gutter={16} align="bottom">
          <Col xs={24} sm={12} md={6}>
            <Form.Item label="Vai trò" htmlFor="assign-role-id">
              <Select
                id="assign-role-id"
                value={roleId}
                onChange={setRoleId}
                options={[
                  { value: '', label: 'Chọn vai trò' },
                  ...(roles.data?.data ?? []).filter((r) => r.isActive).map((r) => ({ value: String(r.id), label: r.roleName })),
                ]}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <Form.Item label="Phạm vi đơn vị" htmlFor="assign-unit-id">
              <Select
                id="assign-unit-id"
                value={unitId}
                onChange={setUnitId}
                options={[
                  { value: '', label: global ? 'Toàn hệ thống' : 'Chọn đơn vị' },
                  ...options.map((o) => ({ value: String(o.unitId), label: o.unitName })),
                ]}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <Form.Item label="Hạn dùng (tùy chọn)" htmlFor="assign-expires-at">
              <DatePicker
                id="assign-expires-at"
                style={{ width: '100%' }}
                format="DD/MM/YYYY"
                value={expiresAt ? dayjs(expiresAt) : null}
                onChange={(d) => setExpiresAt(d ? d.format('YYYY-MM-DD') : '')}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <Form.Item>
              <Button type="primary" htmlType="submit" loading={busy}>
                Gán vai trò
              </Button>
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Card>
  );
}
function ResetPassword({ userId, onDone, onError }) {
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState('');
  return (
    <>
      <Button onClick={() => setOpen(!open)}>Đặt lại mật khẩu</Button>
      {open && (
        <Form
          layout="inline"
          aria-label="Đặt lại mật khẩu"
          onFinish={async () => {
            if (password.length < 8) return onError('Mật khẩu mới tối thiểu 8 ký tự.');
            try {
              await postApiUsersIdResetPassword(userId, { newPassword: password });
              setPassword('');
              setOpen(false);
              onDone('Đã đặt lại mật khẩu. Người dùng cần đăng nhập lại.');
            } catch (err) {
              onError(errorMessage(err));
            }
          }}
        >
          <Space wrap>
            <Input.Password
              autoComplete="new-password"
              aria-label="Mật khẩu mới"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <Button type="primary" htmlType="submit">
              Lưu
            </Button>
          </Space>
        </Form>
      )}
    </>
  );
}
function EditProfile({ user, onSaved, onError }) {
  const [open, setOpen] = useState(false);
  const [fullName, setFullName] = useState(user.fullName);
  const [phone, setPhone] = useState(user.phone ?? '');
  const [email, setEmail] = useState(user.email ?? '');
  if (!open) return <Button onClick={() => setOpen(true)}>Sửa thông tin</Button>;
  return (
    <Form
      layout="inline"
      aria-label="Sửa thông tin người dùng"
      onFinish={async () => {
        try {
          await putApiUsersId(Number(user.id), { fullName, phone: phone || null, email: email || null, isActive: user.isActive });
          setOpen(false);
          onSaved();
        } catch (err) {
          onError(errorMessage(err));
        }
      }}
    >
      <Space wrap>
        <Input aria-label="Họ tên" value={fullName} onChange={(e) => setFullName(e.target.value)} />
        <Input aria-label="Số điện thoại" value={phone} onChange={(e) => setPhone(e.target.value)} />
        <Input aria-label="Email" value={email} onChange={(e) => setEmail(e.target.value)} />
        <Button type="primary" htmlType="submit">
          Lưu
        </Button>
      </Space>
    </Form>
  );
}
