import { useEffect, useState } from 'react';
import { Alert } from 'antd';
import { vi } from '@/locales/vi';

/** Thanh cảnh báo cố định khi mất mạng, tự ẩn khi có mạng lại (FE-05). */
export function OfflineBanner() {
  const [offline, setOffline] = useState(() => !navigator.onLine);

  useEffect(() => {
    const goOffline = () => setOffline(true);
    const goOnline = () => setOffline(false);
    window.addEventListener('offline', goOffline);
    window.addEventListener('online', goOnline);
    return () => {
      window.removeEventListener('offline', goOffline);
      window.removeEventListener('online', goOnline);
    };
  }, []);

  if (!offline) return null;
  return (
    <Alert
      banner
      role="alert"
      type="warning"
      message={vi.common.offline}
      style={{ position: 'fixed', insetInline: 0, top: 0, zIndex: 50, justifyContent: 'center' }}
    />
  );
}
