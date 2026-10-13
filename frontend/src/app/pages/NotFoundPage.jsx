import { Link } from 'react-router';
import { Button, Result } from 'antd';
import { vi } from '@/locales/vi';

export function NotFoundPage() {
  return (
    <Result
      status="404"
      title={vi.notFound.title}
      subTitle={vi.notFound.body}
      extra={
        <Link to="/">
          <Button type="primary">{vi.common.back}</Button>
        </Link>
      }
    />
  );
}
