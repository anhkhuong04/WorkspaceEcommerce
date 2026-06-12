import type {
  AdminDashboardDto,
  AdminOrderStatusSummaryDto,
  LowStockProductVariantDto,
  OrderStatus,
  RecentAdminOrderDto
} from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { Button, EmptyState, Pill } from "../../components/ui/AdminUi";

type PillTone = "green" | "red" | "blue" | "orange" | "slate" | "teal";

const statusPresentation: Record<OrderStatus, { tone: PillTone; barClass: string }> = {
  0: { tone: "orange", barClass: "bg-amber-500" },
  1: { tone: "teal", barClass: "bg-teal-600" },
  2: { tone: "blue", barClass: "bg-sky-600" },
  3: { tone: "blue", barClass: "bg-indigo-500" },
  4: { tone: "green", barClass: "bg-emerald-600" },
  5: { tone: "orange", barClass: "bg-orange-500" },
  6: { tone: "red", barClass: "bg-red-500" }
};

interface DashboardHeroProps {
  lastUpdated: string;
  refreshing: boolean;
  onRefresh: () => void;
}

export function DashboardHero({ lastUpdated, refreshing, onRefresh }: DashboardHeroProps) {
  return (
    <section className="relative overflow-hidden rounded-[1.75rem] border border-teal-900/10 bg-gradient-to-br from-slate-950 via-slate-900 to-teal-950 px-5 py-4 text-white shadow-lg shadow-slate-900/10 sm:px-6">
      <div className="absolute -right-16 -top-24 h-56 w-56 rounded-full bg-teal-400/10 blur-3xl" aria-hidden="true" />
      <div className="relative flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <p className="text-xs font-black uppercase tracking-[0.2em] text-teal-300">Operations overview</p>
          <h1 className="mt-1 text-2xl font-black tracking-tight sm:text-3xl">Admin dashboard</h1>
          <p className="mt-1.5 max-w-2xl text-sm leading-5 text-slate-300">
            Track order throughput, completed revenue, recent activity, and inventory requiring attention.
          </p>
        </div>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <div className="rounded-2xl border border-white/10 bg-white/5 px-4 py-2.5 backdrop-blur">
            <p className="text-[11px] font-black uppercase tracking-[0.16em] text-slate-400">Last updated</p>
            <p className="mt-0.5 text-sm font-bold text-white" aria-live="polite">{lastUpdated}</p>
          </div>
          <Button
            type="button"
            variant="primary"
            className="border-teal-400 bg-teal-500 text-slate-950 hover:bg-teal-300"
            disabled={refreshing}
            onClick={onRefresh}
          >
            {refreshing ? "Refreshing..." : "Refresh data"}
          </Button>
        </div>
      </div>
    </section>
  );
}

export function DashboardMetrics({ dashboard }: { dashboard: AdminDashboardDto }) {
  const metrics = [
    {
      label: "Total orders",
      value: dashboard.totalOrders,
      detail: "All order statuses",
      accentClass: "bg-slate-900"
    },
    {
      label: "Completed revenue",
      value: formatMoney(dashboard.totalRevenue),
      detail: "Completed orders only",
      accentClass: "bg-emerald-600"
    },
    {
      label: "New orders",
      value: dashboard.newOrders,
      detail: "Pending confirmation",
      accentClass: "bg-amber-500"
    },
    {
      label: "Low-stock variants",
      value: dashboard.lowStockVariants.length,
      detail: `At or below ${dashboard.lowStockThreshold} units`,
      accentClass: "bg-red-500"
    }
  ];

  return (
    <section className="grid grid-cols-2 gap-3 xl:grid-cols-4 xl:gap-4" aria-label="Dashboard metrics">
      {metrics.map((metric) => (
        <article key={metric.label} className="relative min-w-0 overflow-hidden rounded-2xl border border-slate-200 bg-white p-4 shadow-sm xl:rounded-3xl">
          <span className={`absolute inset-x-0 top-0 h-1 ${metric.accentClass}`} aria-hidden="true" />
          <p className="text-[11px] font-black uppercase tracking-[0.12em] text-slate-500 sm:text-xs">{metric.label}</p>
          <p className="mt-2 truncate text-2xl font-black tracking-tight text-slate-950 xl:text-3xl" title={String(metric.value)}>{metric.value}</p>
          <p className="mt-1 text-xs font-semibold leading-4 text-slate-500 sm:text-sm">{metric.detail}</p>
        </article>
      ))}
    </section>
  );
}

