import { matchIndex } from '@/lib/admin-tree';
/** Tô sáng đoạn khớp từ khóa (không phân biệt dấu, hoa thường), ví dụ "tam hiep" trong "Phường Tam Hiệp". */
export function HighlightText({ text, query }) {
  const value = String(text ?? '').normalize('NFC');
  const start = matchIndex(value, query);
  if (start < 0) return value;
  const length = Array.from(query.trim().normalize('NFC')).length;
  const chars = Array.from(value);
  return (
    <>
      {chars.slice(0, start).join('')}
      <mark style={{ padding: 0, background: 'rgba(250, 204, 21, 0.45)', color: 'inherit' }}>{chars.slice(start, start + length).join('')}</mark>
      {chars.slice(start + length).join('')}
    </>
  );
}
