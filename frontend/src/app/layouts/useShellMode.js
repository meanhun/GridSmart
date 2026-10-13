import { useEffect, useState } from 'react';

const QUERIES = { tablet: '(min-width: 768px)', desktop: '(min-width: 992px)' };

function readMode() {
  if (typeof window === 'undefined' || !window.matchMedia) return 'desktop';
  if (window.matchMedia(QUERIES.desktop).matches) return 'desktop';
  return window.matchMedia(QUERIES.tablet).matches ? 'tablet' : 'mobile';
}

/**
 * Loại màn hình cho khung ứng dụng: 'mobile' (< 768px), 'tablet' (768–991px), 'desktop' (≥ 992px).
 * Đọc matchMedia ngay lần render đầu, tránh nháy giao diện điện thoại trên máy tính.
 */
export function useShellMode() {
  const [mode, setMode] = useState(readMode);
  useEffect(() => {
    if (!window.matchMedia) return undefined;
    const lists = Object.values(QUERIES).map((q) => window.matchMedia(q));
    const onChange = () => setMode(readMode());
    lists.forEach((l) => l.addEventListener('change', onChange));
    return () => lists.forEach((l) => l.removeEventListener('change', onChange));
  }, []);
  return mode;
}
