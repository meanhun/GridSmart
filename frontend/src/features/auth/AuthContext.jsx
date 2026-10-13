import { createContext, useCallback, useEffect, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getApiAuthMe, postApiAuthLogin, postApiAuthLogout } from '@/api/generated/endpoints/auth/auth';
import { refreshSession } from '@/api/mutator/customInstance';
import { session } from '@/api/session';
/** Làm mới access token trước khi hết hạn bao lâu (FE-03: trước 1 phút). */
const REFRESH_LEAD_MS = 60_000;
export const AuthContext = createContext(null);
const EMPTY_CONTEXT = { permissions: [], scopes: [] };
export function AuthProvider({ children }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState('loading');
  const [user, setUser] = useState(null);
  const loadUser = useCallback(async () => {
    const res = await getApiAuthMe();
    setUser(res.data);
    setStatus('authenticated');
  }, []);
  // Khôi phục phiên khi tải trang: refresh cookie, rồi lấy thông tin người dùng.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const ok = await refreshSession();
      if (cancelled) return;
      if (!ok) {
        setUser(null);
        setStatus('anonymous');
        return;
      }
      try {
        await loadUser();
      } catch {
        if (!cancelled) {
          session.clear();
          setStatus('anonymous');
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [loadUser]);
  // Đồng bộ với bộ nhớ phiên: token mất (hết hạn, đăng xuất ở tab khác) thì coi là chưa đăng nhập.
  useEffect(() => {
    return session.subscribe(() => {
      if (session.getToken() === null) {
        setUser(null);
        setStatus((prev) => (prev === 'loading' ? prev : 'anonymous'));
      }
    });
  }, []);
  // Tự refresh trước khi access token hết hạn.
  useEffect(() => {
    if (status !== 'authenticated') return;
    const expiresAt = session.getExpiresAtMs();
    if (expiresAt === null) return;
    const delay = Math.max(expiresAt - Date.now() - REFRESH_LEAD_MS, 0);
    const timer = window.setTimeout(() => {
      void refreshSession();
    }, delay);
    return () => window.clearTimeout(timer);
  }, [status, user]);
  const login = useCallback(
    async (username, password) => {
      const res = await postApiAuthLogin({ username, password });
      session.setToken(res.data.accessToken, res.data.expiresAt);
      setUser(res.data.user);
      setStatus('authenticated');
      queryClient.clear();
      return res.data.user;
    },
    [queryClient],
  );
  const logout = useCallback(async () => {
    try {
      await postApiAuthLogout();
    } finally {
      session.clear();
      queryClient.clear();
      setUser(null);
      setStatus('anonymous');
    }
  }, [queryClient]);
  const reloadUser = useCallback(async () => {
    await loadUser();
  }, [loadUser]);
  const value = useMemo(
    () => ({
      status,
      user,
      permissionContext: user ? { permissions: user.permissions, scopes: user.scopes } : EMPTY_CONTEXT,
      login,
      logout,
      reloadUser,
    }),
    [status, user, login, logout, reloadUser],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
