import {
  AppstoreOutlined,
  DashboardOutlined,
  LogoutOutlined,
  OrderedListOutlined,
  PictureOutlined,
  ShoppingOutlined,
  TagsOutlined
} from "@ant-design/icons";
import { Button, Layout, Menu, Space, Typography } from "antd";
import type { MenuProps } from "antd";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { useAdminAuth } from "../../features/auth/useAdminAuth";

const { Header, Sider, Content } = Layout;

const menuItems: MenuProps["items"] = [
  { key: "/", icon: <DashboardOutlined />, label: <Link to="/">Dashboard</Link> },
  { key: "/categories", icon: <TagsOutlined />, label: <Link to="/categories">Categories</Link> },
  { key: "/products", icon: <ShoppingOutlined />, label: <Link to="/products">Products</Link> },
  { key: "/orders", icon: <OrderedListOutlined />, label: <Link to="/orders">Orders</Link> },
  { key: "/banners", icon: <PictureOutlined />, label: <Link to="/banners">Banners</Link> }
];

export function AdminLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { session, signOut } = useAdminAuth();

  function handleLogout() {
    signOut();
    queryClient.clear();
    navigate("/login", { replace: true });
  }

  return (
    <Layout className="admin-shell">
      <Sider width={264} className="admin-sider">
        <div className="admin-brand">
          <AppstoreOutlined />
          <span>Workspace Admin</span>
        </div>
        <Menu mode="inline" selectedKeys={[location.pathname]} items={menuItems} className="admin-menu" />
      </Sider>
      <Layout>
        <Header className="admin-header">
          <div>
            <Typography.Text strong>Operations console</Typography.Text>
            <Typography.Text type="secondary" className="admin-header-caption">
              Full HD optimized MVP admin foundation
            </Typography.Text>
          </div>
          <Space size={12}>
            <Typography.Text type="secondary">{session?.email}</Typography.Text>
            <Button icon={<LogoutOutlined />} onClick={handleLogout}>
              Logout
            </Button>
          </Space>
        </Header>
        <Content className="admin-content">
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
