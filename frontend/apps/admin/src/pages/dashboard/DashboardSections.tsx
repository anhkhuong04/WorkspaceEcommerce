import type {
  AdminDashboardDto,
  AdminOrderStatusSummaryDto,
  LowStockProductVariantDto,
  OrderStatus,
  RecentAdminOrderDto,
} from "@workspace-ecommerce/api-types";
import {
  formatDate,
  formatMoney,
  formatOrderStatus,
} from "@workspace-ecommerce/shared-utils";
import {
  ArrowUpRight,
  CircleDollarSign,
  ClipboardList,
  PackageSearch,
  RefreshCw,
  ShoppingBag,
} from "lucide-react";
import type { ReactNode } from "react";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import {
  Button,
  EmptyState,
  IconButton,
  Pill,
  TableViewport,
} from "../../components/ui/AdminUi";
import { formatLocalizedText } from "../../utils/localizedText";

type PillTone = "green" | "red" | "blue" | "orange" | "slate";
const statusPresentation: Record<
  OrderStatus,
  { tone: PillTone; barClass: string }
> = {
  0: { tone: "orange", barClass: "bg-amber-500" },
  1: { tone: "slate", barClass: "bg-slate-500" },
  2: { tone: "blue", barClass: "bg-sky-600" },
  3: { tone: "blue", barClass: "bg-indigo-500" },
  4: { tone: "green", barClass: "bg-emerald-600" },
  5: { tone: "orange", barClass: "bg-orange-600" },
  6: { tone: "red", barClass: "bg-red-600" },
  7: { tone: "slate", barClass: "bg-slate-700" },
};

export function DashboardHero({
  lastUpdated,
  refreshing,
  onRefresh,
}: {
  lastUpdated: string;
  refreshing: boolean;
  onRefresh: () => void;
}) {
  return (
    <AdminPageHeader
      title="Dashboard"
      description="A clear view of orders, revenue and inventory."
      actions={
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-xs text-[var(--muted)]">
            Updated {lastUpdated}
          </span>
          <Button disabled={refreshing} onClick={onRefresh}>
            <RefreshCw
              size={16}
              className={refreshing ? "animate-spin" : ""}
              aria-hidden="true"
            />
            {refreshing ? "Refreshing..." : "Refresh"}
          </Button>
        </div>
      }
    />
  );
}

export function DashboardMetrics({
  dashboard,
}: {
  dashboard: AdminDashboardDto;
}) {
  const metrics = [
    {
      label: "Total orders",
      value: dashboard.totalOrders,
      detail: "Across all statuses",
      icon: ShoppingBag,
    },
    {
      label: "Net collected on fulfilled orders",
      value: dashboard.netCollectedRevenue === undefined ? "Unavailable" : formatMoney(dashboard.netCollectedRevenue),
      detail: "All time · verified paid less recorded refunds · VND at order rate",
      icon: CircleDollarSign,
    },
    {
      label: "Awaiting confirmation",
      value: dashboard.newOrders,
      detail: "Pending orders",
      icon: ClipboardList,
    },
    {
      label: "Stock alerts shown",
      value: dashboard.lowStockVariants.length,
      detail: `Up to 10 variants · ≤ ${dashboard.lowStockThreshold} units`,
      icon: PackageSearch,
    },
  ];
  return (
    <section
      className="grid grid-cols-1 gap-3 min-[380px]:grid-cols-2 xl:grid-cols-4"
      aria-label="Dashboard metrics"
    >
      {metrics.map(({ icon: Icon, ...m }) => (
        <article key={m.label} className="admin-panel !p-4">
          <div className="flex items-start justify-between gap-2">
            <p className="text-sm text-[var(--muted-strong)]">{m.label}</p>
            <Icon
              size={18}
              className="shrink-0 text-[var(--brand)]"
              aria-hidden="true"
            />
          </div>
          <p className="mt-3 break-words text-[clamp(20px,2vw,28px)] font-semibold leading-tight tracking-tight tabular-nums">
            {m.value}
          </p>
          <p className="mt-2 text-xs text-[var(--muted)]">{m.detail}</p>
        </article>
      ))}
    </section>
  );
}

export function OrderStatusOverview({
  summary,
  totalOrders,
  onViewOrders,
  onViewStatus,
}: {
  summary: AdminOrderStatusSummaryDto[];
  totalOrders: number;
  onViewOrders: () => void;
  onViewStatus: (status: OrderStatus) => void;
}) {
  return (
    <DashboardCard
      title="Order activity"
      description="Distribution by current order status."
      action={
        <Button variant="ghost" onClick={onViewOrders}>
          View orders
          <ArrowUpRight size={16} aria-hidden="true" />
        </Button>
      }
    >
      {totalOrders === 0 ? (
        <div className="flex flex-col gap-1 rounded-lg bg-slate-50 px-4 py-5 text-sm">
          <p className="font-medium">No orders yet</p>
          <p className="text-[var(--muted)]">
            Order activity will appear here when customers place an order.
          </p>
        </div>
      ) : (
        <>
          <div
            className="mb-5 flex h-3 overflow-hidden rounded-full bg-slate-100"
            aria-hidden="true"
          >
            {summary
              .filter((s) => s.count > 0)
              .map((s) => (
                <div
                  key={s.status}
                  className={statusPresentation[s.status].barClass}
                  style={{ width: `${(s.count / totalOrders) * 100}%` }}
                />
              ))}
          </div>
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
            {summary.map((s) => (
              <button
                key={s.status}
                type="button"
                onClick={() => onViewStatus(s.status)}
                className="flex min-h-11 items-center justify-between gap-3 rounded-lg px-3 py-2 text-left hover:bg-slate-50"
                aria-label={`View ${formatOrderStatus(s.status)} orders`}
              >
                <span className="flex items-center gap-2 text-sm text-[var(--muted-strong)]">
                  <span
                    className={`h-2 w-2 shrink-0 rounded-full ${statusPresentation[s.status].barClass}`}
                    aria-hidden="true"
                  />
                  {formatOrderStatus(s.status)}
                </span>
                <span className="whitespace-nowrap font-semibold tabular-nums">
                  {s.count}
                  <span className="ml-2 text-xs font-normal text-[var(--muted)]">
                    {Math.round((s.count / totalOrders) * 100)}%
                  </span>
                </span>
              </button>
            ))}
          </div>
        </>
      )}
    </DashboardCard>
  );
}

