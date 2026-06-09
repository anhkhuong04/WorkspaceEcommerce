import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminOrderListRequest, OrderStatus } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import { Alert, Button, Card, Descriptions, Drawer, Empty, Form, Input, Select, Space, Table, Tag, Timeline, Typography, message } from "antd";
import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const orderStatuses: OrderStatus[] = [0, 1, 2, 3, 4, 5, 6];
const nextStatusesByStatus: Record<OrderStatus, OrderStatus[]> = {
  0: [1, 6],
  1: [2],
  2: [3],
  3: [4, 5],
  4: [],
  5: [3, 6],
  6: []
};

const statusSchema = z.object({
  status: z.union([z.literal(0), z.literal(1), z.literal(2), z.literal(3), z.literal(4), z.literal(5), z.literal(6)]),
  note: z.string().trim().max(1000, "Note is too long.").optional()
});

type StatusFormValues = z.infer<typeof statusSchema>;

const statusColors: Record<OrderStatus, string> = {
  0: "gold",
  1: "cyan",
  2: "blue",
  3: "geekblue",
  4: "green",
  5: "orange",
  6: "red"
};

function orderStatusTag(status: OrderStatus) {
  return <Tag color={statusColors[status]}>{formatOrderStatus(status)}</Tag>;
}

function toStatusRequest(values: StatusFormValues) {
  return {
    status: values.status,
    note: values.note?.trim() ? values.note.trim() : null
  };
}

