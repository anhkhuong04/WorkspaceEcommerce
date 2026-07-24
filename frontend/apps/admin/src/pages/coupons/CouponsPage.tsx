import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminCouponDto, AdminCouponListRequest } from "@workspace-ecommerce/api-types";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Notice } from "../../components/ui/AdminUi";
import { useAdminCoupons } from "../../hooks/queries/useAdminCoupons";
import { useAdminProducts } from "../../hooks/queries/useAdminProducts";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { formatLocalizedText } from "../../utils/localizedText";
import { CouponFilters } from "./components/CouponFilters";
import { CouponModal } from "./components/CouponModal";
import { CouponsTable } from "./components/CouponsTable";
import {
  couponDefaultValues,
  couponSchema,
  getProductLabel,
  isValidLocalDateTime,
  parseActiveFilter,
  parsePageNumber,
  toCouponFormValues,
  toCouponRequest,
  type CouponFormValues
} from "./couponTypes";

export function CouponsPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const [editingCoupon, setEditingCoupon] = useState<AdminCouponDto | null>(null);
  const [isCouponModalOpen, setIsCouponModalOpen] = useState(false);
  const [deleteCoupon, setDeleteCoupon] = useState<AdminCouponDto | null>(null);
  const [targetSearch, setTargetSearch] = useState("");

  const pageNumber = parsePageNumber(searchParams.get("page"));
  const activeFilter = parseActiveFilter(searchParams.get("active"));
  const searchFilter = searchParams.get("search")?.trim() || undefined;
  const effectiveAtFilter = searchParams.get("effectiveAt") ?? "";
  const effectiveAt = effectiveAtFilter && isValidLocalDateTime(effectiveAtFilter) ? new Date(effectiveAtFilter).toISOString() : undefined;

  const request = useMemo<AdminCouponListRequest>(() => ({
    pageNumber,
    pageSize: 10,
    search: searchFilter,
    isActive: activeFilter,
    effectiveAt
  }), [activeFilter, effectiveAt, pageNumber, searchFilter]);

  const couponsQuery = useAdminCoupons(request);
  const productsQuery = useAdminProducts({ pageNumber: 1, pageSize: 100 }, "target-picker");
  const products = useMemo(() => productsQuery.data?.items ?? [], [productsQuery.data?.items]);
  const productById = useMemo(() => new Map(products.map((product) => [product.id, product])), [products]);
  const filteredProducts = useMemo(() => {
    const normalizedSearch = targetSearch.trim().toLowerCase();
    if (!normalizedSearch) return products;

    return products.filter((product) =>
      formatLocalizedText(product.name, "").toLowerCase().includes(normalizedSearch) ||
      product.slug.toLowerCase().includes(normalizedSearch) ||
      (product.categoryName ?? "").toLowerCase().includes(normalizedSearch)
    );
  }, [products, targetSearch]);

  const couponForm = useForm<CouponFormValues>({ resolver: zodResolver(couponSchema), defaultValues: couponDefaultValues });
  const watchedDiscountType = useWatch({ control: couponForm.control, name: "discountType" });
  const watchedTargetScope = useWatch({ control: couponForm.control, name: "targetScope" });
  const watchedProductTargetIds = useWatch({ control: couponForm.control, name: "productTargetIds" }) ?? [];

  const couponSaveMutation = useMutation({
    mutationFn: (values: CouponFormValues) => {
      const requestBody = toCouponRequest(values);
      return editingCoupon ? adminApi.updateCoupon(editingCoupon.id, requestBody) : adminApi.createCoupon(requestBody);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-coupons"] });
      setIsCouponModalOpen(false);
      setEditingCoupon(null);
      setTargetSearch("");
      couponForm.reset(couponDefaultValues);
      setNotice({ type: "success", message: "Coupon saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const couponStatusMutation = useMutation({
    mutationFn: (coupon: AdminCouponDto) => adminApi.updateCouponStatus(coupon.id, { isActive: !coupon.isActive }),
    onSuccess: async (_, coupon) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-coupons"] });
      setNotice({ type: "success", message: coupon.isActive ? "Coupon deactivated." : "Coupon activated." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const couponDeleteMutation = useMutation({
    mutationFn: (coupon: AdminCouponDto) => adminApi.deleteCoupon(coupon.id),
    onSuccess: async (_, coupon) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-coupons"] });
      setDeleteCoupon(null);
      setNotice({ type: "success", message: coupon.redemptionCount > 0 ? "Coupon deactivated because it has usage history." : "Coupon deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  function updateFilters(values: { search?: string; isActive?: boolean; effectiveAt?: string }) {
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
    setEditingCoupon(null);
    setTargetSearch("");
    couponForm.reset(couponDefaultValues);
    setIsCouponModalOpen(true);
  }

  function openEditCouponModal(coupon: AdminCouponDto) {
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
    if (editingCoupon && values.usageLimit !== null && values.usageLimit < editingCoupon.usedCount) {
      couponForm.setError("usageLimit", { type: "manual", message: `Usage limit cannot be lower than used count (${editingCoupon.usedCount}).` });
      return;
    }

    couponSaveMutation.mutate(values);
  }

  function getTargetSummary(coupon: AdminCouponDto): string {
    if (coupon.productTargetIds.length === 0) return "All products";

    const [firstProductId] = coupon.productTargetIds;
    const firstProduct = firstProductId ? productById.get(firstProductId) : null;
    const firstLabel = firstProduct ? getProductLabel(firstProduct) : "Targeted products";

    return coupon.productTargetIds.length === 1 ? firstLabel : `${firstLabel} +${coupon.productTargetIds.length - 1}`;
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Coupons"
        description="Create and manage checkout coupons, product targeting, effective windows, usage limits, and active status."
        actions={<Button type="button" variant="primary" onClick={openCreateCouponModal}>New coupon</Button>}
      />

      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {couponsQuery.isError ? <Notice type="error" title="Coupons could not be loaded">{getApiErrorMessage(couponsQuery.error)}</Notice> : null}
      {productsQuery.isError ? <Notice type="warning" title="Products could not be loaded">Specific product targeting is unavailable until products load.</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <CouponFilters searchFilter={searchFilter} activeFilter={activeFilter} effectiveAtFilter={effectiveAtFilter} onUpdateFilters={updateFilters} />

        <CouponsTable
          coupons={couponsQuery.data?.items ?? []}
          isLoading={couponsQuery.isLoading}
          statusPending={couponStatusMutation.isPending}
          deletePending={couponDeleteMutation.isPending}
          getTargetSummary={getTargetSummary}
          onEdit={openEditCouponModal}
          onDelete={setDeleteCoupon}
          onToggleStatus={(coupon) => couponStatusMutation.mutate(coupon)}
        />

        {couponsQuery.data ? (
          <div className="mt-4 flex items-center justify-between text-sm font-semibold text-slate-500">
            <span>Page {couponsQuery.data.pageNumber} of {couponsQuery.data.totalPages || 1}</span>
            <div className="flex gap-2">
              <Button type="button" disabled={!couponsQuery.data.hasPreviousPage} onClick={() => updatePage(Math.max(1, pageNumber - 1))}>Previous</Button>
              <Button type="button" disabled={!couponsQuery.data.hasNextPage} onClick={() => updatePage(pageNumber + 1)}>Next</Button>
            </div>
          </div>
        ) : null}
      </section>

      <CouponModal
        editingCoupon={editingCoupon}
        open={isCouponModalOpen}
        form={couponForm}
        watchedDiscountType={watchedDiscountType}
        watchedTargetScope={watchedTargetScope}
        watchedProductTargetIds={watchedProductTargetIds}
        products={products}
        filteredProducts={filteredProducts}
        productsLoading={productsQuery.isLoading}
        targetSearch={targetSearch}
        savePending={couponSaveMutation.isPending}
        onTargetSearchChange={setTargetSearch}
        onClose={closeCouponModal}
        onSubmit={submitCoupon}
      />

      <ConfirmDialog
        open={deleteCoupon !== null}
        title="Delete coupon"
        message={deleteCoupon ? `Coupon ${deleteCoupon.code} will be permanently deleted because it has no redemptions.` : ""}
        confirmLabel="Delete"
        busy={couponDeleteMutation.isPending}
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
