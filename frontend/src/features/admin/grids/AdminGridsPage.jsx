import { useEffect, useMemo, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { GeoJSON, useMap } from 'react-leaflet';
import L from 'leaflet';
import { Alert, App, Button, Card, Checkbox, Flex, Form, Input, Radio, Select, Space, Table, Typography } from 'antd';
import {
  postApiGrids,
  postApiGridsImport,
  putApiGridsId,
  putApiGridsIdOfficers,
  useGetApiGrids,
  useGetApiGridsId,
} from '@/api/generated/endpoints/grids/grids';
import { useGetApiOfficers } from '@/api/generated/endpoints/incidents/incidents';
import { useGetApiAdminUnitsTree } from '@/api/generated/endpoints/admin-units/admin-units';
import { BaseMap } from '@/components/map/BaseMap';
import { usePermission } from '@/features/auth/useAuth';
import { useUnitScope } from '@/features/command/UnitScopeContext';
import { ImportPanel } from '@/features/admin/units/AdminUnitImport';
import {
  ancestorIds,
  defaultCollapsedGroupKeys,
  flattenTree,
  gridMatches,
  groupGridsByUnit,
  groupRowKeys,
  unitFullName,
} from '@/lib/admin-tree';
import { HighlightText } from '@/components/HighlightText';
import { TreeSearchBar } from '@/features/admin/TreeSearchBar';
import { useTreeCollapse } from '@/features/admin/useTreeCollapse';
import { checkBoundaryText, geometryToWkt, GEOJSON_EXAMPLE, WKT_EXAMPLE } from '@/lib/boundary-input';
import { Example, FieldHint } from '@/components/FieldHint';
import { errorMessage } from '@/lib/error-messages';
import { gridFeatures } from '@/lib/grid-geojson';
import { GridDrawTool } from './GridDrawTool';
import { gridColor } from '@/lib/incident-labels';
/**
 * Quản lý ô lưới (FE-22): bản đồ và bảng trong phạm vi, vẽ/sửa ranh giới, phân công cán bộ, nhập GeoJSON.
 * Bảng nhóm theo Tỉnh/TP › Phường/xã › Khu phố/thôn, thu gọn được; ô tìm kiếm lọc cả bảng và bản đồ.
 */
export function AdminGridsPage() {
  const { unitId } = useUnitScope();
  const grids = useGetApiGrids(unitId ? { unitId } : undefined);
  const allFeatures = gridFeatures(grids.data?.data);
  const tree = useGetApiAdminUnitsTree({});
  const units = useMemo(() => flattenTree(tree.data?.data ?? []), [tree.data]);
  const [query, setQuery] = useState('');
  const searching = query.trim() !== '';
  const features = useMemo(
    () => (searching ? allFeatures.filter((f) => gridMatches(f, query)) : allFeatures),
    [allFeatures, query, searching],
  );
  const groupRows = useMemo(() => groupGridsByUnit(units, features), [units, features]);
  const allGroupKeys = useMemo(() => groupRowKeys(groupRows), [groupRows]);
  const defaultCollapsed = useMemo(() => defaultCollapsedGroupKeys(groupRows), [groupRows]);
  const collapse = useTreeCollapse(defaultCollapsed, searching);
  const expandedRowKeys = allGroupKeys.filter((k) => !collapse.collapsed.has(k));
  const [mode, setMode] = useState({ kind: 'view' });
  // Bản đồ gắn sự kiện một lần, nên dùng ref để luôn đọc chế độ hiện tại.
  const modeRef = useRef(mode.kind);
  modeRef.current = mode.kind;
  const [drawn, setDrawn] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [notice, setNotice] = useState(null);
  const [warnings, setWarnings] = useState([]);
  const canManage = usePermission('grid.manage');
  const canAssign = usePermission('grid.assign_officer');
  const queryClient = useQueryClient();
  // Ô lưới thay đổi thì số ô lưới của đơn vị (gridCount) cũng thay đổi, nên làm mới cả dữ liệu đơn vị.
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['/api/grids'] }),
      queryClient.invalidateQueries({ queryKey: ['/api/admin-units'] }),
    ]);
  const detail = useGetApiGridsId(selectedId ?? 0, { query: { enabled: selectedId !== null } });
  const detailGrid = detail.data?.data;
  const done = (message, extraWarnings = []) => {
    setNotice(message);
    setWarnings(extraWarnings);
    setDrawn(null);
    setMode({ kind: 'view' });
    void refresh();
    void detail.refetch();
  };
  // Chọn ô lưới trên bản đồ thì mở nhánh chứa nó, để dòng tương ứng hiện trong bảng.
  const selectGrid = (gridId) => {
    setSelectedId(gridId);
    const f = allFeatures.find((x) => x.properties.gridId === gridId);
    if (f) {
      const unit = String(f.properties.adminUnitId);
      collapse.reveal([unit, ...ancestorIds(units, unit)].map((id) => `u-${id}`));
    }
  };
  // Lớp bản đồ gắn sự kiện một lần, nên đọc hàm chọn mới nhất qua ref.
  const selectGridRef = useRef(selectGrid);
  useEffect(() => {
    selectGridRef.current = selectGrid;
  });
  // Dòng đơn vị gộp ba cột đầu: tên đơn vị, loại và số ô lưới của cả nhánh.
  const unitSpan = (span) => (row) => (row.kind === 'unit' ? { colSpan: span } : {});
  const columns = [
    {
      title: 'Mã',
      key: 'gridCode',
      onCell: unitSpan(3),
      render: (_, row) => {
        if (row.kind === 'unit') {
          return (
            <span style={{ fontWeight: 500 }}>
              {row.node.name}{' '}
              <Typography.Text type="secondary" style={{ fontWeight: 400 }}>
                {row.node.typeName} · {row.gridCount} ô lưới
              </Typography.Text>
            </span>
          );
        }
        const p = row.feature.properties;
        return (
          <Button
            type="link"
            size="small"
            style={{ fontFamily: 'monospace', padding: 0 }}
            onClick={() => {
              setSelectedId(p.gridId);
              setMode({ kind: 'view' });
            }}
          >
            <HighlightText text={p.gridCode} query={query} />
          </Button>
        );
      },
    },
    {
      title: 'Tên',
      key: 'name',
      onCell: unitSpan(0),
      render: (_, row) => (row.kind === 'grid' ? <HighlightText text={row.feature.properties.name} query={query} /> : null),
    },
    {
      title: 'Khu vực',
      key: 'area',
      onCell: unitSpan(0),
      render: (_, row) => {
        if (row.kind !== 'grid') return null;
        const p = row.feature.properties;
        return (
          <>
            {p.hamletName && <HighlightText text={p.hamletName} query={query} />}
            {p.hamletName && p.communeName && ', '}
            {p.communeName && <HighlightText text={p.communeName} query={query} />}
          </>
        );
      },
    },
    {
      title: 'Trạng thái',
      key: 'status',
      render: (_, row) => {
        if (row.kind !== 'grid') return null;
        const c = gridColor(row.feature.properties.statusColor);
        return (
          <span>
            <span style={{ color: c.fill }}>●</span> {c.label}
          </span>
        );
      },
    },
    {
      title: 'Mở / quá hạn',
      key: 'counts',
      render: (_, row) =>
        row.kind === 'unit'
          ? `${row.openCount} / ${row.overdueCount}`
          : `${row.feature.properties.openIncidentCount} / ${row.feature.properties.overdueIncidentCount}`,
    },
    {
      title: '',
      key: 'actions',
      align: 'right',
      render: (_, row) => {
        if (row.kind !== 'grid') return null;
        const f = row.feature;
        return (
          <Space size="small">
            {canManage && (
              <Button
                type="link"
                size="small"
                onClick={() => {
                  setSelectedId(f.properties.gridId);
                  setMode({ kind: 'edit', id: f.properties.gridId });
                }}
              >
                Sửa
              </Button>
            )}
            {canAssign && (
              <Button
                type="link"
                size="small"
                onClick={() => {
                  setSelectedId(f.properties.gridId);
                  setMode({ kind: 'officers', id: f.properties.gridId });
                }}
              >
                Cán bộ
              </Button>
            )}
          </Space>
        );
      },
    },
  ];
  return (
    <Flex vertical gap={20} component="section" aria-labelledby="grids-title">
      <Flex wrap gap={8} align="center" justify="space-between">
        <Typography.Title level={3} id="grids-title" style={{ margin: 0 }}>
          Ô lưới
        </Typography.Title>
        {canManage && (
          <Space wrap>
            <Button onClick={() => setMode({ kind: 'create' })}>Thêm ô lưới</Button>
            <Button onClick={() => setMode({ kind: 'import' })}>Nhập GeoJSON</Button>
          </Space>
        )}
      </Flex>

      {notice && <Alert type="success" showIcon role="status" title={notice} />}
      {warnings.length > 0 && (
        <Alert
          type="warning"
          showIcon
          role="alert"
          title={
            <ul style={{ margin: 0, paddingLeft: 16 }}>
              {warnings.map((w, i) => (
                <li key={i}>{w}</li>
              ))}
            </ul>
          }
        />
      )}
      {grids.isError && <Alert type="error" showIcon role="alert" title={errorMessage(grids.error)} />}

      <TreeSearchBar
        query={query}
        onQueryChange={(v) => {
          setQuery(v);
          collapse.resetSearch();
        }}
        placeholder="Tìm theo phường/xã, khu phố/thôn, mã hoặc tên ô lưới…"
        label="Tìm ô lưới"
        summary={
          searching ? `Tìm thấy ${features.length}/${allFeatures.length} ô lưới` : `${allFeatures.length} ô lưới trong phạm vi`
        }
        onExpandAll={collapse.expandAll}
        onCollapseAll={() => collapse.collapseAll(allGroupKeys)}
      />

      <BaseMap label="Bản đồ ô lưới quản trị" height="380px" showLocateButton={false}>
        {features.length > 0 && (
          <GeoJSON
            key={features.map((f) => f.properties.gridId).join(',')}
            data={{ type: 'FeatureCollection', features }}
            style={(f) => {
              const c = gridColor(f.properties.statusColor);
              return { color: c.fill, weight: selectedId === f.properties.gridId ? 4 : 2, fillOpacity: 0.2 };
            }}
            onEachFeature={(feature, layer) => {
              const p = feature.properties;
              layer.bindTooltip(`${p.gridCode} · ${p.name}`);
              // Khi đang vẽ hoặc đang mở form, bấm vào ô lưới khác không được đóng form.
              layer.on('click', () => {
                if (modeRef.current !== 'view') return;
                selectGridRef.current(p.gridId);
              });
            }}
          />
        )}
        {searching && features.length > 0 && <FitToFeatures features={features} />}
        {(mode.kind === 'create' || mode.kind === 'edit') && (
          <GridDrawTool editGeometry={mode.kind === 'edit' ? (detailGrid?.boundary ?? null) : null} onGeometry={setDrawn} />
        )}
      </BaseMap>

      <Table
        aria-label="Danh sách ô lưới trong phạm vi"
        size="small"
        columns={columns}
        dataSource={groupRows}
        rowKey="key"
        pagination={false}
        scroll={{ x: 'max-content' }}
        loading={grids.isLoading || tree.isLoading}
        locale={{ emptyText: searching ? `Không có ô lưới nào khớp “${query.trim()}”.` : 'Chưa có ô lưới trong phạm vi.' }}
        expandable={{
          expandedRowKeys,
          expandRowByClick: true,
          onExpand: (_, row) => collapse.toggle(row.key),
        }}
        onRow={(row) => ({
          style:
            row.kind === 'unit'
              ? { background: 'rgba(0, 0, 0, 0.02)', cursor: 'pointer' }
              : selectedId === row.feature.properties.gridId
                ? { background: 'rgba(135, 223, 44, 0.15)' }
                : undefined,
        })}
      />

      {mode.kind === 'create' && <GridForm key="create" onCancel={() => setMode({ kind: 'view' })} onSaved={done} drawn={drawn} />}
      {mode.kind === 'edit' && detailGrid && (
        <GridForm key={`edit-${mode.id}`} grid={detailGrid} drawn={drawn} onCancel={() => setMode({ kind: 'view' })} onSaved={done} />
      )}
      {mode.kind === 'officers' && detailGrid && canAssign && (
        <OfficersForm gridId={mode.id} current={detailGrid.officers} onCancel={() => setMode({ kind: 'view' })} onSaved={done} />
      )}
      {mode.kind === 'import' && (
        <ImportPanel
          title="Nhập GeoJSON ô lưới"
          importFn={(payload, dryRun) => postApiGridsImport(payload, { dryRun })}
          onCancel={() => setMode({ kind: 'view' })}
          onDone={(msg) => done(msg)}
        />
      )}
    </Flex>
  );
}
/** Khi tìm kiếm, căn bản đồ vừa với các ô lưới khớp để thấy ngay khu phố/phường vừa tìm. */
function FitToFeatures({ features }) {
  const map = useMap();
  useEffect(() => {
    const bounds = L.geoJSON({ type: 'FeatureCollection', features }).getBounds();
    if (bounds.isValid()) map.fitBounds(bounds, { padding: [16, 16], maxZoom: 16 });
  }, [features, map]);
  return null;
}
function GridForm({ grid, drawn, onCancel, onSaved }) {
  const { modal } = App.useApp();
  const tree = useGetApiAdminUnitsTree({});
  const units = useMemo(() => flattenTree(tree.data?.data ?? []), [tree.data]);
  const drawnGeometry = drawn;
  const [gridCode, setGridCode] = useState(grid?.gridCode ?? '');
  const [name, setName] = useState(grid?.name ?? '');
  const [adminUnitId, setAdminUnitId] = useState(grid ? String(grid.adminUnitId) : '');
  // Khi sửa, điền sẵn ranh giới hiện tại dạng WKT để người dùng xem và sao chép.
  const [wkt, setWkt] = useState(() => geometryToWkt(grid?.boundary) ?? '');
  const [copied, setCopied] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  // Hình vẽ trên bản đồ được đưa vào ô WKT ngay, để người dùng thấy tọa độ từng đỉnh và có thể sửa.
  useEffect(() => {
    const text = geometryToWkt(drawn);
    if (text) setWkt(text);
  }, [drawn]);
  // Ô lưới thuộc khu phố/thôn: đơn vị lá của cây. Hiển thị đủ tên phường/tỉnh để không nhầm "Khu phố 1".
  const leafUnits = units.filter((u) => (u.node.children ?? []).length === 0);
  const submit = async () => {
    setError(null);
    const geometry = drawnGeometry ?? grid?.boundary ?? null;
    let boundaryPart = {};
    const typed = wkt.trim();
    if (typed) {
      const problem = checkBoundaryText(typed);
      if (problem) {
        setError(problem);
        return;
      }
      // Người dùng có thể dán GeoJSON vào ô này: gửi đúng trường tương ứng.
      boundaryPart = typed.startsWith('{') ? { boundary: JSON.parse(typed) } : { wkt: typed };
    } else if (geometry) {
      boundaryPart = { boundary: geometry };
    }
    if (!gridCode.trim() || !name.trim() || !adminUnitId) {
      setError('Hãy nhập mã ô lưới, tên ô lưới và chọn khu phố/thôn chứa ô lưới.');
      return;
    }
    if (!/^[A-Za-z0-9-]{3,50}$/.test(gridCode.trim())) {
      setError('Mã ô lưới chỉ gồm chữ không dấu, số và dấu gạch ngang, ví dụ DNI-BH-KP03-G01.');
      return;
    }
    if (!grid && !boundaryPart.boundary && !boundaryPart.wkt) {
      setError('Hãy vẽ, dán WKT hoặc tải GeoJSON cho ranh giới ô lưới.');
      return;
    }
    setBusy(true);
    try {
      const body = {
        gridCode: gridCode.trim(),
        name: name.trim(),
        adminUnitId: Number(adminUnitId),
        isActive: grid ? grid.isActive : true,
        ...boundaryPart,
      };
      const res = grid ? await putApiGridsId(Number(grid.id), body) : await postApiGrids(body);
      const warnings = res.data.warnings ?? [];
      onSaved(grid ? 'Đã cập nhật ô lưới.' : 'Đã thêm ô lưới.', warnings);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  const deactivate = async () => {
    try {
      await putApiGridsId(Number(grid.id), {
        gridCode: grid.gridCode,
        name: grid.name,
        adminUnitId: Number(grid.adminUnitId),
        isActive: false,
        boundary: grid.boundary,
      });
      onSaved('Đã ngừng sử dụng ô lưới.');
    } catch (err) {
      setError(errorMessage(err));
    }
  };
  return (
    <Card title={grid ? 'Sửa ô lưới' : 'Thêm ô lưới'} style={{ maxWidth: 672 }}>
      <Form
        layout="vertical"
        aria-label={grid ? 'Sửa ô lưới' : 'Thêm ô lưới'}
        onFinish={() => {
          void submit();
        }}
      >
        <Form.Item
          label="Mã ô lưới"
          extra={
            <FieldHint>
              Mã khu vực, mã khu phố, rồi G01. Ví dụ: <Example>DNI-BH-KP03-G01</Example> (Biên Hòa, khu phố 3).
            </FieldHint>
          }
        >
          <Input
            style={{ fontFamily: 'monospace' }}
            value={gridCode}
            placeholder="DNI-BH-KP03-G01"
            onChange={(e) => setGridCode(e.target.value)}
          />
        </Form.Item>
        <Form.Item
          label="Tên ô lưới"
          extra={
            <FieldHint>
              Ví dụ: <Example>Khu phố 3, Phường Biên Hòa</Example>
            </FieldHint>
          }
        >
          <Input value={name} placeholder="Khu phố 3, Phường Biên Hòa" onChange={(e) => setName(e.target.value)} />
        </Form.Item>
        <Form.Item
          label="Khu phố/thôn chứa ô lưới"
          extra={
            <FieldHint>
              Chọn đúng khu phố/thôn (cấp thấp nhất). Mỗi khu phố có thể có nhiều ô lưới, mỗi ô lưới thuộc đúng một khu phố.
              {leafUnits.length === 0 && ' Chưa có khu phố nào: hãy tạo đơn vị cấp khu phố ở mục Đơn vị hành chính trước.'}
            </FieldHint>
          }
        >
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            value={adminUnitId === '' ? undefined : Number(adminUnitId)}
            placeholder="Chọn khu phố/thôn"
            allowClear
            onChange={(v) => setAdminUnitId(v === undefined || v === null ? '' : String(v))}
            options={leafUnits.map((u) => ({ value: Number(u.node.id), label: unitFullName(units, u.node.id) }))}
          />
        </Form.Item>
        <Alert
          type="info"
          style={{ marginBottom: 16 }}
          title="Ranh giới ô lưới"
          description={
            <div>
              <ol style={{ paddingLeft: 20, margin: '4px 0' }}>
                <li>
                  <strong>Vẽ trên bản đồ:</strong> phóng to đến khu phố đã chọn, bấm nút hình đa giác ở góc trên trái bản đồ, bấm từng đỉnh,
                  rồi bấm lại điểm đầu để khép kín. Trạng thái:{' '}
                  {drawnGeometry ? 'đã vẽ xong.' : grid ? 'giữ ranh giới cũ, có thể sửa đỉnh trên bản đồ.' : 'chưa vẽ.'}
                </li>
                <li>
                  <strong>Hoặc dán WKT / GeoJSON</strong> vào ô bên dưới. Lưu ý thứ tự: <em>kinh độ trước, vĩ độ sau</em>.
                </li>
              </ol>
              <FieldHint>
                WKT: <Example>{WKT_EXAMPLE}</Example>
              </FieldHint>
              <FieldHint>
                GeoJSON: <Example>{GEOJSON_EXAMPLE}</Example>
              </FieldHint>
            </div>
          }
        />
        <Form.Item
          label="Ranh giới dạng WKT hoặc GeoJSON (tùy chọn)"
          extra={
            <FieldHint>
              Nếu đã vẽ trên bản đồ thì không cần nhập ở đây. Hệ thống kiểm tra tọa độ có nằm trong tỉnh Đồng Nai trước khi lưu.
            </FieldHint>
          }
        >
          <Input.TextArea
            rows={3}
            style={{ fontFamily: 'monospace', fontSize: 12 }}
            value={wkt}
            onChange={(e) => setWkt(e.target.value)}
            placeholder={WKT_EXAMPLE}
          />
          <Button
            size="small"
            style={{ marginTop: 8 }}
            disabled={!wkt.trim()}
            onClick={async () => {
              await navigator.clipboard.writeText(wkt.trim());
              setCopied(true);
              window.setTimeout(() => setCopied(false), 2000);
            }}
          >
            {copied ? 'Đã sao chép' : 'Sao chép ranh giới'}
          </Button>
        </Form.Item>
        {error && <Alert type="error" showIcon role="alert" title={error} style={{ marginBottom: 16 }} />}
        <Flex wrap gap={8}>
          <Button type="primary" htmlType="submit" loading={busy} disabled={busy}>
            {busy ? 'Đang lưu…' : 'Lưu ô lưới'}
          </Button>
          <Button onClick={onCancel}>Hủy</Button>
          {grid?.isActive && (
            <Button
              danger
              style={{ marginLeft: 'auto' }}
              onClick={() =>
                modal.confirm({
                  title: 'Ngừng sử dụng ô lưới này? Ô lưới sẽ không nhận sự cố mới.',
                  okText: 'Ngừng sử dụng',
                  okButtonProps: { danger: true },
                  cancelText: 'Hủy',
                  onOk: deactivate,
                })
              }
            >
              Ngừng sử dụng
            </Button>
          )}
        </Flex>
      </Form>
    </Card>
  );
}
function OfficersForm({ gridId, current, onCancel, onSaved }) {
  const officers = useGetApiOfficers({ gridId });
  const [picked, setPicked] = useState(current.map((c) => Number(c.userId)));
  const [primary, setPrimary] = useState(() => {
    const p = current.find((c) => c.isPrimary);
    return p ? Number(p.userId) : null;
  });
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const save = async () => {
    if (picked.length > 0 && (primary === null || !picked.includes(primary))) {
      setError('Hãy đánh dấu một cán bộ chính trong danh sách đã chọn.');
      return;
    }
    setBusy(true);
    try {
      await putApiGridsIdOfficers(gridId, { userIds: picked, primaryUserId: primary ?? picked[0] });
      onSaved('Đã cập nhật phân công cán bộ.');
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  const candidates = officers.data?.data ?? [];
  return (
    <Card title="Phân công cán bộ" style={{ maxWidth: 576 }}>
      <Flex vertical gap={8}>
        {candidates.map((o) => (
          <Flex key={o.id} wrap align="center" gap={12}>
            <Checkbox
              checked={picked.includes(Number(o.id))}
              onChange={(e) => setPicked(e.target.checked ? [...picked, Number(o.id)] : picked.filter((x) => x !== Number(o.id)))}
            >
              {o.fullName}{' '}
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                · đang mở {o.openIncidentCount}
              </Typography.Text>
            </Checkbox>
            <Radio
              name="primary"
              checked={primary === Number(o.id)}
              disabled={!picked.includes(Number(o.id))}
              onChange={() => setPrimary(Number(o.id))}
            >
              chính
            </Radio>
          </Flex>
        ))}
        {officers.isSuccess && candidates.length === 0 && <Typography.Text type="secondary">Chưa có cán bộ nào.</Typography.Text>}
        {error && <Alert type="error" showIcon role="alert" title={error} />}
        <Flex wrap gap={8}>
          <Button type="primary" loading={busy} disabled={busy} onClick={() => void save()}>
            Lưu phân công
          </Button>
          <Button onClick={onCancel}>Hủy</Button>
        </Flex>
      </Flex>
    </Card>
  );
}
