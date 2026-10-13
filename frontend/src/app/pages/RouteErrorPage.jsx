import { isRouteErrorResponse, useRouteError } from 'react-router';
import { Button, Result } from 'antd';
import { vi } from '@/locales/vi';

/** ErrorBoundary cho lỗi không lường trước (FE-05). Không hiện chi tiết kỹ thuật. */
export function RouteErrorPage() {
  const error = useRouteError();
  if (isRouteErrorResponse(error) && error.status === 404) {
    return <Result status="404" title={vi.notFound.title} />;
  }
  return (
    <Result
      status="error"
      title={vi.errorBoundary.title}
      subTitle={vi.errorBoundary.body}
      extra={
        <Button type="primary" onClick={() => window.location.reload()}>
          {vi.errorBoundary.reload}
        </Button>
      }
    />
  );
}
