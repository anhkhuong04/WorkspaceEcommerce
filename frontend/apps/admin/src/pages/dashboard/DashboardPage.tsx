import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Card, Col, Row, Table, Tag, Typography } from "antd";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";

export function DashboardPage() {
  const dashboardQuery = useQuery({
    queryKey: ["admin-dashboard"],
    queryFn: adminApi.getDashboard
  });
  const dashboard = dashboardQuery.data;

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Dashboard" description="Basic operating metrics for orders, revenue, new orders, and low stock variants." />
      <Row gutter={[16, 16]}>
        <Col span={8}><Card title="Total orders"> <Typography.Title level={2}>{dashboard?.totalOrders ?? "-"}</Typography.Title></Card></Col>
        <Col span={8}><Card title="Completed revenue"> <Typography.Title level={2}>{dashboard ? formatMoney(dashboard.totalRevenue) : "-"}</Typography.Title></Card></Col>
        <Col span={8}><Card title="New orders"> <Typography.Title level={2}>{dashboard?.newOrders ?? "-"}</Typography.Title></Card></Col>
      </Row>
      <Card title="Low stock variants" loading={dashboardQuery.isLoading}>
        <Table
          rowKey="variantId"
          dataSource={dashboard?.lowStockVariants ?? []}
          pagination={false}
          columns={[
            { title: "Product", dataIndex: "productName" },
            { title: "SKU", dataIndex: "sku" },
            { title: "Variant", dataIndex: "variantName" },
            { title: "Stock", dataIndex: "stockQuantity", render: (value: number) => <Tag color={value <= 5 ? "red" : "green"}>{value}</Tag> }
          ]}
        />
      </Card>
    </div>
  );
}
