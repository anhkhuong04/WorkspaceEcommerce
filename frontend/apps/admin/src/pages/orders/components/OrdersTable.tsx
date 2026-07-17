import type { AdminOrderListItemDto, OrderStatus, PaymentStatus } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod, formatPaymentStatus } from "@workspace-ecommerce/shared-utils";
import { Button, EmptyState, Pill } from "../../../components/ui/AdminUi";

const statusTones: Record<OrderStatus, "green" | "red" | "blue" | "orange" | "slate"> = { 0: "orange", 1: "slate", 2: "blue", 3: "blue", 4: "green", 5: "orange", 6: "red", 7: "slate" };
const paymentStatusTones: Record<PaymentStatus, "green" | "red" | "blue" | "orange" | "slate"> = { 0: "slate", 1: "blue", 2: "green", 3: "red", 4: "slate" };

type OrdersTableProps = {
  orders: AdminOrderListItemDto[];
  isLoading: boolean;
  onView: (orderId: string) => void;
};

export function OrdersTable({ orders, isLoading, onView }: OrdersTableProps) {
  if (isLoading) {
    return (
      <div className="grid gap-3">
        {[0, 1, 2].map((item) => <div key={item} className="h-14 animate-pulse rounded-2xl bg-slate-100" />)}
      </div>
    );
  }

  if (orders.length === 0) {
    return <EmptyState>No orders found</EmptyState>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[1120px] text-left text-sm">
        <thead className="text-xs uppercase tracking-wide text-slate-500">
          <tr className="border-b border-slate-100">
            <th className="py-3 pr-4">Code</th>
            <th className="py-3 pr-4">Customer</th>
            <th className="py-3 pr-4">Phone</th>
            <th className="py-3 pr-4">Items</th>
            <th className="py-3 pr-4">Total</th>
            <th className="py-3 pr-4">Status</th>
            <th className="py-3 pr-4">Payment</th>
            <th className="py-3 pr-4">Payment status</th>
            <th className="py-3 pr-4">Created</th>
            <th className="py-3 pr-4">Actions</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((item) => (
            <tr key={item.id} className="border-b border-slate-100 last:border-0">
              <td className="py-3 pr-4 font-bold">{item.orderCode}</td>
              <td className="py-3 pr-4">{item.customerName}</td>
              <td className="py-3 pr-4">{item.customerPhone}</td>
              <td className="py-3 pr-4">{item.itemCount}</td>
              <td className="py-3 pr-4">{formatMoney(item.totalAmount)}</td>
              <td className="py-3 pr-4">{orderStatusPill(item.status)}</td>
              <td className="py-3 pr-4">{formatPaymentMethod(item.paymentMethod)}</td>
              <td className="py-3 pr-4">{paymentStatusPill(item.paymentStatus)}</td>
              <td className="py-3 pr-4">{formatDate(item.createdAt)}</td>
              <td className="py-3 pr-4"><Button type="button" onClick={() => onView(item.id)}>View</Button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function orderStatusPill(status: OrderStatus) {
  return <Pill tone={statusTones[status]}>{formatOrderStatus(status)}</Pill>;
}

function paymentStatusPill(status: PaymentStatus) {
  return <Pill tone={paymentStatusTones[status]}>{formatPaymentStatus(status)}</Pill>;
}
