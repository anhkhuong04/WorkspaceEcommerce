import { formatDate } from "@workspace-ecommerce/shared-utils";
import { Link, useNavigate } from "react-router-dom";
import { Notice } from "../../components/ui/AdminUi";
import { useAdminDashboard } from "../../hooks/queries/useAdminDashboard";
import { getApiErrorMessage } from "../../services/api/errors";
import {
  DashboardHero,
  DashboardMetrics,
  DashboardSkeleton,
  LowStockSection,
  OrderStatusOverview,
  RecentOrdersSection,
} from "./DashboardSections";

export function DashboardPage() {
  const navigate = useNavigate();
  const dashboardQuery = useAdminDashboard();
  const dashboard = dashboardQuery.data;
  const lastUpdated =
    dashboardQuery.dataUpdatedAt > 0
      ? formatDate(new Date(dashboardQuery.dataUpdatedAt))
      : "Waiting for dashboard data";

  return (
    <div className="admin-page-grid">
      <DashboardHero
        lastUpdated={lastUpdated}
        refreshing={dashboardQuery.isFetching}
        onRefresh={() => void dashboardQuery.refetch()}
      />

      {dashboardQuery.isError ? (
        <Notice type="error" title="Dashboard could not be loaded">
          {getApiErrorMessage(dashboardQuery.error)}
        </Notice>
      ) : null}

      {dashboardQuery.isLoading ? (
        <DashboardSkeleton />
      ) : dashboard ? (
        <>
          <DashboardMetrics dashboard={dashboard} />
          {dashboard.operations && <section className="admin-panel" aria-label="Operational queues">
            <h2 className="text-base font-semibold">Needs attention</h2>
            <p className="mt-1 text-xs text-[var(--muted)]">Open commerce work and retained background failure records. Select a group to review.</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {dashboard.operations.map(queue => <Link key={queue.key} to={queue.url} className="flex min-h-16 items-center justify-between gap-3 rounded-lg border border-slate-200 p-3 hover:bg-slate-50">
                <span className="text-sm">{queue.label}</span><span className="text-xl font-semibold tabular-nums">{queue.count}</span>
              </Link>)}
            </div>
          </section>}

          <OrderStatusOverview
            summary={dashboard.orderStatusSummary}
            totalOrders={dashboard.totalOrders}
            onViewOrders={() => navigate("/orders")}
            onViewStatus={(status) =>
              navigate(
                `/orders?${new URLSearchParams({ status: String(status) })}`,
              )
            }
          />

          <div
            className={`grid min-w-0 gap-5 ${dashboard.recentOrders.length ? "2xl:grid-cols-2" : ""}`}
          >
            {dashboard.recentOrders.length ? (
              <RecentOrdersSection
                orders={dashboard.recentOrders}
                onViewOrders={() => navigate("/orders")}
                onViewOrder={(order) =>
                  navigate(
                    `/orders?${new URLSearchParams({ search: order.orderCode })}`,
                  )
                }
              />
            ) : null}
            <LowStockSection
              threshold={dashboard.lowStockThreshold}
              variants={dashboard.lowStockVariants}
              onViewProducts={() => navigate("/products")}
              onViewVariant={(variant) =>
                navigate(
                  `/products?${new URLSearchParams({ productId: variant.productId, variantId: variant.variantId })}`,
                )
              }
            />
          </div>
        </>
      ) : null}
    </div>
  );
}
