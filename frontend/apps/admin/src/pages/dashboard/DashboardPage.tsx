import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Alert, Card, Col, Empty, Row, Skeleton, Statistic, Table, Tag, Typography } from "antd";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

export function DashboardPage() {
  const dashboardQuery = useQuery({
    queryKey: ["admin-dashboard"],
    queryFn: adminApi.getDashboard
  });
  const dashboard = dashboardQuery.data;

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Dashboard"
        description="Operating metrics from live orders, completed revenue, pending orders, and low stock variants."
      />

      {dashboardQuery.isError ? (
        <Alert
          type="error"
          showIcon
          message="Dashboard could not be loaded"
          description={getApiErrorMessage(dashboardQuery.error)}
        />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card className="admin-metric-card">
            <Skeleton active paragraph={false} loading={dashboardQuery.isLoading}>
              <Statistic title="Total orders" value={dashboard?.totalOrders ?? 0} />
            </Skeleton>
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card className="admin-metric-card">
            <Skeleton active paragraph={false} loading={dashboardQuery.isLoading}>
              <Statistic title="Completed revenue" value={dashboard ? formatMoney(dashboard.totalRevenue) : formatMoney(0)} />
            </Skeleton>
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card className="admin-metric-card">
            <Skeleton active paragraph={false} loading={dashboardQuery.isLoading}>
              <Statistic title="New orders" value={dashboard?.newOrders ?? 0} valueStyle={{ color: "#0f766e" }} />
            </Skeleton>
          </Card>
        </Col>
      </Row>

      <Card
        title="Low stock variants"
        extra={<Typography.Text type="secondary">Threshold: 10 units</Typography.Text>}
      >
        <Table
          rowKey="variantId"
          loading={dashboardQuery.isLoading}
          dataSource={dashboard?.lowStockVariants ?? []}
          pagination={false}
          locale={{ emptyText: <Empty description="No low stock variants" /> }}
          columns={[
            { title: "Product", dataIndex: "productName" },
            { title: "SKU", dataIndex: "sku", width: 160 },
            { title: "Variant", dataIndex: "variantName" },
            {
              title: "Stock",
              dataIndex: "stockQuantity",
              width: 120,
              render: (value: number) => <Tag color={value <= 5 ? "red" : "orange"}>{value}</Tag>
            },
            {
              title: "Status",
              dataIndex: "isActive",
              width: 120,
              render: (value: boolean) => <Tag color={value ? "green" : "default"}>{value ? "Active" : "Inactive"}</Tag>
            }
          ]}
        />
      </Card>
    </div>
  );
}
