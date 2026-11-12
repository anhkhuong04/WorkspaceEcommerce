import type { AdminCouponDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Pencil, Trash2 } from "lucide-react";
import {
  EmptyState,
  IconButton,
  Pill,
  TableViewport,
  Toggle,
} from "../../../components/ui/AdminUi";
import {
  couponStatusTone,
  formatCouponStatus,
  formatDiscount,
  formatUsage,
  formatWindow,
} from "../couponTypes";
type Props = {
  coupons: AdminCouponDto[];
  isLoading: boolean;
  statusPending: boolean;
  deletePending: boolean;
  getTargetSummary: (c: AdminCouponDto) => string;
  onEdit: (c: AdminCouponDto) => void;
  onDelete: (c: AdminCouponDto) => void;
  onToggleStatus: (c: AdminCouponDto) => void;
};
export function CouponsTable({
  coupons,
  isLoading,
  statusPending,
  deletePending,
  getTargetSummary,
  onEdit,
  onDelete,
  onToggleStatus,
}: Props) {
  if (isLoading)
    return (
      <div className="grid gap-3" role="status" aria-label="Loading coupons">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-slate-100" />
        ))}
      </div>
    );
  if (!coupons.length)
    return (
      <EmptyState>
        No coupons found. Try clearing the filters or create a coupon.
      </EmptyState>
    );
  return (
    <TableViewport label="Coupons">
      <table className="w-full min-w-[980px]">
        <thead>
          <tr>
            <th>Coupon</th>
            <th>Discount</th>
            <th>Validity</th>
            <th>Usage</th>
            <th>Eligibility</th>
            <th>Status</th>
            <th>
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {coupons.map((c) => (
            <tr key={c.id} className="border-b border-slate-100 last:border-0">
              <td>
                <p className="font-mono text-sm font-semibold">{c.code}</p>
                <p className="mt-1 max-w-[200px] text-xs text-[var(--muted)]">
                  {c.name}
                </p>
              </td>
              <td className="whitespace-nowrap font-medium tabular-nums">
                {formatDiscount(c)}
              </td>
              <td className="text-xs text-[var(--muted-strong)]">
                {formatWindow(c)}
              </td>
              <td>
                <p className="font-medium tabular-nums">{formatUsage(c)}</p>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  {c.redemptionCount} redemptions
                </p>
              </td>
              <td>
                <p className="max-w-[180px] text-xs text-[var(--muted-strong)]">
                  {getTargetSummary(c)}
                </p>
                {c.minimumSubtotal !== null ? (
                  <p className="mt-1 text-xs text-[var(--muted)]">
                    Min {formatMoney(c.minimumSubtotal)}
                  </p>
                ) : null}
              </td>
              <td>
                <div className="flex items-center gap-2">
                  <Toggle
                    label={`Status of coupon ${c.code}`}
                    checked={c.isActive}
                    disabled={statusPending}
                    onChange={() => onToggleStatus(c)}
                  />
                  <Pill tone={couponStatusTone(c)}>
                    {formatCouponStatus(c)}
                  </Pill>
                </div>
              </td>
              <td>
                <div className="flex gap-1">
                  <IconButton
                    label={`Edit coupon ${c.code}`}
                    onClick={() => onEdit(c)}
                  >
                    <Pencil size={17} aria-hidden="true" />
                  </IconButton>
                  <IconButton
                    label={`Delete coupon ${c.code}`}
                    variant="danger"
                    disabled={c.redemptionCount > 0 || deletePending}
                    title={
                      c.redemptionCount > 0
                        ? "This coupon has redemptions. Deactivate it instead."
                        : undefined
                    }
                    onClick={() => onDelete(c)}
                  >
                    <Trash2 size={17} aria-hidden="true" />
                  </IconButton>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableViewport>
  );
}
