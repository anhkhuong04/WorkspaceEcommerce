import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminCouponDto, AdminCouponListRequest, AdminCouponUpsertRequest, AdminProductDto, CouponDiscountType } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import { useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, EmptyState, Field, Modal, Notice, Pill, SelectInput, TextArea, TextInput, Toggle } from "../../components/ui/AdminUi";
import { cx } from "../../components/ui/cx";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

type CouponTargetScope = "all" | "products";

const discountTypes: CouponDiscountType[] = [0, 1];

const couponSchema = z.object({
  code: z.string().trim().min(1, "Code is required.").max(50, "Code is too long.").regex(/^[A-Za-z0-9_-]+$/, "Code must use letters, numbers, underscores, or hyphens."),
  name: z.string().trim().min(1, "Name is required.").max(250, "Name is too long."),
  description: z.string().trim().max(1000, "Description is too long."),
  discountType: z.union([z.literal(0), z.literal(1)]),
  discountValue: z.number().positive("Discount value must be greater than zero."),
  maxDiscountAmount: z.number().min(0, "Max discount cannot be negative.").nullable(),
  minimumSubtotal: z.number().min(0, "Minimum subtotal cannot be negative.").nullable(),
  startsAt: z.string(),
  endsAt: z.string(),
  usageLimit: z.number().int("Usage limit must be an integer.").positive("Usage limit must be greater than zero.").nullable(),
  isActive: z.boolean(),
  targetScope: z.enum(["all", "products"]),
  productTargetIds: z.array(z.string().min(1))
})
  .refine((values) => values.discountType !== 0 || values.discountValue <= 100, { path: ["discountValue"], message: "Percentage discount cannot exceed 100." })
  .refine((values) => !values.startsAt || isValidLocalDateTime(values.startsAt), { path: ["startsAt"], message: "Start time is invalid." })
  .refine((values) => !values.endsAt || isValidLocalDateTime(values.endsAt), { path: ["endsAt"], message: "End time is invalid." })
  .refine((values) => !values.startsAt || !values.endsAt || new Date(values.endsAt).getTime() > new Date(values.startsAt).getTime(), { path: ["endsAt"], message: "End time must be after start time." })
  .refine((values) => values.targetScope === "all" || values.productTargetIds.length > 0, { path: ["productTargetIds"], message: "Select at least one target product." });

type CouponFormValues = z.infer<typeof couponSchema>;

const couponDefaultValues: CouponFormValues = {
  code: "",
  name: "",
  description: "",
  discountType: 0,
  discountValue: 10,
  maxDiscountAmount: null,
  minimumSubtotal: null,
  startsAt: "",
  endsAt: "",
  usageLimit: null,
  isActive: true,
  targetScope: "all",
  productTargetIds: []
};

function parsePageNumber(value: string | null): number {
  const pageNumber = Number(value);
  return Number.isInteger(pageNumber) && pageNumber > 0 ? pageNumber : 1;
}

function parseActiveFilter(value: string | null): boolean | undefined {
  if (value === "true") return true;
  if (value === "false") return false;
  return undefined;
}

function isValidLocalDateTime(value: string): boolean {
  return !Number.isNaN(new Date(value).getTime());
}

function toIsoDateTime(value: string): string | null {
  return value.trim() ? new Date(value).toISOString() : null;
}

function toDateTimeLocal(value: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 16);
}

function formatDiscountType(type: CouponDiscountType): string {
  return type === 0 ? "Percentage" : "Fixed amount";
}

function formatDiscount(coupon: AdminCouponDto): string {
  if (coupon.discountType === 0) {
    return coupon.maxDiscountAmount === null
      ? `${coupon.discountValue}%`
      : `${coupon.discountValue}% up to ${formatMoney(coupon.maxDiscountAmount)}`;
  }

  return formatMoney(coupon.discountValue);
}

function formatWindow(coupon: AdminCouponDto): string {
  if (!coupon.startsAt && !coupon.endsAt) return "Always";
  if (coupon.startsAt && coupon.endsAt) return `${formatDate(coupon.startsAt)} - ${formatDate(coupon.endsAt)}`;
  if (coupon.startsAt) return `From ${formatDate(coupon.startsAt)}`;
  return `Until ${formatDate(coupon.endsAt ?? "")}`;
}

