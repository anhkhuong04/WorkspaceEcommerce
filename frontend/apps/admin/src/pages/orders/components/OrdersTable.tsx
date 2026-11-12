import type {
  AdminOrderListItemDto,
  OrderStatus,
  PaymentStatus,
} from "@workspace-ecommerce/api-types";
import {
  formatDate,
  formatMoney,
  formatOrderStatus,
  formatPaymentMethod,
  formatPaymentStatus,
} from "@workspace-ecommerce/shared-utils";
import { ArrowUpRight } from "lucide-react";
import {
  EmptyState,
  IconButton,
  Pill,
  TableViewport,
} from "../../../components/ui/AdminUi";
import { formatLocalizedText } from "../../../utils/localizedText";
const statusTones: Record<
  OrderStatus,
  "green" | "red" | "blue" | "orange" | "slate"
> = {
  0: "orange",
  1: "slate",
  2: "blue",
  3: "blue",
  4: "green",
  5: "orange",
  6: "red",
  7: "slate",
};
const paymentTones: Record<
  PaymentStatus,
  "green" | "red" | "blue" | "orange" | "slate"
> = { 0: "slate", 1: "blue", 2: "green", 3: "red", 4: "slate", 5: "orange", 6: "green", 7: "orange" };
export function OrdersTable({
  orders,
  isLoading,
  onView,
}: {
  orders: AdminOrderListItemDto[];
  isLoading: boolean;
  onView: (id: string) => void;
}) {
  if (isLoading)
    return (
      <div className="grid gap-3" role="status" aria-label="Loading orders">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-slate-100" />
        ))}
      </div>
    );
  if (!orders.length)
    return (
      <EmptyState>
        No orders found. Try a different search or status.
      </EmptyState>
    );
  return (
    <>
    <div className="grid gap-3 lg:hidden">
      {orders.map(o => <article key={o.id} aria-label={`Order ${o.orderCode}`} className="min-w-0 rounded-xl border border-slate-200 p-4">
        <div className="flex flex-wrap items-center justify-between gap-2"><p className="break-all font-medium">{o.orderCode}</p><Pill tone={statusTones[o.status]}>{formatOrderStatus(o.status)}</Pill></div>
        <p className="mt-2 break-words text-sm">{o.customerName} · {o.customerPhone}</p>
        <p className="mt-1 text-xs text-[var(--muted)]">{formatDate(o.createdAt)}</p>
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2"><p className="font-semibold tabular-nums">{formatMoney(o.totalAmount)} <span className="text-xs font-normal text-[var(--muted)]">· {o.itemCount} items</span></p><Pill tone={paymentTones[o.paymentStatus]}>{formatPaymentStatus(o.paymentStatus)}</Pill></div>
        <p className="mt-2 break-all text-xs text-[var(--muted)]">{formatPaymentMethod(o.paymentMethod)} · {o.trackingCode || "No shipment"}</p>
        <button type="button" className="admin-button mt-3 w-full" aria-label={`View order ${o.orderCode}`} onClick={() => onView(o.id)}>View order</button>
      </article>)}
    </div>
    <div className="hidden lg:block"><TableViewport label="Orders">
      <table className="w-full min-w-[960px]">
        <thead>
          <tr>
            <th>Order</th>
            <th>Customer</th>
            <th>Total</th>
            <th>Status</th>
            <th>Payment</th>
            <th>Delivery</th>
            <th>
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {orders.map((o) => (
            <tr key={o.id} className="border-b border-slate-100 last:border-0">
              <td>
                <button
                  onClick={() => onView(o.id)}
                  className="font-medium text-[var(--brand)] hover:underline"
                >
                  {o.orderCode}
                </button>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {formatDate(o.createdAt)}
                </p>
              </td>
              <td>
                <p className="font-medium">
                  {formatLocalizedText(o.customerName)}
                </p>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {o.customerPhone}
                </p>
              </td>
              <td className="whitespace-nowrap tabular-nums">
                <p className="font-medium">{formatMoney(o.totalAmount)}</p>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {o.itemCount} items
                </p>
              </td>
              <td>
                <Pill tone={statusTones[o.status]}>
                  {formatOrderStatus(o.status)}
                </Pill>
              </td>
              <td>
                <Pill tone={paymentTones[o.paymentStatus]}>
                  {formatPaymentStatus(o.paymentStatus)}
                </Pill>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {formatPaymentMethod(o.paymentMethod)}
                </p>
              </td>
              <td>
                {o.trackingCode ? (
                  <>
                    <p className="font-mono text-xs">{o.trackingCode}</p>
                    <p className="mt-1 text-xs text-[var(--muted)]">
                      {o.shipmentStatus?.replace(/([a-z])([A-Z])/g, "$1 $2") ??
                        "Awaiting carrier"}
                    </p>
                  </>
                ) : (
                  <span className="text-xs text-[var(--muted)]">
                    No shipment
                  </span>
                )}
                {o.shipping && <p className="mt-1 text-xs text-[var(--muted)]">{o.shipping.method === 0 ? "WorkspaceEC" : "MiniLogistics"} · {formatMoney(o.shipping.customerCharge)}</p>}
              </td>
              <td>
                <IconButton
                  label={`View order ${o.orderCode}`}
                  onClick={() => onView(o.id)}
                >
                  <ArrowUpRight size={18} aria-hidden="true" />
                </IconButton>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableViewport></div>
    </>
  );
}
