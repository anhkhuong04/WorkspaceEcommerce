import { useQuery } from "@tanstack/react-query";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import { Card, Table, Tag } from "antd";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";

export function OrdersPage() {
  const ordersQuery = useQuery({ queryKey: ["admin-orders"], queryFn: adminApi.getOrders });

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Orders" description="Order list foundation with status, payment, totals, and customer lookup columns." />
      <Card>
        <Table
          rowKey="id"
          loading={ordersQuery.isLoading}
          dataSource={ordersQuery.data?.items ?? []}
          pagination={{ pageSize: 8 }}
          columns={[
            { title: "Code", dataIndex: "orderCode" },
            { title: "Customer", dataIndex: "customerName" },
            { title: "Phone", dataIndex: "customerPhone" },
            { title: "Total", dataIndex: "totalAmount", render: (value: number) => formatMoney(value) },
            { title: "Status", dataIndex: "status", render: (value: 0 | 1 | 2 | 3 | 4 | 5 | 6) => <Tag>{formatOrderStatus(value)}</Tag> },
            { title: "Payment", dataIndex: "paymentMethod", render: (value: 0 | 1) => formatPaymentMethod(value) },
            { title: "Created", dataIndex: "createdAt", render: (value: string) => formatDate(value) }
          ]}
        />
      </Card>
    </div>
  );
}
