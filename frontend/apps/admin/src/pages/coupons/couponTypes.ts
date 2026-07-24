import type { AdminCouponDto, AdminCouponUpsertRequest, AdminProductDto, CouponDiscountType } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import { z } from "zod";
import { formatLocalizedText } from "../../utils/localizedText";

export type CouponTargetScope = "all" | "products";

export const discountTypes: CouponDiscountType[] = [0, 1];

export const couponSchema = z.object({
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

export type CouponFormValues = z.infer<typeof couponSchema>;

export const couponDefaultValues: CouponFormValues = {
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

export function parsePageNumber(value: string | null): number {
  const pageNumber = Number(value);
  return Number.isInteger(pageNumber) && pageNumber > 0 ? pageNumber : 1;
}

export function parseActiveFilter(value: string | null): boolean | undefined {
  if (value === "true") return true;
  if (value === "false") return false;
  return undefined;
}

export function isValidLocalDateTime(value: string): boolean {
  return !Number.isNaN(new Date(value).getTime());
}

export function toIsoDateTime(value: string): string | null {
  return value.trim() ? new Date(value).toISOString() : null;
}

export function toDateTimeLocal(value: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 16);
}

export function formatDiscountType(type: CouponDiscountType): string {
  return type === 0 ? "Percentage" : "Fixed amount";
}

export function formatDiscount(coupon: AdminCouponDto): string {
  if (coupon.discountType === 0) {
    return coupon.maxDiscountAmount === null
      ? `${coupon.discountValue}%`
      : `${coupon.discountValue}% up to ${formatMoney(coupon.maxDiscountAmount)}`;
  }

  return formatMoney(coupon.discountValue);
}

export function formatWindow(coupon: AdminCouponDto): string {
  if (!coupon.startsAt && !coupon.endsAt) return "Always";
  if (coupon.startsAt && coupon.endsAt) return `${formatDate(coupon.startsAt)} - ${formatDate(coupon.endsAt)}`;
  if (coupon.startsAt) return `From ${formatDate(coupon.startsAt)}`;
  return `Until ${formatDate(coupon.endsAt ?? "")}`;
}

export function couponStatusTone(coupon: AdminCouponDto): "green" | "red" | "orange" | "slate" {
  if (!coupon.isActive) return "slate";
  if (coupon.usageLimit !== null && coupon.usedCount >= coupon.usageLimit) return "red";

  const now = Date.now();
  if (coupon.startsAt && new Date(coupon.startsAt).getTime() > now) return "orange";
  if (coupon.endsAt && new Date(coupon.endsAt).getTime() < now) return "red";

  return "green";
}

export function formatCouponStatus(coupon: AdminCouponDto): string {
  if (!coupon.isActive) return "Inactive";
  if (coupon.usageLimit !== null && coupon.usedCount >= coupon.usageLimit) return "Exhausted";

  const now = Date.now();
  if (coupon.startsAt && new Date(coupon.startsAt).getTime() > now) return "Scheduled";
  if (coupon.endsAt && new Date(coupon.endsAt).getTime() < now) return "Expired";

  return "Active";
}

export function formatUsage(coupon: AdminCouponDto): string {
  return coupon.usageLimit === null ? `${coupon.usedCount} used` : `${coupon.usedCount} / ${coupon.usageLimit}`;
}

export function toCouponFormValues(coupon: AdminCouponDto): CouponFormValues {
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

export function toCouponRequest(values: CouponFormValues): AdminCouponUpsertRequest {
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

export function getProductLabel(product: AdminProductDto): string {
  const productName = formatLocalizedText(product.name);
  return product.categoryName ? `${productName} (${product.categoryName})` : productName;
}
