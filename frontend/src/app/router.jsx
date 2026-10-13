import { createBrowserRouter, Navigate } from 'react-router';
import { RequireAuth, RequirePermission } from '@/features/auth/guards';
import { ForbiddenPage } from '@/features/auth/ForbiddenPage';
import { LoginPage } from '@/features/auth/LoginPage';
import { ReportWizard } from '@/features/report/ReportWizard';
import { TrackPage } from '@/features/tracking/TrackPage';
import { CitizenLayout } from './layouts/CitizenLayout';
import { CommandLayout } from './layouts/CommandLayout';
import { OfficerLayout } from './layouts/OfficerLayout';
import { IncidentsListPage } from '@/features/command/IncidentsListPage';
import { IncidentManagePage } from '@/features/command/IncidentManagePage';
import { ReviewQueuePage } from '@/features/command/ReviewQueuePage';
import { DemoPage, DEMO_ENABLED } from '@/features/admin/DemoPage';
import { CategoriesPage } from '@/features/admin/config/CategoriesPage';
import { SlaPage } from '@/features/admin/config/SlaPage';
import { UsersPage, UserDetailPage } from '@/features/admin/users/UsersPages';
import { RolesPage } from '@/features/admin/users/RolesPage';
import { ChangePasswordPage, RegisterPage } from '@/features/auth/AccountPages';
import { OfficerHomePage } from '@/features/officer/OfficerHomePage';
import { OfficerIncidentPage } from '@/features/officer/OfficerIncidentPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { RouteErrorPage } from './pages/RouteErrorPage';
// Một ứng dụng, ba khu vực (quy ước 0.2): / công dân, /officer cán bộ, /command điều hành.
export const router = createBrowserRouter([
  {
    path: '/',
    element: <CitizenLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      // Trang chủ công dân mở thẳng vào màn hình chụp/chọn ảnh phản ánh (FE-08 bước 1).
      { index: true, element: <ReportWizard /> },
      { path: 'report/new', element: <ReportWizard /> },
      { path: 'track', element: <TrackPage /> },
      { path: 'track/:code', element: <TrackPage /> },
      { path: 'register', element: <RegisterPage /> },
    ],
  },
  {
    element: <RequireAuth />,
    errorElement: <RouteErrorPage />,
    children: [
      {
        element: <CitizenLayout />,
        children: [{ path: 'account/password', element: <ChangePasswordPage /> }],
      },
    ],
  },
  { path: '/login', element: <LoginPage />, errorElement: <RouteErrorPage /> },
  { path: '/403', element: <ForbiddenPage /> },
  {
    element: <RequireAuth />,
    errorElement: <RouteErrorPage />,
    children: [
      {
        path: '/officer',
        element: <RequirePermission permission="incident.accept" />,
        children: [
          {
            element: <OfficerLayout />,
            children: [
              { index: true, element: <OfficerHomePage /> },
              { path: 'incidents/:id', element: <OfficerIncidentPage /> },
            ],
          },
        ],
      },
      {
        path: '/command',
        element: <RequirePermission permission="dashboard.view" />,
        children: [
          {
            element: <CommandLayout />,
            children: [
              { index: true, lazy: async () => ({ Component: (await import('@/features/command/CommandMapPage')).CommandMapPage }) },
              { path: 'incidents', element: <IncidentsListPage /> },
              { path: 'incidents/:id', element: <IncidentManagePage /> },
              { path: 'review', element: <ReviewQueuePage /> },
              { path: 'reports', lazy: async () => ({ Component: (await import('@/features/reports/ReportsPage')).ReportsPage }) },
              {
                element: <RequirePermission permission="admin_unit.view" />,
                children: [
                  {
                    path: 'admin/units',
                    lazy: async () => ({ Component: (await import('@/features/admin/units/AdminUnitsPage')).AdminUnitsPage }),
                  },
                ],
              },
              {
                element: <RequirePermission permission="grid.view" />,
                children: [
                  {
                    path: 'admin/grids',
                    lazy: async () => ({ Component: (await import('@/features/admin/grids/AdminGridsPage')).AdminGridsPage }),
                  },
                ],
              },
              {
                element: <RequirePermission permission="category.manage" />,
                children: [{ path: 'admin/categories', element: <CategoriesPage /> }],
              },
              {
                element: <RequirePermission permission="sla.manage" />,
                children: [{ path: 'admin/sla', element: <SlaPage /> }],
              },
              {
                element: <RequirePermission permission="user.view" />,
                children: [
                  { path: 'admin/users', element: <UsersPage /> },
                  { path: 'admin/users/:id', element: <UserDetailPage /> },
                ],
              },
              {
                element: <RequirePermission permission="role.manage" />,
                children: [{ path: 'admin/roles', element: <RolesPage /> }],
              },
              ...(DEMO_ENABLED
                ? [{ element: <RequirePermission permission="role.manage" />, children: [{ path: 'demo', element: <DemoPage /> }] }]
                : []),
            ],
          },
        ],
      },
    ],
  },
  { path: '/home', element: <Navigate to="/" replace /> },
  { path: '*', element: <NotFoundPage /> },
]);