interface OrderStatusOverviewProps {
  summary: AdminOrderStatusSummaryDto[];
  totalOrders: number;
  onViewOrders: () => void;
  onViewStatus: (status: OrderStatus) => void;
}

export function OrderStatusOverview({ summary, totalOrders, onViewOrders, onViewStatus }: OrderStatusOverviewProps) {
  return (
    <DashboardCard
      title="Order status"
      description="Current distribution across the MVP workflow."
      action={<Button type="button" onClick={onViewOrders}>View orders</Button>}
    >
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4 2xl:grid-cols-7">
        {summary.map((item) => {
          const presentation = statusPresentation[item.status];
          const percentage = totalOrders === 0 ? 0 : Math.round((item.count / totalOrders) * 100);

          return (
            <div key={item.status} className="rounded-2xl border border-slate-100 bg-slate-50/70 p-3">
              <div className="mb-1.5 flex items-center justify-between gap-3 text-sm">
                <button
                  type="button"
                  className="rounded-md font-bold text-slate-700 hover:text-teal-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-600 focus-visible:ring-offset-2"
                  onClick={() => onViewStatus(item.status)}
                >
                  {formatOrderStatus(item.status)}
                </button>
                <button
                  type="button"
                  className="rounded-md font-black text-slate-950 hover:text-teal-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-600 focus-visible:ring-offset-2"
                  onClick={() => onViewStatus(item.status)}
                  aria-label={`View ${formatOrderStatus(item.status)} orders`}
                >
                  {item.count} <span className="font-semibold text-slate-400">({percentage}%)</span>
                </button>
              </div>
              <div className="h-1.5 overflow-hidden rounded-full bg-slate-200">
                <div
                  className={`h-full rounded-full transition-[width] ${presentation.barClass}`}
                  style={{ width: `${percentage}%` }}
                  role="progressbar"
                  aria-label={`${formatOrderStatus(item.status)} orders`}
                  aria-valuemin={0}
                  aria-valuemax={Math.max(totalOrders, 1)}
                  aria-valuenow={item.count}
                />
              </div>
            </div>
          );
        })}
      </div>
    </DashboardCard>
  );
}

interface RecentOrdersSectionProps {
  orders: RecentAdminOrderDto[];
  onViewOrders: () => void;
  onViewOrder: (order: RecentAdminOrderDto) => void;
}

export function RecentOrdersSection({ orders, onViewOrders, onViewOrder }: RecentOrdersSectionProps) {
  return (
    <DashboardCard
      title="Recent orders"
      description="The five most recently created orders."
      action={<Button type="button" onClick={onViewOrders}>Open all orders</Button>}
    >
      {orders.length ? (
        <DashboardTableViewport label="Recent orders table">
          <table className="w-full min-w-[680px] text-left text-sm">
            <thead className="sticky top-0 z-10 bg-white text-xs uppercase tracking-wide text-slate-500 shadow-[0_1px_0_0_#e2e8f0]">
              <tr className="border-b border-slate-100">
                <th className="px-3 py-2.5">Order</th>
                <th className="px-3 py-2.5">Customer</th>
                <th className="px-3 py-2.5">Total</th>
                <th className="px-3 py-2.5">Status</th>
                <th className="px-3 py-2.5">Created</th>
                <th className="px-3 py-2.5 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr key={order.id} className="border-b border-slate-100 last:border-0">
                  <td className="px-3 py-2.5 font-black text-slate-900">{order.orderCode}</td>
                  <td className="px-3 py-2.5 font-semibold text-slate-700">{order.customerName}</td>
                  <td className="px-3 py-2.5 font-bold text-slate-900">{formatMoney(order.totalAmount)}</td>
                  <td className="px-3 py-2.5"><OrderStatusPill status={order.status} /></td>
                  <td className="px-3 py-2.5 text-slate-500">{formatDate(order.createdAt)}</td>
                  <td className="px-3 py-2.5 text-right"><Button type="button" variant="ghost" onClick={() => onViewOrder(order)}>Open</Button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </DashboardTableViewport>
      ) : (
        <EmptyState>No recent orders</EmptyState>
      )}
    </DashboardCard>
  );
}

