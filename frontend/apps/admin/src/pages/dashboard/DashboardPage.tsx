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

          <div className="grid gap-4 xl:grid-cols-[minmax(300px,0.85fr)_minmax(0,2.15fr)]">
            <OrderStatusOverview
              summary={dashboard.orderStatusSummary}
              totalOrders={dashboard.totalOrders}
              onViewOrders={() => navigate("/orders")}
            />
            <RecentOrdersSection
              orders={dashboard.recentOrders}
              onViewOrders={() => navigate("/orders")}
            />
          </div>

          <LowStockSection
            threshold={dashboard.lowStockThreshold}
            variants={dashboard.lowStockVariants}
            onViewProducts={() => navigate("/products")}
          />
        </>
      ) : null}
    </div>
  );
}
