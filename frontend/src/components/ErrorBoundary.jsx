import { Component } from 'react';
import { Button, Result } from 'antd';
import { vi } from '@/locales/vi';

/**
 * Bắt lỗi không lường trước ngoài router (FE-05).
 * Dùng Result của antd nhưng không phụ thuộc ConfigProvider, vì boundary này bọc cả lớp providers.
 */
export class ErrorBoundary extends Component {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error, info) {
    console.error('Lỗi không lường trước', error, info.componentStack);
  }

  render() {
    if (!this.state.hasError) return this.props.children;
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
        style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}
      />
    );
  }
}