interface LowStockSectionProps {
  threshold: number;
  variants: LowStockProductVariantDto[];
  onViewProducts: () => void;
  onViewVariant: (variant: LowStockProductVariantDto) => void;
}

export function LowStockSection({ threshold, variants, onViewProducts, onViewVariant }: LowStockSectionProps) {
  return (
    <DashboardCard
      title="Inventory attention"
      description={`Variants at or below the ${threshold}-unit low-stock threshold.`}
      action={<Button type="button" onClick={onViewProducts}>Open products</Button>}
    >
      {variants.length ? (
        <DashboardTableViewport label="Low-stock variants table">
          <table className="w-full min-w-[680px] text-left text-sm">
            <thead className="sticky top-0 z-10 bg-white text-xs uppercase tracking-wide text-slate-500 shadow-[0_1px_0_0_#e2e8f0]">
              <tr className="border-b border-slate-100">
                <th className="px-3 py-2.5">Product</th>
                <th className="px-3 py-2.5">SKU</th>
                <th className="px-3 py-2.5">Variant</th>
                <th className="px-3 py-2.5">Inventory</th>
                <th className="px-3 py-2.5">Availability</th>
                <th className="px-3 py-2.5 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {variants.map((variant) => {
                const stockState = getStockState(variant.stockQuantity, threshold);

                return (
                  <tr key={variant.variantId} className="border-b border-slate-100 last:border-0">
                    <td className="px-3 py-2.5 font-black text-slate-900">{variant.productName}</td>
                    <td className="px-3 py-2.5 font-semibold text-slate-600">{variant.sku}</td>
                    <td className="px-3 py-2.5 text-slate-600">{variant.variantName}</td>
                    <td className="px-3 py-2.5">
                      <div className="flex items-center gap-2">
                        <span className="text-lg font-black text-slate-950">{variant.stockQuantity}</span>
                        <Pill tone={stockState.tone}>{stockState.label}</Pill>
                      </div>
                    </td>
                    <td className="px-3 py-2.5"><Pill tone={variant.isActive ? "green" : "slate"}>{variant.isActive ? "Active" : "Inactive"}</Pill></td>
                    <td className="px-3 py-2.5 text-right"><Button type="button" variant="ghost" onClick={() => onViewVariant(variant)}>Manage</Button></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </DashboardTableViewport>
      ) : (
        <EmptyState>No variants are at or below the current threshold</EmptyState>
      )}
    </DashboardCard>
  );
}

export function DashboardSkeleton() {
  return (
    <div className="grid gap-4" aria-label="Loading dashboard">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[0, 1, 2, 3].map((item) => <div key={item} className="h-32 animate-pulse rounded-3xl bg-slate-200/70" />)}
      </div>
      <div className="h-44 animate-pulse rounded-3xl bg-slate-200/70" />
      <div className="grid gap-4 2xl:grid-cols-2">
        <div className="h-96 animate-pulse rounded-3xl bg-slate-200/70" />
        <div className="h-96 animate-pulse rounded-3xl bg-slate-200/70" />
      </div>
    </div>
  );
}

function DashboardCard({ title, description, action, children }: { title: string; description: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="min-w-0 rounded-3xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-xl font-black tracking-tight text-slate-950">{title}</h2>
          <p className="mt-1 text-sm font-semibold text-slate-500">{description}</p>
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

function DashboardTableViewport({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <p className="mb-2 text-xs font-semibold text-slate-400 sm:hidden">Swipe horizontally to view all columns.</p>
      <div
        className="admin-table-scroll max-h-[22rem] overflow-auto rounded-2xl border border-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-600 focus-visible:ring-offset-2"
        role="region"
        aria-label={label}
        tabIndex={0}
      >
        {children}
      </div>
    </div>
  );
}

function OrderStatusPill({ status }: { status: OrderStatus }) {
  return <Pill tone={statusPresentation[status].tone}>{formatOrderStatus(status)}</Pill>;
}

function getStockState(stockQuantity: number, threshold: number): { label: string; tone: PillTone } {
  if (stockQuantity === 0) {
    return { label: "Out of stock", tone: "red" };
  }

  if (stockQuantity <= Math.max(1, Math.floor(threshold / 2))) {
    return { label: "Critical", tone: "red" };
  }

  return { label: "Low", tone: "orange" };
}
