import { AppstoreOutlined, DashboardOutlined, OrderedListOutlined, PictureOutlined, ShoppingOutlined, TagsOutlined } from "@ant-design/icons";
import { Layout, Menu, Typography } from "antd";
import type { MenuProps } from "antd";
import { Link, Outlet, useLocation } from "react-router-dom";

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
          <Typography.Text strong>Operations console</Typography.Text>
          <Typography.Text type="secondary">Full HD optimized MVP admin foundation</Typography.Text>
        </Header>
        <Content className="admin-content">
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
