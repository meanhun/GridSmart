import { useMemo, useState } from 'react';
import { Alert, App, Button, Checkbox, Flex, Space, Table, Typography } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import {
  deleteApiRolesId,
  getGetApiRolesQueryKey,
  putApiRolesIdPermissions,
  useGetApiPermissions,
  useGetApiRoles,
} from '@/api/generated/endpoints/users/users';
import { usePermission } from '@/features/auth/useAuth';
import { errorMessage } from '@/lib/error-messages';
/** Ma trận vai trò × quyền, nhóm theo module (FE-24). Vai trò hệ thống không xóa được. */
export function RolesPage() {
  const { modal } = App.useApp();
  const roles = useGetApiRoles();
  const perms = useGetApiPermissions();
  const canManage = usePermission('role.manage');
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState({});
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(null);
  const roleList = useMemo(() => roles.data?.data ?? [], [roles.data]);
  const permList = useMemo(() => perms.data?.data ?? [], [perms.data]);
  const modules = useMemo(() => [...new Set(permList.map((p) => p.module))], [permList]);
  const current = (roleId, base) => draft[roleId] ?? base;
  const toggle = (roleId, base, code) => {
    const set = new Set(current(roleId, base));
    if (set.has(code)) set.delete(code);
    else set.add(code);
    setDraft({ ...draft, [roleId]: [...set] });
  };
  const save = async (roleId, base) => {
    setBusy(roleId);
    setError(null);
    setNotice(null);
    try {
      await putApiRolesIdPermissions(roleId, current(roleId, base));
      setDraft(({ [roleId]: _omit, ...rest }) => rest);
      setNotice('Đã lưu quyền của vai trò. Người dùng đang đăng nhập cần đăng nhập lại để nhận quyền mới.');
      await queryClient.invalidateQueries({ queryKey: getGetApiRolesQueryKey() });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(null);
    }
  };
  const doRemove = async (roleId, name) => {
    try {
      await deleteApiRolesId(roleId);
      await queryClient.invalidateQueries({ queryKey: getGetApiRolesQueryKey() });
      setNotice(`Đã xóa vai trò ${name}.`);
    } catch (err) {
      setError(errorMessage(err));
    }
  };
  const remove = (roleId, name) => {
    modal.confirm({
      title: `Xóa vai trò ${name}?`,
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Hủy',
      onOk: () => doRemove(roleId, name),
    });
  };

  // Hàng của bảng: dòng nhóm module, dòng quyền, và dòng cuối chứa nút lưu/xóa.
  const rows = [
    ...modules.flatMap((m) => [
      { key: `module:${m}`, kind: 'group', module: m },
      ...permList.filter((p) => p.module === m).map((p) => ({ key: `perm:${p.code}`, kind: 'perm', perm: p })),
    ]),
    { key: 'actions', kind: 'actions' },
  ];
  const groupCell = (row, index) => {
    if (row.kind !== 'group') return {};
    return { colSpan: index === 0 ? roleList.length + 1 : 0 };
  };
  const columns = [
    {
      title: 'Quyền',
      key: 'permission',
      fixed: 'left',
      onCell: (row) => groupCell(row, 0),
      render: (_, row) => {
        if (row.kind === 'group') return <Typography.Text strong>{row.module}</Typography.Text>;
        if (row.kind === 'actions') return 'Lưu và quản lý';
        return (
          <>
            {row.perm.name}{' '}
            <Typography.Text type="secondary" code style={{ fontSize: 12 }}>
              {row.perm.code}
            </Typography.Text>
          </>
        );
      },
    },
    ...roleList.map((r) => ({
      key: String(r.id),
      align: 'center',
      title: (
        <>
          {r.roleName}
          {r.isSystem && (
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 10, fontWeight: 'normal' }}>
              hệ thống
            </Typography.Text>
          )}
        </>
      ),
      onCell: (row) => groupCell(row, 1),
      render: (_, row) => {
        if (row.kind === 'group') return null;
        if (row.kind === 'perm') {
          return (
            <Checkbox
              aria-label={`${r.roleName}: ${row.perm.name}`}
              disabled={!canManage || busy !== null}
              checked={current(Number(r.id), r.permissions).includes(row.perm.code)}
              onChange={() => toggle(Number(r.id), r.permissions, row.perm.code)}
            />
          );
        }
        const dirty = draft[Number(r.id)] !== undefined;
        return (
          <Space vertical size={4}>
            {canManage && dirty && (
              <Button
                type="primary"
                size="small"
                disabled={busy !== null}
                loading={busy === Number(r.id)}
                onClick={() => void save(Number(r.id), r.permissions)}
              >
                Lưu
              </Button>
            )}
            {canManage && !r.isSystem && (
              <Button type="link" danger size="small" onClick={() => remove(Number(r.id), r.roleName)}>
                Xóa
              </Button>
            )}
          </Space>
        );
      },
    })),
  ];

  return (
    <Flex vertical gap="middle" component="section" aria-labelledby="roles-page-title">
      <Typography.Title level={3} id="roles-page-title" style={{ margin: 0 }}>
        Vai trò và quyền
      </Typography.Title>
      {notice && <Alert type="success" showIcon title={notice} role="status" />}
      {error && <Alert type="error" showIcon title={error} role="alert" />}
      {(roles.isError || perms.isError) && (
        <Alert type="error" showIcon title={errorMessage(roles.error ?? perms.error)} role="alert" />
      )}

      <Table
        size="small"
        bordered
        rowKey="key"
        columns={columns}
        dataSource={rows}
        pagination={false}
        loading={roles.isPending || perms.isPending}
        scroll={{ x: 'max-content' }}
        aria-label="Ma trận vai trò và quyền. Tích để cấp quyền."
      />
    </Flex>
  );
}
