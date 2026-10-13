import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Checkbox, Col, Flex, Form, Input, Row, Select, Space, Table, Tag, Typography } from 'antd';
import {
  getGetApiCategoriesQueryKey,
  postApiCategories,
  putApiCategoriesId,
  useGetApiCategories,
} from '@/api/generated/endpoints/config/config';
import { Can } from '@/components/Can';
import { PRIORITY_OPTIONS, priorityText } from '@/lib/incident-labels';
import { errorMessage } from '@/lib/error-messages';
import { Example, FieldHint } from '@/components/FieldHint';
/** Danh mục sự cố (FE-23): thêm, sửa, bật tắt, mức ưu tiên mặc định. */
export function CategoriesPage() {
  const list = useGetApiCategories({ includeInactive: true });
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(null);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const items = list.data?.data ?? [];
  const refresh = () => queryClient.invalidateQueries({ queryKey: getGetApiCategoriesQueryKey() });
  const toggle = async (c) => {
    setError(null);
    try {
      await putApiCategoriesId(Number(c.id), {
        code: c.code,
        name: c.name,
        defaultPriority: Number(c.defaultPriority),
        isActive: !c.isActive,
      });
      setNotice(c.isActive ? `Đã tắt danh mục ${c.name}.` : `Đã bật danh mục ${c.name}.`);
      await refresh();
    } catch (err) {
      setError(errorMessage(err));
    }
  };
  const columns = [
    { title: 'Mã', dataIndex: 'code', key: 'code', render: (code) => <Typography.Text code>{code}</Typography.Text> },
    { title: 'Tên', dataIndex: 'name', key: 'name' },
    { title: 'Ưu tiên mặc định', key: 'priority', render: (_, c) => priorityText(c.defaultPriority) },
    {
      title: 'Trạng thái',
      key: 'status',
      render: (_, c) => <Tag color={c.isActive ? 'success' : 'default'}>{c.isActive ? 'Đang dùng' : 'Đã tắt'}</Tag>,
    },
    {
      title: 'Thao tác',
      key: 'actions',
      align: 'right',
      render: (_, c) => (
        <Can permission="category.manage">
          <Space size="small">
            <Button type="link" size="small" onClick={() => setEditing(c)}>
              Sửa
            </Button>
            <Button type="link" size="small" onClick={() => void toggle(c)}>
              {c.isActive ? 'Tắt' : 'Bật'}
            </Button>
          </Space>
        </Can>
      ),
    },
  ];
  return (
    <section aria-labelledby="cat-title">
      <Flex vertical gap="middle">
        <Flex align="center" justify="space-between" wrap gap="small">
          <Typography.Title level={3} id="cat-title" style={{ margin: 0 }}>
            Danh mục sự cố
          </Typography.Title>
          <Can permission="category.manage">
            <Button type="primary" onClick={() => setEditing('new')}>
              Thêm danh mục
            </Button>
          </Can>
        </Flex>
        {notice && <Alert type="success" showIcon title={notice} role="status" />}
        {error && <Alert type="error" showIcon title={error} role="alert" />}
        {list.isError && <Alert type="error" showIcon title={errorMessage(list.error)} role="alert" />}

        <Table
          aria-label="Danh mục sự cố"
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={list.isPending}
          pagination={false}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: 'Chưa có danh mục.' }}
        />

        {editing && (
          <CategoryForm
            key={editing === 'new' ? 'new' : editing.id}
            item={editing === 'new' ? null : editing}
            onCancel={() => setEditing(null)}
            onSaved={async (msg) => {
              setNotice(msg);
              setEditing(null);
              await refresh();
            }}
          />
        )}
      </Flex>
    </section>
  );
}
function CategoryForm({ item, onCancel, onSaved }) {
  const [code, setCode] = useState(item?.code ?? '');
  const [name, setName] = useState(item?.name ?? '');
  const [priority, setPriority] = useState(String(item?.defaultPriority ?? 2));
  const [isActive, setIsActive] = useState(item?.isActive ?? true);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    if (!code.trim() || !name.trim()) {
      setError('Hãy nhập mã và tên danh mục.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const body = { code: code.trim(), name: name.trim(), defaultPriority: Number(priority), isActive };
      if (item) await putApiCategoriesId(Number(item.id), body);
      else await postApiCategories(body);
      await onSaved(item ? 'Đã cập nhật danh mục.' : 'Đã thêm danh mục.');
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title={item ? 'Sửa danh mục' : 'Thêm danh mục'} style={{ maxWidth: 576 }}>
      <Form layout="vertical" aria-label={item ? 'Sửa danh mục' : 'Thêm danh mục'} onFinish={() => void submit()}>
        <Row gutter={16}>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Mã danh mục"
              htmlFor="cat-code"
              extra={
                <FieldHint>
                  Chữ in hoa không dấu, dùng gạch dưới. Ví dụ: <Example>RAC_THAI</Example>, <Example>DUONG_HONG</Example>,{' '}
                  <Example>LAN_CHIEM_VIA_HE</Example>.
                </FieldHint>
              }
            >
              <Input id="cat-code" value={code} placeholder="DUONG_HONG" onChange={(e) => setCode(e.target.value.toUpperCase())} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Tên hiển thị"
              htmlFor="cat-name"
              extra={
                <FieldHint>
                  Tên người dân và cán bộ sẽ thấy, ví dụ <Example>Đường hỏng, ổ gà</Example>.
                </FieldHint>
              }
            >
              <Input id="cat-name" value={name} placeholder="Đường hỏng" onChange={(e) => setName(e.target.value)} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item label="Ưu tiên mặc định" htmlFor="cat-priority">
              <Select
                id="cat-priority"
                value={priority}
                onChange={setPriority}
                options={PRIORITY_OPTIONS.map((p) => ({ value: String(p.value), label: p.label }))}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item label=" " colon={false}>
              <Checkbox checked={isActive} onChange={(e) => setIsActive(e.target.checked)}>
                Đang dùng
              </Checkbox>
            </Form.Item>
          </Col>
        </Row>
        {error && <Alert type="error" showIcon title={error} role="alert" style={{ marginBottom: 16 }} />}
        <Space>
          <Button type="primary" htmlType="submit" loading={busy} disabled={busy}>
            Lưu
          </Button>
          <Button onClick={onCancel}>Hủy</Button>
        </Space>
      </Form>
    </Card>
  );
}
