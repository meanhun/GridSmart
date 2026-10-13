// Kiểm tra quyền phía giao diện (FE-04). Quy tắc khớp với backend:
// - Phạm vi toàn hệ thống (unitPath = null) áp dụng cho mọi đơn vị.
// - Phạm vi đơn vị áp dụng cho đơn vị có unitPath bắt đầu bằng đường dẫn phạm vi.
//   Đường dẫn có dấu "/" cuối (ví dụ "/1/2/") nên "/1/20/" không thuộc phạm vi "/1/2/".
/**
 * Trả true nếu người dùng có quyền `code`.
 * Khi truyền `unitPath`, chỉ trả true nếu quyền đó áp dụng được cho đơn vị này.
 */
export function hasPermission(ctx, code, unitPath) {
  if (!ctx.permissions.includes(code)) return false;
  if (unitPath === undefined) return true;
  return ctx.scopes.some((scope) => {
    if (scope.permission !== code) return false;
    if (scope.unitPath === null || scope.unitPath === undefined) return true;
    if (unitPath === null) return false;
    return unitPath.startsWith(scope.unitPath);
  });
}
/** Trang đích sau đăng nhập theo quyền (FE-04). */
export function defaultLandingPath(ctx) {
  if (hasPermission(ctx, 'dashboard.view')) return '/command';
  if (hasPermission(ctx, 'incident.accept')) return '/officer';
  return '/';
}