function couponStatusTone(coupon: AdminCouponDto): "green" | "red" | "orange" | "slate" {
  if (!coupon.isActive) return "slate";
  if (coupon.usageLimit !== null && coupon.usedCount >= coupon.usageLimit) return "red";

  const now = Date.now();
  if (coupon.startsAt && new Date(coupon.startsAt).getTime() > now) return "orange";
  if (coupon.endsAt && new Date(coupon.endsAt).getTime() < now) return "red";

  return "green";
}

function formatCouponStatus(coupon: AdminCouponDto): string {
  if (!coupon.isActive) return "Inactive";
  if (coupon.usageLimit !== null && coupon.usedCount >= coupon.usageLimit) return "Exhausted";

  const now = Date.now();
  if (coupon.startsAt && new Date(coupon.startsAt).getTime() > now) return "Scheduled";
  if (coupon.endsAt && new Date(coupon.endsAt).getTime() < now) return "Expired";

  return "Active";
}

function formatUsage(coupon: AdminCouponDto): string {
  return coupon.usageLimit === null ? `${coupon.usedCount} used` : `${coupon.usedCount} / ${coupon.usageLimit}`;
}

function toCouponFormValues(coupon: AdminCouponDto): CouponFormValues {
  return {
    code: coupon.code,
    name: coupon.name,
    description: coupon.description ?? "",
    discountType: coupon.discountType,
    discountValue: coupon.discountValue,
    maxDiscountAmount: coupon.maxDiscountAmount,
    minimumSubtotal: coupon.minimumSubtotal,
    startsAt: toDateTimeLocal(coupon.startsAt),
    endsAt: toDateTimeLocal(coupon.endsAt),
    usageLimit: coupon.usageLimit,
    isActive: coupon.isActive,
    targetScope: coupon.productTargetIds.length === 0 ? "all" : "products",
    productTargetIds: coupon.productTargetIds
  };
}

function toCouponRequest(values: CouponFormValues): AdminCouponUpsertRequest {
  return {
    code: values.code.trim().toUpperCase(),
    name: values.name.trim(),
    description: values.description.trim() ? values.description.trim() : null,
    discountType: values.discountType,
    discountValue: values.discountValue,
    maxDiscountAmount: values.discountType === 0 ? values.maxDiscountAmount : null,
    minimumSubtotal: values.minimumSubtotal,
    startsAt: toIsoDateTime(values.startsAt),
    endsAt: toIsoDateTime(values.endsAt),
    usageLimit: values.usageLimit,
    isActive: values.isActive,
    productTargetIds: values.targetScope === "all" ? [] : Array.from(new Set(values.productTargetIds))
  };
}

