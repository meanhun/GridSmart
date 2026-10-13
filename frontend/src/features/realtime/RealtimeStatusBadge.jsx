import { Badge } from 'antd';
import { useRealtime } from './RealtimeProvider';
import { vi } from '@/locales/vi';
const TONE = {
  connected: { status: 'success', label: vi.realtime.connected },
  connecting: { status: 'warning', label: vi.realtime.connecting },
  reconnecting: { status: 'warning', label: vi.realtime.reconnecting },
  disconnected: { status: 'error', label: vi.realtime.disconnected },
};
/** Biểu tượng trạng thái kết nối: xanh, vàng, đỏ (FE-06). Luôn có chữ kèm theo. */
export function RealtimeStatusBadge() {
  const { state } = useRealtime();
  const tone = TONE[state];
  return (
    <span role="status">
      <Badge status={tone.status} text={tone.label} />
    </span>
  );
}
