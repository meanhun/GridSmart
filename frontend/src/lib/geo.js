// Lấy vị trí GPS độ chính xác cao (FE-08 bước 2).
export function getCurrentFix(timeoutMs = 10_000) {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new Error('GEOLOCATION_UNSUPPORTED'));
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) =>
        resolve({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
          accuracy: pos.coords.accuracy,
        }),
      (err) => reject(err),
      { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 0 },
    );
  });
}
/** Kiểm tra tọa độ có nằm trong khoảng hợp lệ của WGS84 không. */
export function isValidLatLon(latitude, longitude) {
  return (
    Number.isFinite(latitude) && Number.isFinite(longitude) && latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180
  );
}
