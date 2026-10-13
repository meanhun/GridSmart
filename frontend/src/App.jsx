import { RouterProvider } from 'react-router';
import { AppProviders } from './app/providers';
import { router } from './app/router';
import { ErrorBoundary } from './components/ErrorBoundary';
import { OfflineBanner } from './components/OfflineBanner';
export default function App() {
  return (
    <ErrorBoundary>
      <AppProviders>
        <OfflineBanner />
        <RouterProvider router={router} />
      </AppProviders>
    </ErrorBoundary>
  );
}
