import { useState } from 'react';
import { Alert, Button, Card, Flex, Space, Table, Typography, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { postApiAdminUnitsImport } from '@/api/generated/endpoints/admin-units/admin-units';
import { errorMessage } from '@/lib/error-messages';
const unitImport = (payload, dryRun) => postApiAdminUnitsImport(payload, { dryRun });
/** Nhập GeoJSON: tải file, xem trước (dryRun), rồi xác nhận nhập thật (FE-21, FE-22). */
export function ImportPanel({ title = 'Nhập GeoJSON đơn vị', importFn = unitImport, onCancel, onDone }) {
  const [payload, setPayload] = useState(null);
  const [fileName, setFileName] = useState('');
  const [preview, setPreview] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const load = async (file) => {
    setError(null);
    setPreview(null);
    setFileName(file.name);
    try {
      setPayload(JSON.parse(await file.text()));
    } catch {
      setPayload(null);
      setError('File không phải JSON hợp lệ.');
    }
  };
  const run = async (dryRun) => {
    if (payload === null) return;
    setBusy(true);
    setError(null);
    try {
      const res = await importFn(payload, dryRun);
      if (dryRun) {
        setPreview(res.data);
      } else {
        onDone(`Đã nhập: thêm ${res.data.created}, cập nhật ${res.data.updated}, lỗi ${res.data.failed}.`);
      }
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  const columns = [
    { title: 'Mã', dataIndex: 'code', key: 'code', render: (code) => <Typography.Text code>{code}</Typography.Text> },
    {
      title: 'Thao tác',
      dataIndex: 'action',
      key: 'action',
      render: (action) => (action === 'CREATE' ? 'Thêm' : action === 'UPDATE' ? 'Sửa' : action),
    },
    {
      title: 'Ghi chú',
      key: 'note',
      render: (_, r) => (r.error ? <Typography.Text type="danger">{r.error}</Typography.Text> : r.warnings.join('; ')),
    },
  ];
  return (
    <Card title={title} aria-labelledby="import-title" style={{ maxWidth: 768 }}>
      <Flex vertical gap="middle">
        <Space wrap>
          <Upload
            accept=".json,.geojson"
            maxCount={1}
            showUploadList={false}
            beforeUpload={(f) => {
              void load(f);
              return false;
            }}
          >
            <Button icon={<UploadOutlined />} aria-label="Chọn file GeoJSON đơn vị">
              Chọn file GeoJSON đơn vị
            </Button>
          </Upload>
          {fileName && <Typography.Text type="secondary">Đã chọn: {fileName}</Typography.Text>}
        </Space>

        <Space wrap>
          <Button type="primary" disabled={payload === null || busy} onClick={() => void run(true)}>
            Xem trước
          </Button>
          <Button onClick={onCancel}>Hủy</Button>
        </Space>

        {error && <Alert type="error" showIcon title={error} role="alert" />}

        {preview && (
          <Flex vertical gap="middle">
            <Typography.Text>
              Thêm mới: {preview.created} · Cập nhật: {preview.updated} · Lỗi: {preview.failed}
            </Typography.Text>
            <Table
              size="small"
              columns={columns}
              dataSource={preview.rows.map((r, i) => ({ ...r, key: `${r.code}-${i}` }))}
              pagination={false}
              scroll={{ x: 'max-content', y: 288 }}
            />
            <div>
              <Button
                type="primary"
                disabled={busy || Number(preview.failed) > 0}
                loading={busy}
                onClick={() => void run(false)}
              >
                {Number(preview.failed) > 0 ? 'Sửa lỗi trong file rồi nhập lại' : 'Xác nhận nhập thật'}
              </Button>
            </div>
          </Flex>
        )}
      </Flex>
    </Card>
  );
}
