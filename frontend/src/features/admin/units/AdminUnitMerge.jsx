import { useState } from 'react';
import { Alert, Button, Card, Checkbox, DatePicker, Flex, Input, Radio, Result, Select, Space, Typography } from 'antd';
import dayjs from 'dayjs';
import { postApiAdminUnitsMerge, useGetApiAdminUnitTypes } from '@/api/generated/endpoints/admin-units/admin-units';
import { errorMessage } from '@/lib/error-messages';
/**
 * Trình hướng dẫn sáp nhập (FE-21): chọn đơn vị nguồn, chọn hoặc tạo đơn vị đích, ngày hiệu lực,
 * xem trước rồi mới xác nhận. Số người dùng bị ảnh hưởng chỉ biết chắc khi backend thực hiện.
 */
export function MergePanel({ units, onCancel, onDone }) {
  const types = useGetApiAdminUnitTypes();
  const [step, setStep] = useState(1);
  const [sourceIds, setSourceIds] = useState([]);
  const [targetMode, setTargetMode] = useState('existing');
  const [targetId, setTargetId] = useState('');
  const [newCode, setNewCode] = useState('');
  const [newName, setNewName] = useState('');
  const [newType, setNewType] = useState('');
  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const [error, setError] = useState(null);
  const [result, setResult] = useState(null);
  const [busy, setBusy] = useState(false);
  const sources = units.filter((u) => sourceIds.includes(Number(u.node.id)));
  const childCount = sources.reduce((sum, u) => sum + (u.node.children?.length ?? 0), 0);
  const targetValid =
    targetMode === 'existing'
      ? targetId !== '' && !sourceIds.includes(Number(targetId))
      : newCode.trim() !== '' && newName.trim() !== '' && newType !== '';
  const toggle = (id) => setSourceIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));
  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await postApiAdminUnitsMerge({
        sourceUnitIds: sourceIds,
        targetUnitId: targetMode === 'existing' ? Number(targetId) : undefined,
        newUnit:
          targetMode === 'new'
            ? { unitCode: newCode.trim(), name: newName.trim(), unitTypeId: Number(newType), parentId: undefined }
            : undefined,
        effectiveDate,
      });
      setResult(res.data);
      setStep(3);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  if (step === 3 && result) {
    return (
      <Card style={{ maxWidth: 672 }} role="status">
        <Result
          status="success"
          title="Đã sáp nhập"
          subTitle={
            <ul style={{ textAlign: 'left', display: 'inline-block', paddingLeft: 20, margin: 0 }}>
              <li>Đơn vị đích: mã nội bộ {result.targetUnitId}</li>
              <li>Đơn vị con chuyển sang: {result.movedChildren}</li>
              <li>Ô lưới chuyển sang: {result.movedGrids}</li>
              <li>Phân quyền được chuyển: {result.movedRoleAssignments}</li>
              <li>Người dùng bị ảnh hưởng: {result.affectedUsers} (cần đăng nhập lại để nhận quyền mới)</li>
            </ul>
          }
          extra={
            <Button type="primary" onClick={() => onDone('Đã sáp nhập đơn vị.')}>
              Xong
            </Button>
          }
        />
      </Card>
    );
  }
  return (
    <Card title={`Sáp nhập đơn vị (bước ${step}/2)`} style={{ maxWidth: 672 }}>
      {step === 1 && (
        <Flex vertical gap="middle">
          <div role="group" aria-label="Đơn vị nguồn (sẽ được sáp nhập)">
            <Typography.Text strong>Đơn vị nguồn (sẽ được sáp nhập)</Typography.Text>
            <div style={{ maxHeight: 224, overflowY: 'auto', border: '1px solid #B4B4B2', borderRadius: 6, padding: 8, marginTop: 4 }}>
              {units.map((u) => (
                <div key={Number(u.node.id)} style={{ paddingLeft: u.depth * 14 }}>
                  <Checkbox checked={sourceIds.includes(Number(u.node.id))} onChange={() => toggle(Number(u.node.id))}>
                    {u.node.name} <Typography.Text type="secondary">{u.node.typeName}</Typography.Text>
                  </Checkbox>
                </div>
              ))}
            </div>
          </div>

          <div role="group" aria-label="Đơn vị đích">
            <Typography.Text strong>Đơn vị đích</Typography.Text>
            <Flex vertical gap="small" style={{ marginTop: 4 }}>
              <Radio checked={targetMode === 'existing'} onChange={() => setTargetMode('existing')}>
                Chọn đơn vị có sẵn
              </Radio>
              {targetMode === 'existing' && (
                <Select
                  aria-label="Đơn vị đích có sẵn"
                  style={{ width: '100%' }}
                  showSearch
                  optionFilterProp="label"
                  value={targetId === '' ? undefined : targetId}
                  onChange={(v) => setTargetId(v ?? '')}
                  placeholder="Chọn đơn vị"
                  allowClear
                  options={units
                    .filter((u) => !sourceIds.includes(Number(u.node.id)))
                    .map((u) => ({ value: String(Number(u.node.id)), label: u.node.name }))}
                />
              )}
              <Radio checked={targetMode === 'new'} onChange={() => setTargetMode('new')}>
                Tạo đơn vị mới
              </Radio>
              {targetMode === 'new' && (
                <Flex wrap gap="small">
                  <Input
                    aria-label="Mã đơn vị mới"
                    placeholder="Mã"
                    style={{ flex: '1 1 140px' }}
                    value={newCode}
                    onChange={(e) => setNewCode(e.target.value)}
                  />
                  <Input
                    aria-label="Tên đơn vị mới"
                    placeholder="Tên"
                    style={{ flex: '1 1 140px' }}
                    value={newName}
                    onChange={(e) => setNewName(e.target.value)}
                  />
                  <Select
                    aria-label="Loại đơn vị mới"
                    style={{ flex: '1 1 140px' }}
                    placeholder="Loại"
                    allowClear
                    value={newType === '' ? undefined : newType}
                    onChange={(v) => setNewType(v ?? '')}
                    options={(types.data?.data ?? []).map((t) => ({ value: String(t.id), label: t.typeName }))}
                  />
                </Flex>
              )}
            </Flex>
          </div>

          <Space wrap>
            <label htmlFor="merge-effective-date">Ngày hiệu lực</label>
            <DatePicker
              id="merge-effective-date"
              allowClear={false}
              format="YYYY-MM-DD"
              value={effectiveDate ? dayjs(effectiveDate) : null}
              onChange={(d) => setEffectiveDate(d ? d.format('YYYY-MM-DD') : '')}
            />
          </Space>

          <Space wrap>
            <Button type="primary" disabled={sourceIds.length < 2 || !targetValid} onClick={() => setStep(2)}>
              Xem trước
            </Button>
            <Button onClick={onCancel}>Hủy</Button>
          </Space>
          {sourceIds.length < 2 && <Typography.Text type="secondary">Chọn ít nhất hai đơn vị nguồn.</Typography.Text>}
        </Flex>
      )}

      {step === 2 && (
        <Flex vertical gap="middle">
          <Alert
            type="warning"
            showIcon
            title={`Sẽ sáp nhập ${sources.length} đơn vị vào ${
              targetMode === 'existing' ? 'đơn vị đích đã chọn' : `đơn vị mới "${newName.trim()}"`
            } có hiệu lực từ ${effectiveDate}.`}
            description={
              <Flex vertical gap="small">
                <ul style={{ paddingLeft: 20, margin: 0 }}>
                  {sources.map((u) => (
                    <li key={Number(u.node.id)}>{u.node.name}</li>
                  ))}
                </ul>
                <span>
                  Đơn vị con trực tiếp sẽ chuyển sang đơn vị đích: khoảng {childCount}. Số ô lưới và người dùng bị ảnh hưởng sẽ được tính khi
                  thực hiện.
                </span>
                <Typography.Text type="danger">Thao tác này không hoàn tác được từ giao diện.</Typography.Text>
              </Flex>
            }
          />
          {error && <Alert type="error" showIcon title={error} role="alert" />}
          <Space wrap>
            <Button type="primary" danger loading={busy} disabled={busy} onClick={() => void confirm()}>
              {busy ? 'Đang sáp nhập…' : 'Xác nhận sáp nhập'}
            </Button>
            <Button onClick={() => setStep(1)}>Quay lại</Button>
          </Space>
        </Flex>
      )}
    </Card>
  );
}
