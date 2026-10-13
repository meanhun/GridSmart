import { Link } from 'react-router';
import { Result } from 'antd';
import { vi } from '@/locales/vi';
export function ForbiddenPage() {
  return (
    <Result status="403" title={vi.forbidden.title} subTitle={vi.forbidden.body} extra={<Link to="/">{vi.common.back}</Link>} />
  );
}