export function OrdersPage() {
  const queryClient = useQueryClient();
  const [messageApi, contextHolder] = message.useMessage();
  const [request, setRequest] = useState<AdminOrderListRequest>({ pageNumber: 1, pageSize: 8 });
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);

  const ordersQuery = useQuery({ queryKey: ["admin-orders", request], queryFn: () => adminApi.getOrders(request) });
  const orderQuery = useQuery({
    queryKey: ["admin-order", selectedOrderId],
    queryFn: () => adminApi.getOrder(selectedOrderId ?? ""),
    enabled: selectedOrderId !== null
  });
  const order = orderQuery.data;
  const nextStatuses = useMemo(() => order ? nextStatusesByStatus[order.status] : [], [order]);

  const statusForm = useForm<StatusFormValues>({
    resolver: zodResolver(statusSchema),
    defaultValues: { status: 1, note: "" }
  });

  useEffect(() => {
    if (nextStatuses.length > 0) {
      statusForm.reset({ status: nextStatuses[0], note: "" });
    }
  }, [nextStatuses, statusForm]);

  const updateStatusMutation = useMutation({
    mutationFn: (values: StatusFormValues) => {
      if (!selectedOrderId) {
        throw new Error("Order is required.");
      }

      return adminApi.updateOrderStatus(selectedOrderId, toStatusRequest(values));
    },
    onSuccess: async (updatedOrder) => {
      queryClient.setQueryData(["admin-order", selectedOrderId], updatedOrder);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-orders"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })
      ]);
      messageApi.success("Order status updated.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  function updateFilters(values: Partial<AdminOrderListRequest>) {
    setRequest((current) => ({ ...current, ...values, pageNumber: 1 }));
  }

  return (
    <div className="admin-page-grid">
      {contextHolder}
      <AdminPageHeader
        title="Orders"
        description="Review orders, inspect order snapshots, render status history, and move orders through MVP transitions."
      />

      {ordersQuery.isError ? (
        <Alert type="error" showIcon message="Orders could not be loaded" description={getApiErrorMessage(ordersQuery.error)} />
      ) : null}

      <Card>
        <Space className="admin-toolbar" wrap>
          <Input.Search
            allowClear
            placeholder="Search code, customer, phone"
            onSearch={(value) => updateFilters({ search: value.trim() || undefined })}
            style={{ width: 320 }}
          />
          <Select
            allowClear
            placeholder="Status"
            value={request.status}
            onChange={(value) => updateFilters({ status: value })}
            style={{ width: 220 }}
            options={orderStatuses.map((status) => ({ value: status, label: formatOrderStatus(status) }))}
          />
        </Space>
        <Table
          rowKey="id"
          loading={ordersQuery.isLoading}
          dataSource={ordersQuery.data?.items ?? []}
          locale={{ emptyText: <Empty description="No orders found" /> }}
          pagination={{
            current: ordersQuery.data?.pageNumber ?? request.pageNumber,
            pageSize: ordersQuery.data?.pageSize ?? request.pageSize,
            total: ordersQuery.data?.totalCount ?? 0,
            showSizeChanger: true
          }}
          onChange={(pagination) => setRequest((current) => ({
            ...current,
            pageNumber: pagination.current ?? 1,
            pageSize: pagination.pageSize ?? current.pageSize
          }))}
          columns={[
            { title: "Code", dataIndex: "orderCode", width: 150 },
            { title: "Customer", dataIndex: "customerName" },
            { title: "Phone", dataIndex: "customerPhone", width: 150 },
            { title: "Items", dataIndex: "itemCount", width: 90 },
            { title: "Total", dataIndex: "totalAmount", width: 140, render: (value: number) => formatMoney(value) },
            { title: "Status", dataIndex: "status", width: 150, render: (value: OrderStatus) => orderStatusTag(value) },
            { title: "Payment", dataIndex: "paymentMethod", width: 180, render: (value: 0 | 1) => formatPaymentMethod(value) },
            { title: "Created", dataIndex: "createdAt", width: 180, render: (value: string) => formatDate(value) },
            { title: "Actions", key: "actions", width: 100, render: (_, record) => <Button onClick={() => setSelectedOrderId(record.id)}>View</Button> }
          ]}
        />
      </Card>

      <Drawer
        title={order ? `Order ${order.orderCode}` : "Order detail"}
        open={selectedOrderId !== null}
        onClose={() => setSelectedOrderId(null)}
        width={760}
      >
        {orderQuery.isError ? (
          <Alert type="error" showIcon message="Order could not be loaded" description={getApiErrorMessage(orderQuery.error)} />
        ) : null}
        <Space direction="vertical" size={16} className="admin-drawer-stack">
          <Card loading={orderQuery.isLoading}>
            {order ? (
              <Descriptions column={2} size="small" bordered>
                <Descriptions.Item label="Status">{orderStatusTag(order.status)}</Descriptions.Item>
                <Descriptions.Item label="Payment">{formatPaymentMethod(order.paymentMethod)}</Descriptions.Item>
                <Descriptions.Item label="Customer">{order.customerName}</Descriptions.Item>
                <Descriptions.Item label="Phone">{order.customerPhone}</Descriptions.Item>
                <Descriptions.Item label="Email">{order.customerEmail ?? "-"}</Descriptions.Item>
                <Descriptions.Item label="Created">{formatDate(order.createdAt)}</Descriptions.Item>
                <Descriptions.Item label="Shipping address" span={2}>{order.shippingAddress}</Descriptions.Item>
                <Descriptions.Item label="Customer note" span={2}>{order.note ?? "-"}</Descriptions.Item>
                <Descriptions.Item label="Subtotal">{formatMoney(order.subtotal)}</Descriptions.Item>
                <Descriptions.Item label="Shipping fee">{formatMoney(order.shippingFee)}</Descriptions.Item>
                <Descriptions.Item label="Discount">{formatMoney(order.discountAmount)}</Descriptions.Item>
                <Descriptions.Item label="Total">{formatMoney(order.totalAmount)}</Descriptions.Item>
              </Descriptions>
            ) : null}
          </Card>

          <Card title="Items" loading={orderQuery.isLoading}>
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={order?.items ?? []}
              locale={{ emptyText: <Empty description="No order items" /> }}
              columns={[
                { title: "Product", dataIndex: "productNameSnapshot" },
                { title: "SKU", dataIndex: "skuSnapshot", width: 150 },
                { title: "Price", dataIndex: "unitPrice", width: 120, render: (value: number) => formatMoney(value) },
                { title: "Qty", dataIndex: "quantity", width: 80 },
                { title: "Line", dataIndex: "lineTotal", width: 120, render: (value: number) => formatMoney(value) },
                { title: "Install", dataIndex: "requiresInstallation", width: 120, render: (value: boolean) => value ? <Tag color="blue">Required</Tag> : <Tag>None</Tag> }
              ]}
            />
          </Card>

          <Card title="Status transition" loading={orderQuery.isLoading}>
            {order && nextStatuses.length === 0 ? (
              <Empty description="This order is in a terminal status" />
            ) : (
              <Form layout="vertical">
                <Controller
                  control={statusForm.control}
                  name="status"
                  render={({ field, fieldState }) => (
                    <Form.Item label="Next status" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                      <Select
                        value={field.value}
                        onChange={field.onChange}
                        options={nextStatuses.map((status) => ({ value: status, label: formatOrderStatus(status) }))}
                      />
                    </Form.Item>
                  )}
                />
                <Controller
                  control={statusForm.control}
                  name="note"
                  render={({ field, fieldState }) => (
                    <Form.Item label="Internal note" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                      <Input.TextArea {...field} rows={3} placeholder="Optional note for status history" />
                    </Form.Item>
                  )}
                />
                <Button
                  type="primary"
                  loading={updateStatusMutation.isPending}
                  disabled={!order || nextStatuses.length === 0}
                  onClick={statusForm.handleSubmit((values) => updateStatusMutation.mutate(values))}
                >
                  Update status
                </Button>
              </Form>
            )}
          </Card>

          <Card title="Status history" loading={orderQuery.isLoading}>
            {order && order.statusHistory.length > 0 ? (
              <Timeline
                items={order.statusHistory.map((history) => ({
                  color: statusColors[history.toStatus],
                  children: (
                    <Space direction="vertical" size={2}>
                      <Typography.Text strong>
                        {history.fromStatus === null ? "Created" : formatOrderStatus(history.fromStatus)} to {formatOrderStatus(history.toStatus)}
                      </Typography.Text>
                      <Typography.Text type="secondary">
                        {formatDate(history.changedAt)} by {history.changedBy ?? "system"}
                      </Typography.Text>
                      {history.note ? <Typography.Text>{history.note}</Typography.Text> : null}
                    </Space>
                  )
                }))}
              />
            ) : (
              <Empty description="No status history" />
            )}
          </Card>
        </Space>
      </Drawer>
    </div>
  );
}