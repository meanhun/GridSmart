import { describe, expect, it } from 'vitest';
import { defaultLandingPath, hasPermission } from './permissions';
const leaderTinh = {
  permissions: ['incident.view', 'dashboard.view'],
  scopes: [
    { permission: 'incident.view', unitPath: null },
    { permission: 'dashboard.view', unitPath: null },
  ],
};
const leaderPhuong = {
  permissions: ['incident.view', 'incident.accept'],
  scopes: [
    { permission: 'incident.view', unitPath: '/1/2/' },
    { permission: 'incident.accept', unitPath: '/1/2/' },
  ],
};
const citizen = { permissions: ['incident.create'], scopes: [] };
describe('hasPermission', () => {
  it('không có quyền thì luôn false', () => {
    expect(hasPermission(citizen, 'incident.view')).toBe(false);
    expect(hasPermission(citizen, 'incident.view', '/1/2/')).toBe(false);
  });
  it('kiểm tra quyền không kèm đơn vị chỉ cần có mã quyền', () => {
    expect(hasPermission(leaderPhuong, 'incident.view')).toBe(true);
  });
  it('phạm vi toàn hệ thống (null) áp dụng cho mọi đơn vị', () => {
    expect(hasPermission(leaderTinh, 'incident.view', '/1/')).toBe(true);
    expect(hasPermission(leaderTinh, 'incident.view', '/1/2/5/')).toBe(true);
  });
  it('phạm vi đơn vị áp dụng cho chính đơn vị đó và con của nó', () => {
    expect(hasPermission(leaderPhuong, 'incident.view', '/1/2/')).toBe(true);
    expect(hasPermission(leaderPhuong, 'incident.view', '/1/2/5/')).toBe(true);
  });
  it('không áp dụng cho đơn vị cha', () => {
    expect(hasPermission(leaderPhuong, 'incident.view', '/1/')).toBe(false);
  });
  it('không áp dụng cho đơn vị anh em, kể cả có chung tiền tố số', () => {
    expect(hasPermission(leaderPhuong, 'incident.view', '/1/3/')).toBe(false);
    expect(hasPermission(leaderPhuong, 'incident.view', '/1/20/')).toBe(false);
  });
  it('đơn vị chưa xác định (null) không khớp phạm vi đơn vị', () => {
    expect(hasPermission(leaderPhuong, 'incident.view', null)).toBe(false);
  });
  it('quyền khác nhau có phạm vi độc lập', () => {
    expect(hasPermission(leaderPhuong, 'incident.accept', '/1/2/')).toBe(true);
    expect(hasPermission(leaderPhuong, 'incident.accept', '/1/3/')).toBe(false);
  });
});
describe('defaultLandingPath', () => {
  it('có dashboard.view thì về trung tâm điều hành', () => {
    expect(defaultLandingPath(leaderTinh)).toBe('/command');
  });
  it('có incident.accept thì về cổng cán bộ', () => {
    expect(defaultLandingPath(leaderPhuong)).toBe('/officer');
  });
  it('còn lại về cổng công dân', () => {
    expect(defaultLandingPath(citizen)).toBe('/');
  });
});
