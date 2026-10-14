import { Layout, Menu, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';

const { Header, Sider, Content } = Layout;

// Menu sẽ lọc theo quyền của người dùng (Permission Matrix) khi có API đăng nhập.
const items: MenuProps['items'] = [
  { key: '/', label: 'Tổng quan' },
  { key: '/contracts', label: 'Hợp đồng' },
  { key: '/partners', label: 'Đối tác' },
  { key: '/documents', label: 'Tài liệu & số hóa' },
  { key: '/support', label: 'Hỗ trợ' },
  { key: '/organization', label: 'Tổ chức & người dùng' },
];

export function MainLayout() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider breakpoint="lg" collapsedWidth={0} theme="light">
        <Typography.Title level={4} style={{ padding: '16px 24px', margin: 0 }}>
          ConTrack
        </Typography.Title>
        <Menu
          mode="inline"
          selectedKeys={[location.pathname]}
          items={items}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', paddingInline: 24 }} />
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