function getProductLabel(product: AdminProductDto): string {
  return product.categoryName ? `${product.name} (${product.categoryName})` : product.name;
}

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

  const couponsQuery = useQuery({ queryKey: ["admin-coupons", request], queryFn: () => adminApi.getCoupons(request) });
  const productsQuery = useQuery({ queryKey: ["admin-products"], queryFn: adminApi.getProducts });

  const productById = useMemo(() => new Map((productsQuery.data ?? []).map((product) => [product.id, product])), [productsQuery.data]);
  const filteredProducts = useMemo(() => {
    const normalizedSearch = targetSearch.trim().toLowerCase();
    const products = productsQuery.data ?? [];
    if (!normalizedSearch) return products;

    return products.filter((product) =>
      product.name.toLowerCase().includes(normalizedSearch) ||
      product.slug.toLowerCase().includes(normalizedSearch) ||
      (product.categoryName ?? "").toLowerCase().includes(normalizedSearch)
    );
  }, [productsQuery.data, targetSearch]);

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
    const firstLabel = firstProduct ? firstProduct.name : "Targeted products";

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
        <div className="mb-4 grid gap-3 lg:grid-cols-[minmax(220px,1fr)_180px_220px_auto]">
          <TextInput
            key={searchFilter ?? ""}
            defaultValue={searchFilter ?? ""}
            placeholder="Search code or name"
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                updateFilters({ search: event.currentTarget.value.trim() || undefined, isActive: activeFilter, effectiveAt: effectiveAtFilter });
              }
            }}
          />
          <SelectInput
            value={activeFilter === undefined ? "" : String(activeFilter)}
            onChange={(event) => updateFilters({
              search: searchFilter,
              isActive: event.target.value === "" ? undefined : event.target.value === "true",
              effectiveAt: effectiveAtFilter
            })}
          >
            <option value="">All statuses</option>
            <option value="true">Active only</option>
            <option value="false">Inactive only</option>
          </SelectInput>
          <TextInput
            key={effectiveAtFilter}
            type="datetime-local"
            defaultValue={effectiveAtFilter}
            onChange={(event) => updateFilters({ search: searchFilter, isActive: activeFilter, effectiveAt: event.currentTarget.value })}
          />
          <Button type="button" onClick={() => updateFilters({ search: undefined, isActive: undefined, effectiveAt: undefined })}>Clear filters</Button>
        </div>

        {couponsQuery.isLoading ? (
          <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-16 animate-pulse rounded-2xl bg-slate-100" />)}</div>
        ) : couponsQuery.data?.items.length ? (
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
                {couponsQuery.data.items.map((coupon) => (
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
                        <Toggle checked={coupon.isActive} disabled={couponStatusMutation.isPending} onChange={() => couponStatusMutation.mutate(coupon)} />
                        <Pill tone={couponStatusTone(coupon)}>{formatCouponStatus(coupon)}</Pill>
                      </div>
                    </td>
                    <td className="py-3 pr-4">
                      <div className="flex flex-wrap gap-2">
                        <Button type="button" onClick={() => openEditCouponModal(coupon)}>Edit</Button>
                        <Button
                          type="button"
                          variant="danger"
                          disabled={coupon.redemptionCount > 0 || couponDeleteMutation.isPending}
                          title={coupon.redemptionCount > 0 ? "Coupons with redemptions cannot be deleted. Deactivate instead." : undefined}
                          onClick={() => setDeleteCoupon(coupon)}
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
        ) : <EmptyState>No coupons found</EmptyState>}

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

      <Modal
        title={editingCoupon ? `Edit ${editingCoupon.code}` : "New coupon"}
        open={isCouponModalOpen}
        onClose={closeCouponModal}
        widthClass="max-w-5xl"
        footer={(
          <>
            <Button type="button" disabled={couponSaveMutation.isPending} onClick={closeCouponModal}>Cancel</Button>
            <Button type="button" variant="primary" disabled={couponSaveMutation.isPending} onClick={couponForm.handleSubmit(submitCoupon)}>{couponSaveMutation.isPending ? "Saving..." : "Save"}</Button>
          </>
        )}
      >
        <form className="grid gap-5" noValidate>
          {editingCoupon && editingCoupon.redemptionCount > 0 ? (
            <Notice type="warning" title="This coupon has redemption history">
              Rule changes affect future checkouts only. Existing orders keep their coupon code, name, and discount snapshot.
            </Notice>
          ) : null}

          <div className="grid gap-4 md:grid-cols-2">
            <Controller control={couponForm.control} name="code" render={({ field, fieldState }) => <Field label="Code" error={fieldState.error?.message}><TextInput {...field} placeholder="SUMMER10" /></Field>} />
            <Controller control={couponForm.control} name="name" render={({ field, fieldState }) => <Field label="Name" error={fieldState.error?.message}><TextInput {...field} placeholder="Summer campaign" /></Field>} />
          </div>

          <Controller control={couponForm.control} name="description" render={({ field, fieldState }) => <Field label="Description" error={fieldState.error?.message}><TextArea {...field} rows={3} placeholder="Internal campaign note" /></Field>} />

          <div className="grid gap-4 lg:grid-cols-3">
            <Controller
              control={couponForm.control}
              name="discountType"
              render={({ field, fieldState }) => (
                <Field label="Discount type" error={fieldState.error?.message}>
                  <SelectInput value={field.value} onChange={(event) => field.onChange(Number(event.target.value) as CouponDiscountType)}>
                    {discountTypes.map((type) => <option key={type} value={type}>{formatDiscountType(type)}</option>)}
                  </SelectInput>
                </Field>
              )}
            />
            <Controller control={couponForm.control} name="discountValue" render={({ field, fieldState }) => <Field label={watchedDiscountType === 0 ? "Discount percent" : "Discount amount"} error={fieldState.error?.message}><TextInput type="number" min={0} step="0.01" value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
            <Controller control={couponForm.control} name="maxDiscountAmount" render={({ field, fieldState }) => <Field label="Max discount" error={fieldState.error?.message}><TextInput type="number" min={0} step="0.01" value={field.value ?? ""} disabled={watchedDiscountType !== 0} onChange={(event) => field.onChange(event.target.value === "" ? null : Number(event.target.value))} /></Field>} />
          </div>

          <div className="grid gap-4 lg:grid-cols-4">
            <Controller control={couponForm.control} name="minimumSubtotal" render={({ field, fieldState }) => <Field label="Minimum subtotal" error={fieldState.error?.message}><TextInput type="number" min={0} step="0.01" value={field.value ?? ""} onChange={(event) => field.onChange(event.target.value === "" ? null : Number(event.target.value))} /></Field>} />
            <Controller control={couponForm.control} name="usageLimit" render={({ field, fieldState }) => <Field label="Usage limit" error={fieldState.error?.message}><TextInput type="number" min={1} step={1} value={field.value ?? ""} onChange={(event) => field.onChange(event.target.value === "" ? null : Number(event.target.value))} /></Field>} />
            <Controller control={couponForm.control} name="startsAt" render={({ field, fieldState }) => <Field label="Starts at" error={fieldState.error?.message}><TextInput type="datetime-local" value={field.value} onChange={field.onChange} /></Field>} />
            <Controller control={couponForm.control} name="endsAt" render={({ field, fieldState }) => <Field label="Ends at" error={fieldState.error?.message}><TextInput type="datetime-local" value={field.value} onChange={field.onChange} /></Field>} />
          </div>

          <div className="grid gap-4 md:grid-cols-[220px_1fr]">
            <Controller control={couponForm.control} name="isActive" render={({ field }) => <Field label="Active"><div className="flex items-center gap-3"><Toggle checked={field.value} onChange={field.onChange} /><Pill tone={field.value ? "green" : "slate"}>{field.value ? "Active" : "Inactive"}</Pill></div></Field>} />
            <Controller
              control={couponForm.control}
              name="targetScope"
              render={({ field, fieldState }) => (
                <Field label="Target products" error={fieldState.error?.message}>
                  <SelectInput
                    value={field.value}
                    onChange={(event) => {
                      const nextScope = event.target.value as CouponTargetScope;
                      field.onChange(nextScope);
                      if (nextScope === "all") {
                        couponForm.setValue("productTargetIds", [], { shouldDirty: true, shouldValidate: true });
                      }
                    }}
                  >
                    <option value="all">All products</option>
                    <option value="products">Specific products</option>
                  </SelectInput>
                </Field>
              )}
            />
          </div>

          {watchedTargetScope === "products" ? (
            <Controller
              control={couponForm.control}
              name="productTargetIds"
              render={({ field, fieldState }) => (
                <div>
                  <span className="mb-1.5 block text-sm font-bold text-slate-700">Product picker ({watchedProductTargetIds.length} selected)</span>
                  <div className="grid gap-3">
                    <TextInput value={targetSearch} onChange={(event) => setTargetSearch(event.currentTarget.value)} placeholder="Search products or categories" />
                    {productsQuery.isLoading ? (
                      <div className="grid gap-2">{[0, 1, 2].map((item) => <div key={item} className="h-12 animate-pulse rounded-xl bg-slate-100" />)}</div>
                    ) : filteredProducts.length ? (
                      <div className="admin-table-scroll max-h-72 overflow-y-auto rounded-2xl border border-slate-200">
                        {filteredProducts.map((product) => {
                          const checked = field.value.includes(product.id);
                          return (
                            <label key={product.id} className={cx("flex cursor-pointer items-center justify-between gap-4 border-b border-slate-100 px-4 py-3 last:border-0 hover:bg-slate-50", checked && "bg-slate-100")}>
                              <span className="min-w-0">
                                <span className="block truncate text-sm font-bold text-slate-900">{getProductLabel(product)}</span>
                                <span className="mt-0.5 flex flex-wrap items-center gap-2 text-xs font-semibold text-slate-500">
                                  <span>{product.slug}</span>
                                  <Pill tone={product.isActive ? "green" : "slate"}>{product.isActive ? "Active" : "Inactive"}</Pill>
                                </span>
                              </span>
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={(event) => {
                                  if (event.currentTarget.checked) {
                                    field.onChange([...field.value, product.id]);
                                  } else {
                                    field.onChange(field.value.filter((id) => id !== product.id));
                                  }
                                }}
                                className="h-4 w-4 rounded border-slate-300 text-slate-700 focus:ring-slate-600"
                              />
                            </label>
                          );
                        })}
                      </div>
                    ) : <EmptyState>No products match this search</EmptyState>}
                  </div>
                  {fieldState.error?.message ? <span className="mt-1 block text-sm font-semibold text-red-600" role="alert">{fieldState.error.message}</span> : null}
                </div>
              )}
            />
          ) : null}
        </form>
      </Modal>

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
