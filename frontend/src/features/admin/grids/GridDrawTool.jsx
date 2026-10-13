import { useEffect } from 'react';
import L from 'leaflet';
import '@geoman-io/leaflet-geoman-free';
import '@geoman-io/leaflet-geoman-free/dist/leaflet-geoman.css';
import { useMap } from 'react-leaflet';
/**
 * Vẽ đa giác và sửa đỉnh bằng Leaflet-Geoman (FE-22).
 * Chỉ bật khi đang thêm hoặc sửa ô lưới.
 */
export function GridDrawTool({ editGeometry, onGeometry }) {
  const map = useMap();
  useEffect(() => {
    map.pm.addControls({
      position: 'topleft',
      drawPolygon: true,
      drawMarker: false,
      drawCircleMarker: false,
      drawPolyline: false,
      drawRectangle: false,
      drawCircle: false,
      drawText: false,
      cutPolygon: false,
      rotateMode: false,
      dragMode: false,
      editMode: false,
      removalMode: false,
    });
    // Hình vừa vẽ được giữ trên bản đồ để người dùng thấy, và có thể kéo đỉnh để chỉnh tiếp.
    // Vẽ hình mới thì hình cũ được thay thế, nên luôn chỉ có một hình nháp.
    let drawnLayer = null;
    const pushFrom = (layer) => {
      onGeometry(layer.toGeoJSON().geometry);
    };
    const onCreate = (e) => {
      const layer = e.layer;
      if (drawnLayer && drawnLayer !== layer) drawnLayer.remove();
      drawnLayer = layer;
      pushFrom(layer);
      layer.on('pm:edit', () => pushFrom(layer));
      layer.pm?.enable({ allowSelfIntersection: false });
    };
    map.on('pm:create', onCreate);
    return () => {
      map.off('pm:create', onCreate);
      drawnLayer?.remove();
      map.pm.removeControls();
    };
  }, [map, onGeometry]);
  useEffect(() => {
    if (!editGeometry) return;
    const group = L.geoJSON(editGeometry).addTo(map);
    const pushUpdate = () => {
      group.eachLayer((layer) => {
        onGeometry(layer.toGeoJSON().geometry);
      });
    };
    group.eachLayer((layer) => {
      const poly = layer;
      poly.pm?.enable({ allowSelfIntersection: false });
      poly.on('pm:edit', pushUpdate);
    });
    map.fitBounds(group.getBounds(), { maxZoom: 17 });
    return () => {
      group.eachLayer((layer) => layer.pm?.disable());
      group.remove();
    };
  }, [editGeometry, map, onGeometry]);
  return null;
}