export function RecentOrdersSection({
  orders,
  onViewOrders,
  onViewOrder,
}: {
  orders: RecentAdminOrderDto[];
  onViewOrders: () => void;
  onViewOrder: (order: RecentAdminOrderDto) => void;
}) {
  return (
    <DashboardCard
      title="Recent orders"
      description="The five latest orders."
      action={
        <Button variant="ghost" onClick={onViewOrders}>
          All orders
          <ArrowUpRight size={16} aria-hidden="true" />
        </Button>
      }
    >
      {orders.length ? (
        <TableViewport label="Recent orders">
          <table className="w-full min-w-[600px]">
            <thead>
              <tr>
                <th>Order</th>
                <th>Customer</th>
                <th>Total</th>
                <th>Status</th>
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {orders.map((o) => (
                <tr
                  key={o.id}
                  className="border-b border-slate-100 last:border-0"
                >
                  <td>
                    <button
                      className="text-left font-medium text-[var(--brand)] hover:underline"
                      onClick={() => onViewOrder(o)}
                    >
                      {o.orderCode}
                    </button>
                    <p className="mt-1 text-xs text-[var(--muted)]">
                      {formatDate(o.createdAt)}
                    </p>
                  </td>
                  <td>{formatLocalizedText(o.customerName)}</td>
                  <td className="whitespace-nowrap font-medium tabular-nums">
                    {formatMoney(o.totalAmount)}
                  </td>
                  <td>
                    <Pill tone={statusPresentation[o.status].tone}>
                      {formatOrderStatus(o.status)}
                    </Pill>
                  </td>
                  <td>
                    <IconButton
                      label={`Open order ${o.orderCode}`}
                      onClick={() => onViewOrder(o)}
                    >
                      <ArrowUpRight size={17} aria-hidden="true" />
                    </IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableViewport>
      ) : (
        <EmptyState>No recent orders</EmptyState>
      )}
    </DashboardCard>
  );
}

export function LowStockSection({
  threshold,
  variants,
  onViewProducts,
  onViewVariant,
}: {
  threshold: number;
  variants: LowStockProductVariantDto[];
  onViewProducts: () => void;
  onViewVariant: (variant: LowStockProductVariantDto) => void;
}) {
  return (
    <DashboardCard
      title="Inventory to review"
      description={`First ${variants.length} alerts at or below ${threshold} units.`}
      action={
        <Button variant="ghost" onClick={onViewProducts}>
          Products
          <ArrowUpRight size={16} aria-hidden="true" />
        </Button>
      }
    >
      {variants.length ? (
        <TableViewport label="Inventory alerts" className="max-h-[420px]">
          <table className="w-full min-w-[520px]">
            <thead className="sticky top-0 z-10">
              <tr>
                <th>Product / variant</th>
                <th>Stock</th>
                <th>Visibility</th>
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {variants.map((v) => (
                <tr
                  key={v.variantId}
                  className="border-b border-slate-100 last:border-0"
                >
                  <td className="max-w-[400px]">
                    <p className="line-clamp-2 font-medium">
                      {formatLocalizedText(v.productName)}
                    </p>
                    <p className="mt-1 line-clamp-2 text-xs text-[var(--muted)]">
                      {v.variantName}
                    </p>
                    <p className="mt-1 font-mono text-xs text-[var(--muted)]">
                      {v.sku}
                    </p>
                  </td>
                  <td>
                    <Pill tone={v.stockQuantity === 0 ? "red" : "orange"}>
                      {v.stockQuantity === 0
                        ? "Out of stock"
                        : `${v.stockQuantity} left`}
                    </Pill>
                  </td>
                  <td>
                    <span className="text-xs text-[var(--muted-strong)]">
                      {v.isActive ? "Active" : "Inactive"}
                    </span>
                  </td>
                  <td>
                    <IconButton
                      label={`Manage ${v.sku}`}
                      onClick={() => onViewVariant(v)}
                    >
                      <ArrowUpRight size={17} aria-hidden="true" />
                    </IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableViewport>
      ) : (
        <EmptyState>
          No variants to review at the {threshold}-unit alert threshold.
        </EmptyState>
      )}
    </DashboardCard>
  );
}
export function DashboardSkeleton() {
  return (
    <div className="grid gap-4" role="status" aria-label="Loading dashboard">
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <div
            key={i}
            className="h-32 animate-pulse rounded-xl bg-slate-200/70"
          />
        ))}
      </div>
      <div className="h-44 animate-pulse rounded-xl bg-slate-200/70" />
      <div className="h-80 animate-pulse rounded-xl bg-slate-200/70" />
    </div>
  );
}
function DashboardCard({
  title,
  description,
  action,
  children,
}: {
  title: string;
  description: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="admin-panel">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="text-base font-semibold">{title}</h2>
          <p className="mt-1 text-xs text-[var(--muted)]">{description}</p>
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}
