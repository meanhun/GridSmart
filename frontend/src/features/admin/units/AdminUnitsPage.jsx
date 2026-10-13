import { useEffect, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { GeoJSON, useMap } from 'react-leaflet';
import L from 'leaflet';
import { Alert, Button, Card, Col, DatePicker, Descriptions, Empty, Flex, Form, Input, Row, Select, Space, Spin, Typography, Upload } from 'antd';
import { CaretDownOutlined, CaretRightOutlined, UploadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useGetApiAdminLevels,
  useGetApiAdminUnitTypes,
  useGetApiAdminUnitsId,
  useGetApiAdminUnitsTree,
  postApiAdminUnits,
  putApiAdminUnitsId,
} from '@/api/generated/endpoints/admin-units/admin-units';
import { BaseMap } from '@/components/map/BaseMap';
import { usePermission } from '@/features/auth/useAuth';
import { FieldHint, Example } from '@/components/FieldHint';
import { checkBoundaryText, GEOJSON_EXAMPLE, WKT_EXAMPLE } from '@/lib/boundary-input';
import { errorMessage } from '@/lib/error-messages';
import {
  allowedChildLevelOrder,
  ancestorIds,
  countByLevel,
  defaultCollapsedIds,
  flattenTree,
  parentIds,
  searchTree,
  unitFullName,
  visibleTreeRows,
} from '@/lib/admin-tree';
import { formatDateTime } from '@/lib/datetime';
import { isApiError } from '@/api/errors';
import { HighlightText } from '@/components/HighlightText';
import { TreeSearchBar } from '@/features/admin/TreeSearchBar';
import { useTreeCollapse } from '@/features/admin/useTreeCollapse';
import { ImportPanel } from './AdminUnitImport';
import { MergePanel } from './AdminUnitMerge';
/** Quản lý đơn vị hành chính (FE-21): cây bên trái (tìm kiếm, thu gọn), chi tiết và ranh giới bên phải. */
export function AdminUnitsPage() {
  const tree = useGetApiAdminUnitsTree({});
  const flat = useMemo(() => flattenTree(tree.data?.data ?? []), [tree.data]);
  const [query, setQuery] = useState('');
  const search = useMemo(() => searchTree(flat, query), [flat, query]);
  const defaultCollapsed = useMemo(() => defaultCollapsedIds(flat), [flat]);
  const collapse = useTreeCollapse(defaultCollapsed, search !== null);
  const rows = visibleTreeRows(flat, collapse.collapsed, search?.visibleIds ?? null);
  const [selectedId, setSelectedId] = useState(null);
  const [mode, setMode] = useState({ kind: 'view' });
  const [notice, setNotice] = useState(null);
  const canManage = usePermission('admin_unit.manage');
  const queryClient = useQueryClient();
  const detail = useGetApiAdminUnitsId(selectedId ?? 0, { query: { enabled: selectedId !== null } });
  const selected = flat.find((f) => Number(f.node.id) === selectedId)?.node;
  const refreshTree = () => queryClient.invalidateQueries({ queryKey: ['/api/admin-units'] });
  return (
    <section aria-labelledby="units-title">
      <Row gutter={[24, 24]}>
        <Col xs={24} lg={8}>
          <Flex vertical gap="small">
            <Typography.Title level={3} id="units-title" style={{ margin: 0 }}>
              Đơn vị hành chính
            </Typography.Title>
            {canManage && (
              <Space wrap size="small">
                <Button size="small" onClick={() => setMode({ kind: 'create' })}>
                  Thêm đơn vị
                </Button>
                <Button size="small" onClick={() => setMode({ kind: 'merge' })}>
                  Sáp nhập
                </Button>
                <Button size="small" onClick={() => setMode({ kind: 'import' })}>
                  Nhập GeoJSON
                </Button>
              </Space>
            )}
            {tree.isError && <Alert type="error" showIcon title={errorMessage(tree.error)} role="alert" />}
            {tree.isPending && (
              <div role="status">
                <Spin /> Đang tải cây…
              </div>
            )}
            <TreeSearchBar
              query={query}
              onQueryChange={(v) => {
                setQuery(v);
                collapse.resetSearch();
              }}
              placeholder="Tìm phường/xã, khu phố/thôn hoặc mã…"
              label="Tìm đơn vị hành chính"
              summary={searchSummary(flat, search)}
              onExpandAll={collapse.expandAll}
              onCollapseAll={() => collapse.collapseAll(parentIds(flat))}
            />
            <Card size="small" styles={{ body: { padding: 8, maxHeight: '70vh', overflowY: 'auto' } }}>
              <ul aria-label="Cây đơn vị" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                {rows.map((f) => {
                  const id = String(f.node.id);
                  return (
                    <TreeItem
                      key={id}
                      item={f}
                      query={query}
                      matched={search?.matchedIds.has(id) ?? false}
                      expanded={!collapse.collapsed.has(id)}
                      onToggle={() => collapse.toggle(id)}
                      active={Number(f.node.id) === selectedId}
                      onSelect={() => {
                        setSelectedId(Number(f.node.id));
                        setMode({ kind: 'view' });
                        // Xóa từ khóa sau khi chọn vẫn thấy đơn vị này trong cây
                        collapse.reveal(ancestorIds(flat, id));
                      }}
                    />
                  );
                })}
              </ul>
              {search && rows.length === 0 && (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={`Không có đơn vị nào khớp “${query.trim()}”.`} />
              )}
            </Card>
          </Flex>
        </Col>

        <Col xs={24} lg={16}>
          <Flex vertical gap="middle">
            {notice && <Alert type="success" showIcon title={notice} role="status" />}

            {mode.kind === 'merge' && (
              <MergePanel
                units={flat}
                onCancel={() => setMode({ kind: 'view' })}
                onDone={(msg) => {
                  setNotice(msg);
                  setMode({ kind: 'view' });
                  void refreshTree();
                }}
              />
            )}
            {mode.kind === 'import' && (
              <ImportPanel
                onCancel={() => setMode({ kind: 'view' })}
                onDone={(msg) => {
                  setNotice(msg);
                  setMode({ kind: 'view' });
                  void refreshTree();
                }}
              />
            )}
            {(mode.kind === 'create' || mode.kind === 'edit') && (
              <UnitForm
                key={`${mode.kind}-${selectedId ?? 'new'}`}
                flat={flat}
                editing={mode.kind === 'edit' ? detail.data?.data : undefined}
                onCancel={() => setMode({ kind: 'view' })}
                onSaved={(msg) => {
                  setNotice(msg);
                  setMode({ kind: 'view' });
                  void refreshTree();
                  void detail.refetch();
                }}
              />
            )}

            {mode.kind === 'view' && selectedId !== null && (
              <Flex vertical gap="middle">
                {detail.isPending && (
                  <div role="status">
                    <Spin /> Đang tải…
                  </div>
                )}
                {detail.isError && <Alert type="error" showIcon title={errorMessage(detail.error)} role="alert" />}
                {detail.data && (
                  <>
                    <header>
                      <Typography.Text type="secondary" code>
                        {detail.data.data.unitCode} · {detail.data.data.typeCode}
                      </Typography.Text>
                      <Typography.Title level={4} style={{ margin: '4px 0' }}>
                        {detail.data.data.name}
                      </Typography.Title>
                      <Typography.Text type="secondary">
                        {detail.data.data.path.map((p) => p.name).join(' › ')}
                        {detail.data.data.isActive ? '' : ' · đã ngừng hoạt động'}
                      </Typography.Text>
                    </header>
                    <Descriptions
                      size="small"
                      column={1}
                      items={[
                        { key: 'from', label: 'Hiệu lực từ', children: formatDateTime(detail.data.data.effectiveFrom) },
                        { key: 'children', label: 'Đơn vị con', children: detail.data.data.childCount },
                        { key: 'grids', label: 'Ô lưới', children: detail.data.data.gridCount },
                      ]}
                    />
                    {canManage && (
                      <div>
                        <Button onClick={() => setMode({ kind: 'edit' })}>Sửa đơn vị</Button>
                      </div>
                    )}
                    <BoundaryPreview key={detail.data.data.id} boundary={detail.data.data.boundary} />
                  </>
                )}
                {!detail.data && selected && <Typography.Text type="secondary">{selected.name}</Typography.Text>}
              </Flex>
            )}
            {mode.kind === 'view' && selectedId === null && (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chọn một đơn vị trong cây để xem chi tiết." />
            )}
          </Flex>
        </Col>
      </Row>
    </section>
  );
}
/** Dòng tóm tắt dưới ô tìm kiếm, ví dụ "Tìm thấy 1 phường/xã, 3 khu phố/thôn". */
function searchSummary(flat, search) {
  if (!search) {
    const counts = countByLevel(flat, new Set(flat.map((f) => String(f.node.id))));
    return `${counts.COMMUNE ?? 0} phường/xã · ${counts.HAMLET ?? 0} khu phố/thôn`;
  }
  const counts = countByLevel(flat, search.matchedIds);
  const parts = [
    counts.PROVINCE && `${counts.PROVINCE} tỉnh/TP`,
    counts.COMMUNE && `${counts.COMMUNE} phường/xã`,
    counts.HAMLET && `${counts.HAMLET} khu phố/thôn`,
  ].filter(Boolean);
  return parts.length > 0 ? `Tìm thấy ${parts.join(', ')}` : 'Không tìm thấy';
}
function TreeItem({ item, query, matched, expanded, onToggle, active, onSelect }) {
  const childCount = (item.node.children ?? []).length;
  return (
    <li>
      <Flex align="center" style={{ paddingLeft: item.depth * 16 }}>
        {childCount > 0 ? (
          <Button
            type="text"
            size="small"
            icon={expanded ? <CaretDownOutlined aria-hidden="true" /> : <CaretRightOutlined aria-hidden="true" />}
            aria-label={`${expanded ? 'Thu gọn' : 'Mở rộng'} ${item.node.name}`}
            aria-expanded={expanded}
            onClick={onToggle}
            style={{ flex: 'none' }}
          />
        ) : (
          <span style={{ width: 24, flex: 'none' }} aria-hidden="true" />
        )}
        <Button
          type={active ? 'default' : 'text'}
          block
          onClick={onSelect}
          aria-current={active ? 'true' : undefined}
          style={{
            textAlign: 'left',
            justifyContent: 'flex-start',
            paddingLeft: 6,
            fontWeight: active || matched ? 500 : undefined,
            textDecoration: item.node.isActive ? undefined : 'line-through',
            opacity: item.node.isActive ? undefined : 0.5,
          }}
        >
          <span>
            <HighlightText text={item.node.name} query={query} />{' '}
            <Typography.Text type="secondary">
              {item.node.typeName}
              {childCount > 0 && !expanded ? ` · ${childCount}` : ''}
            </Typography.Text>
          </span>
        </Button>
      </Flex>
    </li>
  );
}
/** Ranh giới đơn vị (GeoJSON). Backend trả null nếu chưa có. Bản đồ được tạo lại theo từng đơn vị (xem key ở nơi gọi). */
function BoundaryPreview({ boundary }) {
  const geometry = boundary;
  if (!geometry?.type) return <Typography.Text type="secondary">Chưa có ranh giới.</Typography.Text>;
  return (
    <BaseMap label="Ranh giới đơn vị" height="320px" showLocateButton={false}>
      <GeoJSON data={geometry} style={{ color: '#21A691', weight: 2, fillOpacity: 0.1 }} />
      <FitToGeometry geometry={geometry} />
    </BaseMap>
  );
}
/** Căn khung bản đồ vừa với ranh giới đang xem (tỉnh rộng, phường/khu phố hẹp). */
function FitToGeometry({ geometry }) {
  const map = useMap();
  useEffect(() => {
    const bounds = L.geoJSON(geometry).getBounds();
    if (bounds.isValid()) map.fitBounds(bounds, { padding: [16, 16], maxZoom: 17 });
  }, [geometry, map]);
  return null;
}
/** Thêm mới hoặc sửa đơn vị. Loại đơn vị lọc theo cấp hợp lệ của đơn vị cha (FE-21). */
function UnitForm({ flat, editing, onCancel, onSaved }) {
  const types = useGetApiAdminUnitTypes();
  const levels = useGetApiAdminLevels();
  const [unitCode, setUnitCode] = useState(editing?.unitCode ?? '');
  const [name, setName] = useState(editing?.name ?? '');
  const [unitTypeId, setUnitTypeId] = useState(String(editing?.unitTypeId ?? ''));
  const [parentId, setParentId] = useState(editing?.parentId != null ? String(editing.parentId) : '');
  const [effectiveFrom, setEffectiveFrom] = useState(
    editing?.effectiveFrom ? editing.effectiveFrom.slice(0, 10) : new Date().toISOString().slice(0, 10),
  );
  const [boundaryText, setBoundaryText] = useState('');
  const [error, setError] = useState(null);
  const [parentError, setParentError] = useState(null);
  const [busy, setBusy] = useState(false);
  const levelOrderOf = (levelCode) => {
    const order = levels.data?.data.find((l) => l.levelCode === levelCode)?.levelOrder;
    return order === undefined ? undefined : Number(order);
  };
  const parent = flat.find((f) => String(Number(f.node.id)) === parentId)?.node;
  const requiredOrder = allowedChildLevelOrder(parent ? (levelOrderOf(parent.levelCode) ?? Number(parent.levelOrder)) : null);
  const allowedTypes = (types.data?.data ?? []).filter((t) => levelOrderOf(t.levelCode) === requiredOrder);
  const typeOptions = allowedTypes.length > 0 ? allowedTypes : (types.data?.data ?? []);
  const submit = async () => {
    setError(null);
    setParentError(null);
    if (!unitCode.trim() || !name.trim() || !unitTypeId) {
      setError('Hãy nhập mã đơn vị, tên đơn vị và chọn loại đơn vị (ví dụ: khu phố).');
      return;
    }
    const boundaryProblem = checkBoundaryText(boundaryText);
    if (boundaryProblem) {
      setError(boundaryProblem);
      return;
    }
    setBusy(true);
    try {
      const body = {
        unitCode: unitCode.trim(),
        name: name.trim(),
        unitTypeId: Number(unitTypeId),
        parentId: parentId ? Number(parentId) : null,
        effectiveFrom,
        isActive: true,
        ...boundaryPayload(boundaryText),
      };
      if (editing) await putApiAdminUnitsId(Number(editing.id), body);
      else await postApiAdminUnits(body);
      onSaved(editing ? 'Đã cập nhật đơn vị.' : 'Đã thêm đơn vị.');
    } catch (err) {
      if (err instanceof SyntaxError) {
        setError('Ranh giới GeoJSON không đúng định dạng JSON.');
        return;
      }
      const message = errorMessage(err);
      if (isApiError(err) && (err.code === 'ADMIN_UNIT_INVALID_LEVEL' || err.code === 'ADMIN_UNIT_CYCLE')) setParentError(message);
      else setError(message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title={editing ? 'Sửa đơn vị' : 'Thêm đơn vị'} style={{ maxWidth: 672 }}>
      <Form
        layout="vertical"
        aria-label={editing ? 'Sửa đơn vị' : 'Thêm đơn vị'}
        onFinish={() => {
          void submit();
        }}
      >
        <Row gutter={16}>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Mã đơn vị"
              htmlFor="unit-code"
              extra={
                <FieldHint>
                  Ghép mã cha với mã riêng. Ví dụ khu phố: <Example>DNI-BH-KP03</Example> (Đồng Nai › Biên Hòa › khu phố 3). Mã không được
                  trùng.
                </FieldHint>
              }
            >
              <Input id="unit-code" value={unitCode} placeholder="DNI-BH-KP03" onChange={(e) => setUnitCode(e.target.value)} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Tên đơn vị"
              htmlFor="unit-name"
              extra={
                <FieldHint>
                  Chỉ ghi tên riêng, ví dụ <Example>Khu phố 3</Example>. Hệ thống tự hiển thị kèm phường/tỉnh ở các danh sách.
                </FieldHint>
              }
            >
              <Input id="unit-name" value={name} placeholder="Khu phố 3" onChange={(e) => setName(e.target.value)} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Đơn vị cha"
              htmlFor="unit-parent"
              validateStatus={parentError ? 'error' : undefined}
              help={parentError ? <span role="alert">{parentError}</span> : undefined}
              extra={
                <FieldHint>
                  Khu phố thuộc phường, phường thuộc tỉnh. Ví dụ: <Example>Khu phố 3 · Phường Biên Hòa · Tỉnh Đồng Nai</Example>
                </FieldHint>
              }
            >
              <Select
                id="unit-parent"
                showSearch
                optionFilterProp="label"
                value={parentId}
                onChange={(v) => {
                  setParentId(v);
                  setUnitTypeId('');
                }}
                options={[
                  { value: '', label: 'Không có (cấp cao nhất: tỉnh)' },
                  ...flat.map((f) => ({ value: String(Number(f.node.id)), label: unitFullName(flat, f.node.id) })),
                ]}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item label="Loại đơn vị" htmlFor="unit-type">
              <Select
                id="unit-type"
                value={unitTypeId === '' ? undefined : unitTypeId}
                placeholder="Chọn loại"
                onChange={(v) => setUnitTypeId(v ?? '')}
                options={typeOptions.map((t) => ({ value: String(t.id), label: t.typeName }))}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              label="Hiệu lực từ"
              htmlFor="unit-effective-from"
              extra={
                <FieldHint>
                  Ngày đơn vị bắt đầu hoạt động, ví dụ <Example>2026-01-01</Example>.
                </FieldHint>
              }
            >
              <DatePicker
                id="unit-effective-from"
                allowClear={false}
                style={{ width: '100%' }}
                format="YYYY-MM-DD"
                value={effectiveFrom ? dayjs(effectiveFrom) : null}
                onChange={(d) => setEffectiveFrom(d ? d.format('YYYY-MM-DD') : '')}
              />
            </Form.Item>
          </Col>
        </Row>
        <Form.Item
          label="Ranh giới khu vực (tùy chọn)"
          htmlFor="boundary"
          extra={
            <Flex vertical>
              <FieldHint>
                WKT: <Example>{WKT_EXAMPLE}</Example>
              </FieldHint>
              <FieldHint>
                GeoJSON: <Example>{GEOJSON_EXAMPLE}</Example>
              </FieldHint>
              <FieldHint>
                Lưu ý thứ tự: <em>kinh độ trước, vĩ độ sau</em> (ví dụ 106.82 10.94). Tọa độ phải nằm trong tỉnh Đồng Nai.
              </FieldHint>
            </Flex>
          }
        >
          <Input.TextArea
            id="boundary"
            rows={3}
            style={{ fontFamily: 'monospace', fontSize: 12 }}
            value={boundaryText}
            onChange={(e) => setBoundaryText(e.target.value)}
            placeholder={WKT_EXAMPLE}
          />
        </Form.Item>
        <Form.Item>
          <Upload
            accept=".json,.geojson"
            maxCount={1}
            showUploadList={false}
            beforeUpload={(f) => {
              void f.text().then(setBoundaryText);
              return false;
            }}
          >
            <Button icon={<UploadOutlined />} aria-label="Tải file GeoJSON ranh giới">
              Tải file GeoJSON ranh giới
            </Button>
          </Upload>
        </Form.Item>
        {error && <Alert type="error" showIcon title={error} role="alert" style={{ marginBottom: 16 }} />}
        <Space>
          <Button type="primary" htmlType="submit" loading={busy} disabled={busy}>
            {busy ? 'Đang lưu…' : 'Lưu'}
          </Button>
          <Button onClick={onCancel}>Hủy</Button>
        </Space>
      </Form>
    </Card>
  );
}
/** Chuyển ô nhập ranh giới thành trường boundary (GeoJSON) hoặc wkt. */
export function boundaryPayload(text) {
  const trimmed = text.trim();
  if (!trimmed) return {};
  if (trimmed.startsWith('{')) return { boundary: JSON.parse(trimmed) };
  return { wkt: trimmed };
}
