import { useEffect, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { Alert, Button } from 'antd';
import { MapContainer, TileLayer, useMap, ZoomControl } from 'react-leaflet';
import { getCurrentFix } from '@/lib/geo';
/** Tâm mặc định: Biên Hòa (FE-07). */
export const BIEN_HOA_CENTER = [10.9447, 106.8243];
const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
/** Bản đồ nền OpenStreetMap dùng chung (FE-07). */
export function BaseMap({ center = BIEN_HOA_CENTER, zoom = 14, height = '320px', showLocateButton = true, children, label }) {
  return (
    <div
      style={{
        position: 'relative',
        overflow: 'hidden',
        height,
        borderRadius: 8,
        border: '1px solid var(--ant-color-border, #B4B4B2)',
      }}
      role="region"
      aria-label={label}
    >
      <MapContainer center={center} zoom={zoom} scrollWheelZoom zoomControl={false} style={{ height: '100%', width: '100%' }}>
        <TileLayer url="https://tile.openstreetmap.org/{z}/{x}/{y}.png" attribution={OSM_ATTRIBUTION} />
        {/* Nút phóng to đặt góc dưới trái để không đè lên các bảng nổi phía trên. */}
        <ZoomControl position="bottomleft" />
        <AutoResize />
        {children}
        {showLocateButton && <LocateButton />}
      </MapContainer>
    </div>
  );
}
/** Vẽ lại bản đồ khi khung chứa đổi kích thước (thu gọn/mở sidebar, xoay màn hình), tránh vùng xám thiếu ô bản đồ. */
function AutoResize() {
  const map = useMap();
  useEffect(() => {
    if (typeof ResizeObserver === 'undefined') return undefined;
    const observer = new ResizeObserver(() => map.invalidateSize({ pan: false }));
    observer.observe(map.getContainer());
    return () => observer.disconnect();
  }, [map]);
  return null;
}
function LocateButton() {
  const map = useMap();
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  const locate = async () => {
    setBusy(true);
    setFailed(false);
    try {
      const fix = await getCurrentFix();
      map.flyTo([fix.latitude, fix.longitude], Math.max(map.getZoom(), 16));
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="leaflet-bottom leaflet-right">
      <div className="leaflet-control" style={{ margin: 12 }}>
        <Button onClick={() => void locate()} disabled={busy} loading={busy}>
          {busy ? 'Đang định vị…' : 'Vị trí của tôi'}
        </Button>
        {failed && <Alert type="error" title="Không lấy được vị trí." style={{ marginTop: 4, padding: '2px 8px' }} />}
      </div>
    </div>
  );
}
/** Icon đơn giản bằng CSS, tránh phụ thuộc file ảnh mặc định của Leaflet khi bundle. */
export const pinIcon = L.divIcon({
  className: '',
  html: '<span style="display:block;width:20px;height:20px;border-radius:50%;background:#21A691;border:3px solid #FFFFFF;box-shadow:0 0 0 1px #27403E"></span>',
  iconSize: [20, 20],
  iconAnchor: [10, 10],
});
