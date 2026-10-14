import { createBrowserRouter } from 'react-router-dom';
import { MainLayout } from './layouts/MainLayout';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { SignInPage } from '../features/auth/SignInPage';
import { DashboardPage } from '../features/dashboard/DashboardPage';

/**
 * Route gốc. Mỗi feature (contracts, partners...) sẽ thêm route con vào MainLayout.
 * Chặn route theo quyền: task "Nền giao diện" của Sprint 0.
 */
export const router = createBrowserRouter([
  { path: '/login', element: <SignInPage /> },
  {
    path: '/',
    element: <MainLayout />,
    children: [{ index: true, element: <DashboardPage /> }],
  },
  { path: '/403', element: <ForbiddenPage /> },
  { path: '*', element: <NotFoundPage /> },
]);
