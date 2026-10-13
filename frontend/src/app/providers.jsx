import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { AuthProvider } from '@/features/auth/AuthContext';
import { RealtimeProvider } from '@/features/realtime/RealtimeProvider';
import { antdTheme } from './theme';

dayjs.locale('vi');

export function AppProviders({ children }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 15_000 },
        },
      }),
  );
  return (
    <ConfigProvider locale={viVN} theme={antdTheme}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <RealtimeProvider>{children}</RealtimeProvider>
          </AuthProvider>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  );
}
