import { Pagination } from "../../components/ui/Pagination";
import { Plus } from "lucide-react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type {
  AdminCouponDto,
  AdminCouponListRequest,
} from "@workspace-ecommerce/api-types";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Notice } from "../../components/ui/AdminUi";
import { useAdminCoupons } from "../../hooks/queries/useAdminCoupons";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { CouponFilters } from "./components/CouponFilters";
import { CouponModal } from "./components/CouponModal";
import { CouponsTable } from "./components/CouponsTable";
import {
  couponDefaultValues,
  couponSchema,
  isValidLocalDateTime,
  parseActiveFilter,
  parsePageNumber,
  toCouponFormValues,
  toCouponRequest,
  type CouponFormValues,
} from "./couponTypes";

export function CouponsPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [notice, setNotice] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);
  const [editingCoupon, setEditingCoupon] = useState<AdminCouponDto | null>(
    null,
  );
  const [isCouponModalOpen, setIsCouponModalOpen] = useState(false);
  const [deleteCoupon, setDeleteCoupon] = useState<AdminCouponDto | null>(null);
  const [targetSearch, setTargetSearch] = useState("");

  const pageNumber = parsePageNumber(searchParams.get("page"));
  const activeFilter = parseActiveFilter(searchParams.get("active"));
  const searchFilter = searchParams.get("search")?.trim() || undefined;
  const effectiveAtFilter = searchParams.get("effectiveAt") ?? "";
  const effectiveAt =
    effectiveAtFilter && isValidLocalDateTime(effectiveAtFilter)
      ? new Date(effectiveAtFilter).toISOString()
      : undefined;

  const request = useMemo<AdminCouponListRequest>(
    () => ({
      pageNumber,
      pageSize: 10,
      search: searchFilter,
      isActive: activeFilter,
      effectiveAt,
    }),
    [activeFilter, effectiveAt, pageNumber, searchFilter],
  );

  const couponsQuery = useAdminCoupons(request);


  const couponForm = useForm<CouponFormValues>({
    resolver: zodResolver(couponSchema),
    defaultValues: couponDefaultValues,
  });
  const watchedDiscountType = useWatch({
    control: couponForm.control,
    name: "discountType",
  });
  const watchedTargetScope = useWatch({
    control: couponForm.control,
    name: "targetScope",
  });

  const couponSaveMutation = useMutation({
    mutationFn: (values: CouponFormValues) => {
      const requestBody = toCouponRequest(values);
      return editingCoupon
        ? adminApi.updateCoupon(editingCoupon.id, requestBody)
        : adminApi.createCoupon(requestBody);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-coupons"] });
      setIsCouponModalOpen(false);
      setEditingCoupon(null);
      setTargetSearch("");
      couponForm.reset(couponDefaultValues);
      setNotice({ type: "success", message: "Coupon saved." });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const couponStatusMutation = useMutation({
    mutationFn: (coupon: AdminCouponDto) =>
      adminApi.updateCouponStatus(coupon.id, { isActive: !coupon.isActive }),
    onSuccess: async (_, coupon) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-coupons"] });
      setNotice({
        type: "success",
        message: coupon.isActive ? "Coupon deactivated." : "Coupon activated.",
      });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const couponDeleteMutation = useMutation({
    mutationFn: (coupon: AdminCouponDto) => adminApi.deleteCoupon(coupon.id),
    onSuccess: async (_, coupon) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-coupons"] });
      setDeleteCoupon(null);
      setNotice({
        type: "success",
        message:
          coupon.redemptionCount > 0
            ? "Coupon deactivated because it has usage history."
            : "Coupon deleted.",
      });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  function updateFilters(values: {
    search?: string;
    isActive?: boolean;
    effectiveAt?: string;
  }) {
    const nextParams = new URLSearchParams(searchParams);
    if (values.search === undefined) nextParams.delete("search");
    else nextParams.set("search", values.search);
    if (values.isActive === undefined) nextParams.delete("active");
    else nextParams.set("active", String(values.isActive));
    if (!values.effectiveAt) nextParams.delete("effectiveAt");
    else nextParams.set("effectiveAt", values.effectiveAt);
    nextParams.delete("page");
    setSearchParams(nextParams);
  }

  function updatePage(nextPageNumber: number) {
    const nextParams = new URLSearchParams(searchParams);
    if (nextPageNumber <= 1) nextParams.delete("page");
    else nextParams.set("page", String(nextPageNumber));
    setSearchParams(nextParams);
  }

  function openCreateCouponModal() {
    couponSaveMutation.reset();
    setEditingCoupon(null);
    setTargetSearch("");
    couponForm.reset(couponDefaultValues);
    setIsCouponModalOpen(true);
  }

  function openEditCouponModal(coupon: AdminCouponDto) {
    couponSaveMutation.reset();
    setEditingCoupon(coupon);
    setTargetSearch("");
    couponForm.reset(toCouponFormValues(coupon));
    setIsCouponModalOpen(true);
  }

  function closeCouponModal() {
    if (couponSaveMutation.isPending) return;
    setIsCouponModalOpen(false);
    setEditingCoupon(null);
    setTargetSearch("");
    couponForm.reset(couponDefaultValues);
  }

  function submitCoupon(values: CouponFormValues) {
    if (
      editingCoupon &&
      values.usageLimit !== null &&
      values.usageLimit < editingCoupon.usedCount
    ) {
      couponForm.setError("usageLimit", {
        type: "manual",
        message: `Usage limit cannot be lower than used count (${editingCoupon.usedCount}).`,
      });
      return;
    }

    couponSaveMutation.mutate(values);
  }

  function getTargetSummary(coupon: AdminCouponDto): string {
    if (coupon.productTargetIds.length === 0) return "All products";

    return `${coupon.productTargetIds.length} selected products`;
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Coupons"
        description="Manage discounts, eligibility and usage limits."
        actions={
          <Button
            type="button"
            variant="primary"
            onClick={openCreateCouponModal}
          >
            <Plus size={17} aria-hidden="true" />
            New coupon
          </Button>
        }
      />

      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {couponsQuery.isError ? (
        <Notice type="error" title="Coupons could not be loaded">
          {getApiErrorMessage(couponsQuery.error)}
          <div className="mt-3">
            <Button
              disabled={couponsQuery.isFetching}
              onClick={() => void couponsQuery.refetch()}
            >
              Try again
            </Button>
          </div>
        </Notice>
      ) : null}

      <section className="admin-panel">
        <CouponFilters
          searchFilter={searchFilter}
          activeFilter={activeFilter}
          effectiveAtFilter={effectiveAtFilter}
          onUpdateFilters={updateFilters}
        />

        <CouponsTable
          coupons={couponsQuery.data?.items ?? []}
          isLoading={couponsQuery.isLoading}
          statusPending={couponStatusMutation.isPending}
          deletePending={couponDeleteMutation.isPending}
          getTargetSummary={getTargetSummary}
          onEdit={openEditCouponModal}
          onDelete={(coupon) => {
            couponDeleteMutation.reset();
            setDeleteCoupon(coupon);
          }}
          onToggleStatus={(coupon) => couponStatusMutation.mutate(coupon)}
        />

        {couponsQuery.data ? (
          <Pagination
            data={couponsQuery.data}
            label="coupons"
            busy={couponsQuery.isFetching}
            onPageChange={updatePage}
          />
        ) : null}
      </section>

      <CouponModal
        editingCoupon={editingCoupon}
        open={isCouponModalOpen}
        form={couponForm}
        watchedDiscountType={watchedDiscountType}
        watchedTargetScope={watchedTargetScope}
        targetSearch={targetSearch}
        savePending={couponSaveMutation.isPending}
        error={
          couponSaveMutation.isError
            ? getApiErrorMessage(couponSaveMutation.error)
            : undefined
        }
        onTargetSearchChange={setTargetSearch}
        onClose={closeCouponModal}
        onSubmit={submitCoupon}
      />

      <ConfirmDialog
        open={deleteCoupon !== null}
        title="Delete coupon"
        message={
          deleteCoupon
            ? `Coupon ${deleteCoupon.code} will be permanently deleted because it has no redemptions.`
            : ""
        }
        confirmLabel="Delete"
        busy={couponDeleteMutation.isPending}
        error={
          couponDeleteMutation.isError
            ? getApiErrorMessage(couponDeleteMutation.error)
            : undefined
        }
        onCancel={() => setDeleteCoupon(null)}
        onConfirm={() => {
          if (deleteCoupon) {
            couponDeleteMutation.mutate(deleteCoupon);
          }
        }}
      />
    </div>
  );
}
