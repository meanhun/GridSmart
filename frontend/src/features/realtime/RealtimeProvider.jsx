import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { session } from '@/api/session';
/** Tên sự kiện theo mục 8.2 của tài liệu backend. */
export const REALTIME_EVENTS = [
  'incident.created',
  'incident.assigned',
  'incident.updated',
  'incident.escalated',
  'incident.overdue',
  'incident.resolved',
  'grid.status-changed',
  'notification.created',
];
/** Độ trễ kết nối lại tăng dần (FE-06). */
const RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 30000];
const RealtimeContext = createContext(null);
/** Cập nhật cache TanStack Query theo sự kiện thay vì tải lại trang (FE-06). */
export function invalidateForEvent(queryClient, name) {
  switch (name) {
    case 'incident.created':
    case 'incident.assigned':
    case 'incident.updated':
    case 'incident.escalated':
    case 'incident.overdue':
    case 'incident.resolved':
      void queryClient.invalidateQueries({ queryKey: ['/api/incidents'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/officer/incidents'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/dashboard'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/public/track'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/me/incidents'] });
      break;
    case 'grid.status-changed':
      void queryClient.invalidateQueries({ queryKey: ['/api/dashboard/grid-status'] });
      void queryClient.invalidateQueries({ queryKey: ['/api/grids'] });
      break;
    case 'notification.created':
      void queryClient.invalidateQueries({ queryKey: ['/api/notifications'] });
      break;
  }
}
export function RealtimeProvider({ children }) {
  const queryClient = useQueryClient();
  const [state, setState] = useState('connecting');
  const connectionRef = useRef(null);
  const listenersRef = useRef(new Map());
  const dispatch = useCallback(
    (name, payload) => {
      invalidateForEvent(queryClient, name);
      listenersRef.current.get(name)?.forEach((handler) => handler(payload));
    },
    [queryClient],
  );
  // Một kết nối duy nhất. Khi đăng nhập hoặc đăng xuất (token đổi có/không) thì kết nối lại để gắn đúng nhóm.
  useEffect(() => {
    let disposed = false;
    let lastHadToken = null;
    const build = () => {
      const connection = new HubConnectionBuilder()
        .withUrl('/hubs/events', { accessTokenFactory: () => session.getToken() ?? '' })
        .withAutomaticReconnect(RECONNECT_DELAYS_MS)
        .configureLogging(LogLevel.Warning)
        .build();
      for (const name of REALTIME_EVENTS) {
        connection.on(name, (payload) => dispatch(name, payload));
      }
      connection.onreconnecting(() => setState('reconnecting'));
      connection.onreconnected(() => setState('connected'));
      connection.onclose(() => setState('disconnected'));
      return connection;
    };
    // Mọi thao tác start/stop đi qua một hàng đợi để không bao giờ dừng kết nối đang khởi động.
    let queue = Promise.resolve();
    const enqueue = (task) => {
      queue = queue.then(task, task);
      return queue;
    };
    const startInternal = async () => {
      if (disposed) return;
      const connection = build();
      connectionRef.current = connection;
      setState('connecting');
      try {
        await connection.start();
        if (!disposed) setState('connected');
      } catch {
        if (!disposed) setState('disconnected');
      }
    };
    const stopInternal = async () => {
      const current = connectionRef.current;
      connectionRef.current = null;
      await current?.stop();
    };
    lastHadToken = session.getToken() !== null;
    // Trì hoãn một tick để StrictMode (mount, hủy, mount lại) không xếp thêm kết nối thừa.
    const startTimer = window.setTimeout(() => void enqueue(startInternal), 0);
    // Đăng nhập hoặc đăng xuất: kết nối lại để gắn đúng nhóm theo người dùng.
    const unsubscribe = session.subscribe(() => {
      const hasToken = session.getToken() !== null;
      if (hasToken === lastHadToken) return;
      lastHadToken = hasToken;
      void enqueue(async () => {
        await stopInternal();
        await startInternal();
      });
    });
    return () => {
      disposed = true;
      window.clearTimeout(startTimer);
      unsubscribe();
      void enqueue(stopInternal);
    };
  }, [dispatch]);
  const subscribe = useCallback((name, handler) => {
    const map = listenersRef.current;
    if (!map.has(name)) map.set(name, new Set());
    map.get(name).add(handler);
    return () => {
      map.get(name)?.delete(handler);
    };
  }, []);
  const joinTracking = useCallback(async (trackingCode, phone) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return false;
    return connection.invoke('JoinTracking', trackingCode, phone);
  }, []);
  const value = useMemo(() => ({ state, joinTracking, subscribe }), [state, joinTracking, subscribe]);
  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
export function useRealtime() {
  const ctx = useContext(RealtimeContext);
  if (!ctx) throw new Error('useRealtime phải được dùng bên trong RealtimeProvider.');
  return ctx;
}
/** Đăng ký nghe một sự kiện realtime. Tự hủy khi component unmount. */
export function useRealtimeEvent(name, handler) {
  const { subscribe } = useRealtime();
  const handlerRef = useRef(handler);
  handlerRef.current = handler;
  useEffect(() => subscribe(name, (payload) => handlerRef.current(payload)), [name, subscribe]);
}
