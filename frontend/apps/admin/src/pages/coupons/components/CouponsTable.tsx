import type { AdminCouponDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Button, EmptyState, Pill, Toggle } from "../../../components/ui/AdminUi";
import {
  couponStatusTone,
  formatCouponStatus,
  formatDiscount,
  formatDiscountType,
  formatUsage,
  formatWindow
} from "../couponTypes";

type CouponsTableProps = {
  coupons: AdminCouponDto[];
  isLoading: boolean;
  statusPending: boolean;
  deletePending: boolean;
  getTargetSummary: (coupon: AdminCouponDto) => string;
  onEdit: (coupon: AdminCouponDto) => void;
  onDelete: (coupon: AdminCouponDto) => void;
  onToggleStatus: (coupon: AdminCouponDto) => void;
};

export function CouponsTable({
  coupons,
  isLoading,
  statusPending,
  deletePending,
  getTargetSummary,
  onEdit,
  onDelete,
  onToggleStatus
}: CouponsTableProps) {
  if (isLoading) {
    return (
      <div className="grid gap-3">
        {[0, 1, 2].map((item) => <div key={item} className="h-16 animate-pulse rounded-2xl bg-slate-100" />)}
      </div>
    );
  }

  if (coupons.length === 0) {
    return <EmptyState>No coupons found</EmptyState>;
  }

  return (
    <div className="admin-table-scroll overflow-x-auto">
      <table className="w-full min-w-[1180px] text-left text-sm">
        <thead className="text-xs uppercase tracking-wide text-slate-500">
          <tr className="border-b border-slate-100">
            <th className="py-3 pr-4">Code</th>
            <th className="py-3 pr-4">Name</th>
            <th className="py-3 pr-4">Type</th>
            <th className="py-3 pr-4">Value</th>
            <th className="py-3 pr-4">Dates</th>
            <th className="py-3 pr-4">Usage</th>
            <th className="py-3 pr-4">Targets</th>
            <th className="py-3 pr-4">Status</th>
            <th className="py-3 pr-4">Actions</th>
          </tr>
        </thead>
        <tbody>
          {coupons.map((coupon) => (
            <tr key={coupon.id} className="border-b border-slate-100 last:border-0">
              <td className="py-3 pr-4">
                <p className="font-black text-slate-950">{coupon.code}</p>
                {coupon.minimumSubtotal !== null ? <p className="mt-0.5 text-xs font-semibold text-slate-500">Min {formatMoney(coupon.minimumSubtotal)}</p> : null}
              </td>
              <td className="py-3 pr-4">
                <p className="font-bold text-slate-900">{coupon.name}</p>
                {coupon.description ? <p className="mt-0.5 max-w-[260px] truncate text-xs text-slate-500">{coupon.description}</p> : null}
              </td>
              <td className="py-3 pr-4">{formatDiscountType(coupon.discountType)}</td>
              <td className="py-3 pr-4 font-bold text-slate-900">{formatDiscount(coupon)}</td>
              <td className="py-3 pr-4 text-slate-600">{formatWindow(coupon)}</td>
              <td className="py-3 pr-4">
                <div className="grid gap-1">
                  <span className="font-bold text-slate-800">{formatUsage(coupon)}</span>
                  <span className="text-xs text-slate-500">{coupon.redemptionCount} redemptions</span>
                </div>
              </td>
              <td className="py-3 pr-4 text-slate-600">{getTargetSummary(coupon)}</td>
              <td className="py-3 pr-4">
                <div className="flex items-center gap-3">
                  <Toggle checked={coupon.isActive} disabled={statusPending} onChange={() => onToggleStatus(coupon)} />
                  <Pill tone={couponStatusTone(coupon)}>{formatCouponStatus(coupon)}</Pill>
                </div>
              </td>
              <td className="py-3 pr-4">
                <div className="flex flex-wrap gap-2">
                  <Button type="button" onClick={() => onEdit(coupon)}>Edit</Button>
                  <Button
                    type="button"
                    variant="danger"
                    disabled={coupon.redemptionCount > 0 || deletePending}
                    title={coupon.redemptionCount > 0 ? "Coupons with redemptions cannot be deleted. Deactivate instead." : undefined}
                    onClick={() => onDelete(coupon)}
                  >
                    Delete
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
