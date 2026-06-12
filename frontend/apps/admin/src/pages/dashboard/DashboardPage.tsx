import { useQuery } from "@tanstack/react-query";
import { formatDate } from "@workspace-ecommerce/shared-utils";
import { useNavigate } from "react-router-dom";
import { Notice } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import {
  DashboardHero,
  DashboardMetrics,
  DashboardSkeleton,
  LowStockSection,
  OrderStatusOverview,
  RecentOrdersSection
} from "./DashboardSections";

export function DashboardPage() {
  const navigate = useNavigate();
  const dashboardQuery = useQuery({
    queryKey: ["admin-dashboard"],
    queryFn: adminApi.getDashboard
  });
  const dashboard = dashboardQuery.data;
  const lastUpdated = dashboardQuery.dataUpdatedAt > 0
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

          <OrderStatusOverview
            summary={dashboard.orderStatusSummary}
            totalOrders={dashboard.totalOrders}
            onViewOrders={() => navigate("/orders")}
            onViewStatus={(status) => navigate(`/orders?${new URLSearchParams({ status: String(status) })}`)}
          />

          <div className="grid min-w-0 gap-4 2xl:grid-cols-2">
            <RecentOrdersSection
              orders={dashboard.recentOrders}
              onViewOrders={() => navigate("/orders")}
              onViewOrder={(order) => navigate(`/orders?${new URLSearchParams({ search: order.orderCode })}`)}
            />
            <LowStockSection
              threshold={dashboard.lowStockThreshold}
              variants={dashboard.lowStockVariants}
              onViewProducts={() => navigate("/products")}
              onViewVariant={(variant) => navigate(`/products?${new URLSearchParams({ productId: variant.productId, variantId: variant.variantId })}`)}
            />
          </div>
        </>
      ) : null}
    </div>
  );
}
