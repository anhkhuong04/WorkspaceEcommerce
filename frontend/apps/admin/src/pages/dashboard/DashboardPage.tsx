import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { EmptyState, Notice, Pill } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

function MetricCard({ title, value, loading }: { title: string; value: string | number; loading: boolean }) {
  return (
    <div className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm">
      <p className="text-sm font-bold text-slate-500">{title}</p>
      {loading ? <div className="mt-4 h-9 w-28 animate-pulse rounded-xl bg-slate-100" /> : <p className="mt-3 text-4xl font-black text-slate-950">{value}</p>}
    </div>
  );
}

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
        <Notice type="error" title="Dashboard could not be loaded">{getApiErrorMessage(dashboardQuery.error)}</Notice>
      ) : null}

      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard title="Total orders" value={dashboard?.totalOrders ?? 0} loading={dashboardQuery.isLoading} />
        <MetricCard title="Completed revenue" value={dashboard ? formatMoney(dashboard.totalRevenue) : formatMoney(0)} loading={dashboardQuery.isLoading} />
        <MetricCard title="New orders" value={dashboard?.newOrders ?? 0} loading={dashboardQuery.isLoading} />
      </div>

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-4 flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <h2 className="text-xl font-black text-slate-950">Low stock variants</h2>
          <p className="text-sm font-semibold text-slate-500">Threshold: 10 units</p>
        </div>

        {dashboardQuery.isLoading ? (
          <div className="grid gap-3">
            {[0, 1, 2].map((item) => <div key={item} className="h-12 animate-pulse rounded-2xl bg-slate-100" />)}
          </div>
        ) : dashboard?.lowStockVariants.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500">
                <tr className="border-b border-slate-100">
                  <th className="py-3 pr-4">Product</th>
                  <th className="py-3 pr-4">SKU</th>
                  <th className="py-3 pr-4">Variant</th>
                  <th className="py-3 pr-4">Stock</th>
                  <th className="py-3 pr-4">Status</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.lowStockVariants.map((variant) => (
                  <tr key={variant.variantId} className="border-b border-slate-100 last:border-0">
                    <td className="py-3 pr-4 font-bold text-slate-900">{variant.productName}</td>
                    <td className="py-3 pr-4 text-slate-600">{variant.sku}</td>
                    <td className="py-3 pr-4 text-slate-600">{variant.variantName}</td>
                    <td className="py-3 pr-4"><Pill tone={variant.stockQuantity <= 5 ? "red" : "orange"}>{variant.stockQuantity}</Pill></td>
                    <td className="py-3 pr-4"><Pill tone={variant.isActive ? "green" : "slate"}>{variant.isActive ? "Active" : "Inactive"}</Pill></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState>No low stock variants</EmptyState>
        )}
      </section>
    </div>
  );
}
