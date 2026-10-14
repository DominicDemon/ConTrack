import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { RouterProvider } from 'react-router-dom';
import { router } from './router';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
});

export function App() {
  return (
    <ConfigProvider locale={viVN} theme={{ token: { colorPrimary: '#1f4e8c' } }}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ConfigProvider>
  );
}
